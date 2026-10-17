using System.Net;
using Argos.Contracts.Realtime;
using Argos.Simulation.Configuration;
using Argos.Simulation.Generators;
using Argos.Simulator.Clients;
using Argos.Simulator.Configuration;
using Microsoft.Extensions.Options;

namespace Argos.Simulator.Workers;

/// <summary>
/// Envia à API, a cada intervalo configurado, o sinal de vida técnico gerado pela simulação (temporário, Fase 0; plan 4.4).
/// Se a API estiver indisponível, tenta de novo com espera progressiva, sem encerrar.
/// </summary>
internal sealed class TechnicalHeartbeatWorker(
    ArgosApiClient apiClient,
    IOptions<SimulatorOptions> simulatorOptions,
    IOptions<SimulationOptions> simulationOptions,
    TimeProvider timeProvider,
    ILogger<TechnicalHeartbeatWorker> logger) : BackgroundService
{
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = simulatorOptions.Value;
        var interval = TimeSpan.FromSeconds(options.HeartbeatIntervalSeconds);
        var generator = new TechnicalHeartbeatGenerator(simulationOptions.Value);

        logger.LogInformation(
            "Sinal de vida técnico a cada {IntervalSeconds} s para {ApiBaseUrl}.",
            options.HeartbeatIntervalSeconds,
            options.ApiBaseUrl);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var simulated = generator.Next();
                await Task.Delay(simulated.Jitter, timeProvider, stoppingToken);
                await SendWithRetryAsync(options.Source, simulated.Sequence, stoppingToken);
                await Task.Delay(interval, timeProvider, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Encerramento normal do host.
        }
    }

    private async Task SendWithRetryAsync(string source, long sequence, CancellationToken stoppingToken)
    {
        var retryDelay = InitialRetryDelay;

        for (var attempt = 1; ; attempt++)
        {
            var heartbeat = new TechnicalHeartbeat
            {
                Source = source,
                Sequence = sequence,
                SentAt = timeProvider.GetUtcNow(),
            };

            try
            {
                var status = await apiClient.SendHeartbeatAsync(heartbeat, stoppingToken);
                if ((int)status is >= 200 and < 300)
                {
                    logger.LogInformation(
                        "Sinal de vida {Sequence} enviado: {StatusCode} ({Status}).",
                        sequence,
                        (int)status,
                        status);
                    return;
                }

                if (!IsTransient(status))
                {
                    // Erro do próprio envio: repetir não resolve, então o sinal é descartado.
                    logger.LogError(
                        "Sinal de vida {Sequence} recusado pela API: {StatusCode} ({Status}). Sinal descartado.",
                        sequence,
                        (int)status,
                        status);
                    return;
                }

                logger.LogWarning(
                    "API respondeu {StatusCode} ({Status}) ao sinal de vida {Sequence}. Tentativa {Attempt}; nova tentativa em {RetrySeconds} s.",
                    (int)status,
                    status,
                    sequence,
                    attempt,
                    retryDelay.TotalSeconds);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                && !stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    "API indisponível ao enviar o sinal de vida {Sequence}: {Error}. Tentativa {Attempt}; nova tentativa em {RetrySeconds} s.",
                    sequence,
                    exception.Message,
                    attempt,
                    retryDelay.TotalSeconds);
            }

            await Task.Delay(retryDelay, timeProvider, stoppingToken);
            retryDelay = TimeSpan.FromTicks(Math.Min(retryDelay.Ticks * 2, MaxRetryDelay.Ticks));
        }
    }

    /// <summary>
    /// Falhas que podem passar sozinhas: 5xx, 408 (tempo esgotado) e 429 (excesso de requisições).
    /// </summary>
    private static bool IsTransient(HttpStatusCode status) =>
        (int)status >= 500
        || status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests;
}
