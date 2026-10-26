using Argos.Application.Common.Exceptions;
using Argos.Application.Telemetry;
using Argos.Contracts.Access;
using Argos.Contracts.Operations;
using Argos.Contracts.Realtime;
using Argos.Contracts.Telemetry;
using Argos.Domain.Inventory.Agents;
using Argos.Domain.Inventory.Types;
using Argos.Domain.Telemetry;
using Argos.UnitTests.Application.Fakes;
using Argos.UnitTests.Application.Inventory;
using Argos.UnitTests.Domain.Inventory;
using ContractAgentStatus = Argos.Contracts.Inventory.AgentStatus;
using ContractResourceKind = Argos.Contracts.Operations.ResourceKind;
using DomainResourceKind = Argos.Domain.Telemetry.ResourceKind;

namespace Argos.UnitTests.Application.Telemetry;

/// <summary>
/// Descoberta e ingestão da telemetria (RF-023, RF-024; RN-12, RN-13, RN-27; Art. VIII.2; D-71, D-72, D-80, D-94;
/// plan 16.4, 16.11).
/// </summary>
public sealed class TelemetryServicesTests
{
    private static readonly DateTimeOffset Received = InventoryTestData.Now;
    private static readonly DateTimeOffset Cycle = Received.AddSeconds(-1);

    private readonly InventoryServices _services = new();
    private readonly FakeTelemetry _telemetry;

