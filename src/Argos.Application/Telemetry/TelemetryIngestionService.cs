using Argos.Application.Common;
using Argos.Application.Common.Abstractions;
using Argos.Application.Operations;
using Argos.Contracts.Access;
using Argos.Contracts.Operations;
using Argos.Contracts.Realtime;
using Argos.Contracts.Telemetry;
using Argos.Domain.Inventory.Agents;
using Argos.Domain.Inventory.Types;
using Argos.Domain.Telemetry;
using DomainAgentStatus = Argos.Domain.Inventory.Agents.AgentStatus;
using DomainResourceKind = Argos.Domain.Telemetry.ResourceKind;

namespace Argos.Application.Telemetry;

/// <summary>
/// Ingestão do lote do PROMETHEUS (RF-023, RF-024; RN-12, RN-13, RN-27; Art. VIII.2; D-71, D-80; plan 16.4, 16.11).
/// <list type="number">
/// <item>Situações reportadas (RN-12, origem <c>Telemetry</c>, instante = <c>reportedAt</c>), para o agente que entra
/// em turno já ter a posição aceita no mesmo lote.</item>
/// <item>Posições: de equipamento móvel ativo e de agente ativo em atividade (RN-13). A de agente fora de atividade é
/// descartada sem gravar (Art. VIII.2).</item>
/// <item>Sinais de vida de equipamento fixo ativo.</item>
/// </list>
/// Recurso inexistente ou inativo, categoria errada, agente fora de atividade e situação igual à atual são
/// <b>ignorados e contados</b>, sem erro (D-71). Cada item aceito grava uma leitura e atualiza o estado atual, que só
/// avança. Tudo num <c>SaveChanges</c>; depois do commit, <c>OperationsChanged</c> (se alguma situação mudou),
/// <c>TelemetryBatchProcessed</c> para todos, sem viaturas e motos, e <c>AgentPositionsUpdated</c> só para
/// <c>agents.positions.view</c>, com agentes, viaturas e motos (RN-27, A-38). O autor é o do <see cref="ICurrentActor"/>
/// (<c>PROMETHEUS</c>).
/// </summary>
public sealed class TelemetryIngestionService(
    ITelemetryRepository telemetry,
    ITelemetryQueries queries,
    IUnitOfWork unitOfWork,
    IRealtimeNotifier notifier,
    ICurrentActor actor,
    TimeProvider timeProvider)
{
    /// <exception cref="Common.Exceptions.ValidationFailedException">Formato inválido, por campo.</exception>
    public async Task<TelemetryBatchResult> IngestAsync(TelemetryBatchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var receivedAt = timeProvider.GetUtcNow();
        TelemetryBatchValidator.Validate(request, receivedAt);

        var author = actor.Name;
        var isSimulated = request.IsSimulated!.Value;
        var reports = request.AgentStatusReports ?? [];
        var positions = request.Positions ?? [];
        var heartbeats = request.Heartbeats ?? [];

        var agentIds = reports.Select(report => report.AgentId!.Value)
            .Concat(positions.Where(IsAgent).Select(position => position.ResourceId!.Value))
            .ToHashSet();
        var equipmentIds = positions.Where(position => !IsAgent(position)).Select(position => position.ResourceId!.Value)
            .Concat(heartbeats.Select(heartbeat => heartbeat.EquipmentId!.Value))
            .ToHashSet();

        var agents = (await telemetry.FindAgentsAsync(agentIds, cancellationToken)).ToDictionary(agent => agent.Id);
        var equipment = await queries.FindEquipmentFactsAsync(equipmentIds, cancellationToken);
        var batch = new Batch(receivedAt, isSimulated, author);

        // 1) Situações reportadas (RN-12).
        foreach (var report in reports)
        {
            var target = EnumNames.Convert<DomainAgentStatus>(report.Status!.Value);
            if (!agents.TryGetValue(report.AgentId!.Value, out var agent) || !agent.IsActive || agent.Status == target)
            {
                batch.Ignored++;
                continue;
            }

            agent.ChangeStatus(target, report.ReportedAt!.Value, author, AgentStatusChangeOrigin.Telemetry);
            batch.StatusReportsApplied++;
        }

        // 2) Posições, depois das situações do lote.
        foreach (var position in positions)
        {
            var id = position.ResourceId!.Value;
            var accepted = IsAgent(position)
                ? agents.TryGetValue(id, out var agent) && agent.IsActive && AgentActivity.IsInActivity(agent.Status)
                : equipment.TryGetValue(id, out var facts) && facts.IsActive && facts.Category == EquipmentCategory.Mobile;

            if (!accepted)
            {
                batch.Ignored++;
                continue;
            }

            batch.AddPosition(
                IsAgent(position) ? DomainResourceKind.Agent : DomainResourceKind.Equipment,
                id,
                position.Latitude!.Value,
                position.Longitude!.Value,
                position.RecordedAt!.Value);
        }

        // 3) Sinais de vida de fixos.
        foreach (var heartbeat in heartbeats)
        {
            var id = heartbeat.EquipmentId!.Value;
            if (!equipment.TryGetValue(id, out var facts) || !facts.IsActive || facts.Category != EquipmentCategory.Fixed)
            {
                batch.Ignored++;
                continue;
            }

            batch.AddHeartbeat(id, heartbeat.RecordedAt!.Value);
        }

        var advancedPositions = await ApplyToLiveStatesAsync(batch, cancellationToken);
        telemetry.AddReadings(batch.Readings);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await PublishAsync(batch, advancedPositions, equipment, cancellationToken);

        return new TelemetryBatchResult
        {
            ReceivedAt = receivedAt,
            StatusReportsApplied = batch.StatusReportsApplied,
            PositionsAccepted = batch.PositionsAccepted,
            HeartbeatsAccepted = batch.HeartbeatsAccepted,
            Ignored = batch.Ignored,
        };
    }

    private static bool IsAgent(TelemetryPositionItem position) => position.ResourceKind == Contracts.Operations.ResourceKind.Agent;

    /// <summary>Aplica as leituras aceitas aos estados atuais (D-71); devolve as posições que avançaram o estado.</summary>
    private async Task<List<TelemetryReading>> ApplyToLiveStatesAsync(Batch batch, CancellationToken cancellationToken)
    {
        var states = new Dictionary<(DomainResourceKind, Guid), ResourceLiveState>();
        foreach (var kind in batch.Readings.Select(reading => reading.ResourceKind).Distinct())
        {
            var ids = batch.Readings.Where(reading => reading.ResourceKind == kind).Select(reading => reading.ResourceId).ToHashSet();
            foreach (var state in await telemetry.FindLiveStatesAsync(kind, ids, cancellationToken))
            {
                states[(state.ResourceKind, state.ResourceId)] = state;
            }
        }

        var advanced = new List<TelemetryReading>();
        foreach (var reading in batch.Readings)
        {
            var key = (reading.ResourceKind, reading.ResourceId);
            var isPosition = reading.Kind == TelemetryReadingKind.Position;

            if (!states.TryGetValue(key, out var state))
            {
                state = isPosition
                    ? ResourceLiveState.FromPosition(
                        reading.ResourceKind, reading.ResourceId, reading.Latitude!.Value, reading.Longitude!.Value, reading.RecordedAt, batch.ReceivedAt)
                    : ResourceLiveState.FromHeartbeat(reading.ResourceKind, reading.ResourceId, reading.RecordedAt, batch.ReceivedAt);
                states[key] = state;
                telemetry.AddLiveState(state);

                if (isPosition)
                {
                    advanced.Add(reading);
                }

                continue;
            }

            if (isPosition)
            {
                if (state.ApplyPosition(reading.Latitude!.Value, reading.Longitude!.Value, reading.RecordedAt, batch.ReceivedAt))
                {
                    advanced.Add(reading);
                }
            }
            else
            {
                state.ApplyHeartbeat(reading.RecordedAt, batch.ReceivedAt);
            }
        }

        return advanced;
    }

    /// <summary>
    /// Mensagens depois do commit (plan 16.11). Só as posições que avançaram o estado vão à Central, para um marcador
    /// não voltar a uma posição antiga; uma posição atualizada mais de uma vez no lote vai só com a última.
    /// </summary>
    private async Task PublishAsync(
        Batch batch,
        List<TelemetryReading> advancedPositions,
        IReadOnlyDictionary<Guid, TelemetryEquipmentFacts> equipment,
        CancellationToken cancellationToken)
    {
        if (batch.StatusReportsApplied > 0)
        {
            await notifier.PublishOperationsChangedAsync(OperationsChangeKind.AgentStatus, batch.ReceivedAt, cancellationToken);
        }

        var latest = advancedPositions
            .GroupBy(reading => (reading.ResourceKind, reading.ResourceId))
            .Select(group => group.MaxBy(reading => reading.RecordedAt)!)
            .ToArray();

        bool IsCrew(TelemetryReading reading) =>
            reading.ResourceKind == DomainResourceKind.Equipment && equipment[reading.ResourceId].CrewAssignable;

        static EquipmentPositionUpdate ToEquipment(TelemetryReading reading) => new()
        {
            EquipmentId = reading.ResourceId,
            Latitude = reading.Latitude!.Value,
            Longitude = reading.Longitude!.Value,
            RecordedAt = reading.RecordedAt,
        };

        await notifier.PublishAsync(
            RealtimeAudience.Everyone,
            OperationsHubContract.Messages.TelemetryBatchProcessed,
            new TelemetryBatchProcessed
            {
                ReceivedAt = batch.ReceivedAt,
                EquipmentPositions = latest
                    .Where(reading => reading.ResourceKind == DomainResourceKind.Equipment && !IsCrew(reading))
                    .Select(ToEquipment)
                    .ToArray(),
            },
            cancellationToken);

        var agentPositions = latest
            .Where(reading => reading.ResourceKind == DomainResourceKind.Agent)
            .Select(reading => new AgentPositionUpdate
            {
                AgentId = reading.ResourceId,
                Latitude = reading.Latitude!.Value,
                Longitude = reading.Longitude!.Value,
                RecordedAt = reading.RecordedAt,
            })
            .ToArray();
        var crewPositions = latest.Where(IsCrew).Select(ToEquipment).ToArray();

        if (agentPositions.Length > 0 || crewPositions.Length > 0)
        {
            await notifier.PublishAsync(
                RealtimeAudience.WithPermission(Permissions.AgentPositionsView),
                OperationsHubContract.Messages.AgentPositionsUpdated,
                new AgentPositionsUpdated
                {
                    ReceivedAt = batch.ReceivedAt,
                    Positions = agentPositions,
                    CrewEquipmentPositions = crewPositions,
                },
                cancellationToken);
        }
    }

    /// <summary>Leituras aceitas e contagens do lote.</summary>
    private sealed class Batch(DateTimeOffset receivedAt, bool isSimulated, string author)
    {
        public DateTimeOffset ReceivedAt { get; } = receivedAt;

        public List<TelemetryReading> Readings { get; } = [];

        public int StatusReportsApplied { get; set; }

        public int PositionsAccepted { get; private set; }

        public int HeartbeatsAccepted { get; private set; }

        public int Ignored { get; set; }

        public void AddPosition(DomainResourceKind kind, Guid id, double latitude, double longitude, DateTimeOffset recordedAt)
        {
            Readings.Add(TelemetryReading.Position(kind, id, latitude, longitude, recordedAt, ReceivedAt, isSimulated, author));
            PositionsAccepted++;
        }

        public void AddHeartbeat(Guid id, DateTimeOffset recordedAt)
        {
            Readings.Add(TelemetryReading.Heartbeat(DomainResourceKind.Equipment, id, recordedAt, ReceivedAt, isSimulated, author));
            HeartbeatsAccepted++;
        }
    }
}
