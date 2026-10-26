using System.Net;
using Argos.Contracts.Telemetry;
using Argos.Simulation.Configuration;
using Argos.Simulation.Generators;
using Argos.Simulation.Geography;
using Argos.Simulation.Models;
using Argos.Simulation.Movement;
using Argos.Simulator.Clients;
using Argos.Simulator.Configuration;
using Microsoft.Extensions.Options;

namespace Argos.Simulator.Workers;

/// <summary>
/// PROMETHEUS (plan 16.5; D-72 a D-76): a cada <c>PositionIntervalSeconds</c>, lê os recursos ativos da API, monta o
/// lote do ciclo pela simulação pura (<see cref="TelemetryBatchBuilder"/>, semente da configuração) e o envia. Substitui
/// o sinal de vida técnico da Fase 0 (D-73).
/// <list type="bullet">
/// <item>O estado do ciclo (bordas do turno e últimos sinais de vida) só é adotado depois do <c>202</c>; num <c>409</c>
/// ou numa falha passageira, as bordas ficam pendentes e vão no ciclo seguinte.</item>
/// <item><c>401</c>/<c>403</c> são erro de configuração da chave de sistema (14.9), registrados como erro.</item>
/// <item>API fora do ar: nova tentativa com espera progressiva de 1 s a 30 s, sem encerrar.</item>
/// </list>
/// </summary>
internal sealed class TelemetryWorker(
    ArgosApiClient apiClient,
    IOptions<TelemetryOptions> telemetryOptions,
    IOptions<SimulationOptions> simulationOptions,
    TimeProvider timeProvider,
    ILogger<TelemetryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = telemetryOptions.Value;
        var interval = TimeSpan.FromSeconds(options.PositionIntervalSeconds);
        var settings = new TelemetryBatchSettings(
            simulationOptions.Value.Seed,
            TimeSpan.FromSeconds(options.FixedHeartbeatIntervalSeconds),
            new TelemetrySpeeds(
                options.SpeedsKmh.Vehicle, options.SpeedsKmh.Motorcycle, options.SpeedsKmh.Scooter, options.SpeedsKmh.OnFoot));
        var builder = new TelemetryBatchBuilder(new PatrolRoutes(PrometeiaGeography.Load()));
        var state = TelemetryCycleState.Empty;
        var retryDelay = InitialRetryDelay;

        logger.LogInformation(
            "Telemetria a cada {IntervalSeconds} s, sinais de vida dos fixos a cada {HeartbeatSeconds} s, semente {Seed}.",
            options.PositionIntervalSeconds,
            options.FixedHeartbeatIntervalSeconds,
            settings.Seed);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                TimeSpan wait;
                try
                {
                    state = await RunCycleAsync(builder, state, settings, stoppingToken);
                    retryDelay = InitialRetryDelay;
                    wait = interval;
                }
                catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                    && !stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning(
                        "API indisponível no ciclo de telemetria: {Error}. Nova tentativa em {RetrySeconds} s.",
                        exception.Message,
                        retryDelay.TotalSeconds);
                    wait = retryDelay;
                    retryDelay = TimeSpan.FromTicks(Math.Min(retryDelay.Ticks * 2, MaxRetryDelay.Ticks));
                }

                await Task.Delay(wait, timeProvider, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Encerramento normal do host.
        }
    }

    /// <returns>O estado a usar no ciclo seguinte: o novo, se a API aceitou o lote; senão, o anterior.</returns>
    private async Task<TelemetryCycleState> RunCycleAsync(
        TelemetryBatchBuilder builder, TelemetryCycleState previous, TelemetryBatchSettings settings, CancellationToken stoppingToken)
    {
        var (discoveryStatus, resources) = await apiClient.GetResourcesAsync(stoppingToken);
        if (resources is null)
        {
            LogRefusal("a descoberta dos recursos", discoveryStatus);
            return previous;
        }

        var cycle = builder.Build(
            TelemetryTranslation.ToSimulation(resources), timeProvider.GetUtcNow(), previous, settings);
        var (status, result) = await apiClient.SendBatchAsync(TelemetryTranslation.ToRequest(cycle.Batch), stoppingToken);

        if (status == HttpStatusCode.Accepted && result is not null)
        {
            logger.LogInformation(
                "Lote aceito: {Reports} situações, {Positions} posições, {Heartbeats} sinais de vida, {Ignored} ignorados.",
                result.StatusReportsApplied,
                result.PositionsAccepted,
                result.HeartbeatsAccepted,
                result.Ignored);
            return cycle.NextState;
        }

        LogRefusal("o lote de telemetria", status);
        return previous;
    }

    private void LogRefusal(string what, HttpStatusCode status)
    {
        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            logger.LogError(
                "Chave de sistema recusada pela API para {What}: {StatusCode} ({Status}). Confira Simulator:SystemKey "
                    + "(ARGOS_SYSTEM_KEY).",
                what,
                (int)status,
                status);
            return;
        }

        // 409 (concorrência) e falhas passageiras: as bordas ficam pendentes para o ciclo seguinte.
        logger.LogWarning(
            "API respondeu {StatusCode} ({Status}) para {What}; tentativa de novo no próximo ciclo.", (int)status, status, what);
    }
}

/// <summary>Tradução entre os contratos da API e os modelos próprios da Simulação (plan 16.5), pelos nomes dos enums.</summary>
internal static class TelemetryTranslation
{
    public static SimulatedTelemetryResources ToSimulation(TelemetryResourceList resources) => new(
        resources.Equipment
            .Select(item => new SimulatedTelemetryEquipment(
                item.Id,
                item.Key,
                item.TypeKey,
                Convert<SimulatedEquipmentCategory>(item.Category),
                Convert<SimulatedEquipmentStatus>(item.Status),
                item.CrewAssignable,
                item.BaseLatitude is { } latitude && item.BaseLongitude is { } longitude ? new GeoCoordinate(latitude, longitude) : null,
                item.BaseRegion))
            .ToArray(),
        resources.Agents
            .Select(agent => new SimulatedTelemetryAgent(
                agent.Id,
                agent.Key,
                Convert<SimulatedAgentStatus>(agent.Status),
                new GeoCoordinate(agent.BaseLatitude, agent.BaseLongitude),
                agent.BaseRegion,
                agent.AssignedEquipmentId,
                agent.ScheduledOnDuty))
            .ToArray());

    public static TelemetryBatchRequest ToRequest(SimulatedTelemetryBatch batch) => new()
    {
        SentAt = batch.SentAt,
        IsSimulated = true,
        AgentStatusReports = batch.StatusReports
            .Select(report => new AgentStatusReportItem
            {
                AgentId = report.AgentId,
                Status = Convert<Contracts.Inventory.AgentStatus>(report.Status),
                ReportedAt = report.ReportedAt,
            })
            .ToList(),
        Positions = batch.Positions
            .Select(position => new TelemetryPositionItem
            {
                ResourceKind = Convert<Contracts.Operations.ResourceKind>(position.ResourceKind),
                ResourceId = position.ResourceId,
                Latitude = position.Latitude,
                Longitude = position.Longitude,
                RecordedAt = position.RecordedAt,
            })
            .ToList(),
        Heartbeats = batch.Heartbeats
            .Select(heartbeat => new TelemetryHeartbeatItem { EquipmentId = heartbeat.EquipmentId, RecordedAt = heartbeat.RecordedAt })
            .ToList(),
    };

    private static TTarget Convert<TTarget>(Enum value)
        where TTarget : struct, Enum =>
        Enum.Parse<TTarget>(value.ToString());
}