    public TelemetryServicesTests()
    {
        _telemetry = new FakeTelemetry(_services.Inventory);
        _services.Actor.Name = "PROMETHEUS";
        _services.Actor.Role = null;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private TelemetryIngestionService Ingestion() =>
        new(_telemetry, _telemetry, _services.UnitOfWork, _services.Notifier, _services.Actor, _services.Time);

    // Ingestão

    [Fact]
    public async Task Ingest_AppliesStatusBeforePositions_SoTheAgentEnteringTheShiftHasItsPosition_RN12_RN13()
    {
        var agent = AddAgent();

        var result = await Ingestion().IngestAsync(Batch(
            reports: [Report(agent.Id, ContractAgentStatus.OnDuty)],
            positions: [Position(ContractResourceKind.Agent, agent.Id)]), Ct);

        Assert.Equal((1, 1, 0, 0), (result.StatusReportsApplied, result.PositionsAccepted, result.HeartbeatsAccepted, result.Ignored));
        var change = agent.StatusChanges.Last();
        Assert.Equal((AgentStatus.OnDuty, AgentStatusChangeOrigin.Telemetry, "PROMETHEUS", Cycle),
            (agent.Status, change.Origin, change.Author, change.OccurredAt));
        var reading = Assert.Single(_telemetry.Readings);
        Assert.Equal((DomainResourceKind.Agent, agent.Id, TelemetryReadingKind.Position, "PROMETHEUS", true, Received),
            (reading.ResourceKind, reading.ResourceId, reading.Kind, reading.Author, reading.IsSimulated, reading.ReceivedAt));
        var state = Assert.Single(_telemetry.LiveStates);
        Assert.Equal((-22.21, Cycle), (state.Latitude!.Value, state.PositionRecordedAt!.Value));
        Assert.Equal(1, _services.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Ingest_PositionOfAgentOutOfActivity_IsDiscardedWithoutReading_ArtVIII2()
    {
        var offDuty = AddAgent("GCM-0001");
        var onBreak = AddAgent("GCM-0002", AgentStatus.OnBreak);
        var leaving = AddAgent("GCM-0003", AgentStatus.OnDuty);

        var result = await Ingestion().IngestAsync(Batch(
            reports: [Report(leaving.Id, ContractAgentStatus.OffDuty)],
            positions:
            [
                Position(ContractResourceKind.Agent, offDuty.Id),
                Position(ContractResourceKind.Agent, onBreak.Id),
                Position(ContractResourceKind.Agent, leaving.Id),
            ]), Ct);

        Assert.Equal((1, 0, 3), (result.StatusReportsApplied, result.PositionsAccepted, result.Ignored));
        Assert.Empty(_telemetry.Readings);
        Assert.Empty(_telemetry.LiveStates);
        Assert.DoesNotContain(OperationsHubContract.Messages.AgentPositionsUpdated, _services.Notifier.Names);
    }

    [Fact]
    public async Task Ingest_UnknownInactiveWrongCategoryAndSameStatus_AreIgnoredAndCounted_D71()
    {
        var agent = AddAgent(status: AgentStatus.OnDuty);
        var inactive = AddAgent("GCM-0009");
        inactive.Inactivate(null, InventoryTestData.Now, "admin");
        var fixedId = AddEquipment(EquipmentCategory.Fixed);
        var inactiveMobile = AddEquipment(EquipmentCategory.Mobile, isActive: false);

        var result = await Ingestion().IngestAsync(Batch(
            reports:
            [
                Report(agent.Id, ContractAgentStatus.OnDuty),
                Report(inactive.Id, ContractAgentStatus.OnDuty),
                Report(Guid.NewGuid(), ContractAgentStatus.OnDuty),
            ],
            positions:
            [
                Position(ContractResourceKind.Equipment, fixedId),
                Position(ContractResourceKind.Equipment, inactiveMobile),
                Position(ContractResourceKind.Equipment, Guid.NewGuid()),
            ],
            heartbeats: [Heartbeat(inactiveMobile), Heartbeat(Guid.NewGuid())]), Ct);

        Assert.Equal((0, 0, 0, 8), (result.StatusReportsApplied, result.PositionsAccepted, result.HeartbeatsAccepted, result.Ignored));
        Assert.Empty(_telemetry.Readings);
    }

    [Fact]
    public async Task Ingest_FixedHeartbeat_RecordsReadingAndLastCommunication_RF024()
    {
        var fixedId = AddEquipment(EquipmentCategory.Fixed);

        var result = await Ingestion().IngestAsync(Batch(heartbeats: [Heartbeat(fixedId)]), Ct);

        Assert.Equal(1, result.HeartbeatsAccepted);
        Assert.Equal(TelemetryReadingKind.Heartbeat, Assert.Single(_telemetry.Readings).Kind);
        var state = Assert.Single(_telemetry.LiveStates);
        Assert.Equal((Cycle, (DateTimeOffset?)null), (state.LastCommunicationAt, state.PositionRecordedAt));
    }

    [Fact]
    public async Task Ingest_OlderPosition_GoesToTheReadingsButNotToTheStateNorToTheHub_D71()
    {
        var mobile = AddEquipment(EquipmentCategory.Mobile);
        _telemetry.LiveStates.Add(ResourceLiveState.FromPosition(DomainResourceKind.Equipment, mobile, -22.0, -49.0, Received, Received));

        var result = await Ingestion().IngestAsync(Batch(positions: [Position(ContractResourceKind.Equipment, mobile)]), Ct);

        Assert.Equal(1, result.PositionsAccepted);
        Assert.Single(_telemetry.Readings);
        Assert.Equal(-22.0, _telemetry.LiveStates.Single().Latitude);
        Assert.Empty(_services.Notifier.Single<TelemetryBatchProcessed>(OperationsHubContract.Messages.TelemetryBatchProcessed).EquipmentPositions);
    }

    [Fact]
    public async Task Ingest_PublishesAfterTheCommit_InOrder_WithTheAudienceOfEachMessage_RN27()
    {
        var scooter = AddEquipment(EquipmentCategory.Mobile);
        var patrol = AddEquipment(EquipmentCategory.Mobile, crewAssignable: true);
        var agent = AddAgent();

        await Ingestion().IngestAsync(Batch(
            reports: [Report(agent.Id, ContractAgentStatus.OnDuty)],
            positions:
            [
                Position(ContractResourceKind.Equipment, scooter),
                Position(ContractResourceKind.Equipment, patrol),
                Position(ContractResourceKind.Agent, agent.Id),
            ]), Ct);

        Assert.Equal(
            [
                (null, OperationsHubContract.Messages.OperationsChanged, 1),
                (null, OperationsHubContract.Messages.TelemetryBatchProcessed, 1),
                (Permissions.AgentPositionsView, OperationsHubContract.Messages.AgentPositionsUpdated, 1),
            ],
            _services.Notifier.Messages.Select(message => (message.Permission, message.Name, message.SavesBefore)));
        Assert.Equal(OperationsChangeKind.AgentStatus,
            _services.Notifier.Single<OperationsChanged>(OperationsHubContract.Messages.OperationsChanged).Kind);

        // A-38: viatura e moto só na mensagem de quem vê agentes.
        var everyone = _services.Notifier.Single<TelemetryBatchProcessed>(OperationsHubContract.Messages.TelemetryBatchProcessed);
        Assert.Equal([scooter], everyone.EquipmentPositions.Select(position => position.EquipmentId));
        Assert.Equal(Received, everyone.ReceivedAt);

        var restricted = _services.Notifier.Single<AgentPositionsUpdated>(OperationsHubContract.Messages.AgentPositionsUpdated);
        Assert.Equal([agent.Id], restricted.Positions.Select(position => position.AgentId));
        Assert.Equal([patrol], restricted.CrewEquipmentPositions.Select(position => position.EquipmentId));
    }

    [Fact]
    public async Task Ingest_WithoutPositions_PublishesOnlyTheBatchProcessed_D80()
    {
        var result = await Ingestion().IngestAsync(Batch(), Ct);

        Assert.Equal(0, result.Ignored);
        Assert.Equal([OperationsHubContract.Messages.TelemetryBatchProcessed], _services.Notifier.Names);
    }

    [Fact]
    public async Task Ingest_FailedSave_PublishesNothing_D80()
    {
        var agent = AddAgent();
        _services.UnitOfWork.Failure = new ConflictException("Conflito de concorrência.");

        await Assert.ThrowsAsync<ConflictException>(
            () => Ingestion().IngestAsync(Batch(reports: [Report(agent.Id, ContractAgentStatus.OnDuty)]), Ct));

        Assert.Empty(_services.Notifier.Messages);
    }

    [Fact]
    public async Task Ingest_InvalidFormat_ListsEveryFieldWithItsMessage_D71()
    {
        var request = new TelemetryBatchRequest
        {
            Positions =
            [
                new TelemetryPositionItem
                {
                    ResourceKind = ContractResourceKind.Equipment, ResourceId = Guid.NewGuid(), Latitude = 95, Longitude = -49.6,
                },
            ],
            AgentStatusReports = [Report(Guid.NewGuid(), ContractAgentStatus.OnDuty) with { ReportedAt = Received.AddSeconds(1) }],
        };

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => Ingestion().IngestAsync(request, Ct));

        Assert.Equal(["A latitude deve estar entre -90 e 90."], exception.Errors["positions[0].latitude"]);
        Assert.Equal(["Informe o instante da leitura."], exception.Errors["positions[0].recordedAt"]);
        Assert.Equal(["O instante informado não pode estar no futuro."], exception.Errors["agentStatusReports[0].reportedAt"]);
        Assert.Equal(["Informe o instante de envio."], exception.Errors["sentAt"]);
        Assert.Equal(["Informe se o lote é simulado."], exception.Errors["isSimulated"]);
        Assert.Equal(5, exception.Errors.Count);
        Assert.Equal(0, _services.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Ingest_MoreThan1000ItemsInAList_IsRefused_D71()
    {
        var request = Batch(heartbeats: Enumerable.Range(0, 1001).Select(_ => Heartbeat(Guid.NewGuid())).ToList());

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => Ingestion().IngestAsync(request, Ct));

        Assert.Equal(["O lote pode ter no máximo 1.000 itens em cada lista."], exception.Errors["heartbeats"]);
    }

    // Descoberta

    [Theory]
    [InlineData(12, DutyShift.Morning, true)] // 09h em Prometeia (UTC−3)
    [InlineData(12, DutyShift.Afternoon, false)]
    [InlineData(1, DutyShift.Night, true)] // 22h
    [InlineData(9, DutyShift.Night, false)] // 06h
    [InlineData(9, null, false)]
    public async Task Resources_ScheduledOnDutyByTheShiftInTheMunicipalityTimeZone_D94(int utcHour, DutyShift? shift, bool expected)
    {
        _services.Time.Now = new DateTimeOffset(2026, 10, 26, utcHour, 0, 0, TimeSpan.Zero);
        _telemetry.Resources = new TelemetryResourceList
        {
            Agents = [new TelemetryAgentResource { Id = Guid.NewGuid(), Key = "GCM/0001", Shift = shift }],
        };

        var resources = await new TelemetryResourceService(_telemetry, _services.Calendar(), _services.Time).GetResourcesAsync(Ct);

        Assert.Equal(expected, Assert.Single(resources.Agents).ScheduledOnDuty);
    }

    private Agent AddAgent(string registration = "GCM-0042", AgentStatus status = AgentStatus.OffDuty)
    {
        var agent = AgentTestData.NewAgent(registration);
        if (status != agent.Status)
        {
            agent.ChangeStatus(status, InventoryTestData.Now.AddHours(-2), "admin");
        }

        _services.Inventory.Agents.Add(agent);
        return agent;
    }

    private Guid AddEquipment(EquipmentCategory category, bool isActive = true, bool crewAssignable = false)
    {
        var id = Guid.NewGuid();
        _telemetry.EquipmentFacts[id] = new TelemetryEquipmentFacts(id, isActive, category, crewAssignable);
        return id;
    }

    private static TelemetryBatchRequest Batch(
        List<AgentStatusReportItem>? reports = null,
        List<TelemetryPositionItem>? positions = null,
        List<TelemetryHeartbeatItem>? heartbeats = null) => new()
    {
        SentAt = Cycle,
        IsSimulated = true,
        AgentStatusReports = reports ?? [],
        Positions = positions ?? [],
        Heartbeats = heartbeats ?? [],
    };

    private static AgentStatusReportItem Report(Guid agentId, ContractAgentStatus status) =>
        new() { AgentId = agentId, Status = status, ReportedAt = Cycle };

    private static TelemetryPositionItem Position(ContractResourceKind kind, Guid id) =>
        new() { ResourceKind = kind, ResourceId = id, Latitude = -22.21, Longitude = -49.66, RecordedAt = Cycle };

    private static TelemetryHeartbeatItem Heartbeat(Guid equipmentId) => new() { EquipmentId = equipmentId, RecordedAt = Cycle };
}
