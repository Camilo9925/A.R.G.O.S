using Argos.Application.Common.Exceptions;
using Argos.Application.Operations;
using Argos.Contracts.Inventory;
using Argos.Contracts.Operations;
using Argos.Contracts.Realtime;
using Argos.Domain.Access;
using Argos.Domain.Inventory.Types;
using EquipmentCategory = Argos.Domain.Inventory.Types.EquipmentCategory;
using Argos.Domain.Operations;
using Argos.UnitTests.Application.Fakes;
using Argos.UnitTests.Application.Inventory;
using Argos.UnitTests.Domain.Inventory;
using ContractAlertAction = Argos.Contracts.Operations.AlertAction;
using DomainAgentRole = Argos.Domain.Inventory.Agents.AgentRole;
using DomainAgentStatus = Argos.Domain.Inventory.Agents.AgentStatus;
using DomainAlertCause = Argos.Domain.Operations.Alerts.AlertCause;
using DomainAlertSeverity = Argos.Domain.Operations.Alerts.AlertSeverity;
using DomainAlertState = Argos.Domain.Operations.Alerts.AlertState;
using DomainEquipmentStatus = Argos.Domain.Inventory.Equipments.EquipmentStatus;
using DomainMaintenanceNeed = Argos.Domain.Inventory.Equipments.MaintenanceNeed;
using DomainResourceKind = Argos.Domain.Telemetry.ResourceKind;

namespace Argos.UnitTests.Application.Operations;

/// <summary>
/// Retrato, detalhes e alertas da Central (US-11 a US-15; RF-027 a RF-037; RN-13 a RN-16, RN-20, RN-21, RN-26, RN-27,
/// RN-36; A-38; D-59, D-79 a D-82; plan 16.6 a 16.8).
/// </summary>
public sealed class OperationsServicesTests
{
    private static readonly GeoPosition Installed = new(-22.21, -49.66);
    private static readonly GeoPosition Base = new(-22.25, -49.70);
    private static readonly GeoPosition Live = new(-22.30, -49.75);

    private readonly InventoryServices _services = new();
    private readonly FakeOperationsQueries _queries = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private OperationsSnapshotService Snapshot() => new(_queries, _services.EquipmentQueryService(), _services.Actor, _services.Time);

    private OperationsDetailsService Details() =>
        new(_queries, _services.EquipmentQueryService(), _services.AgentQueryService(), _services.Actor);

    private AlertService AlertService() =>
        new(_services.Alerts, _queries, _services.UnitOfWork, _services.Notifier, _services.Actor, _services.Time);

    private void ActAs(UserRole role) => _services.Actor.Role = role;

    // Retrato: perfis (RN-27, RN-26, A-38)

    [Fact]
    public async Task Snapshot_Manager_HasNoAgentsNoActionsAndCrewVehiclesAtTheBase_RN27()
    {
        ActAs(UserRole.Manager);
        var patrol = AddEquipment(EquipmentCategory.Mobile, crewAssignable: true, live: Live);
        var scooter = AddEquipment(EquipmentCategory.Mobile, live: Live);
        AddAgentRow(DomainAgentStatus.OnDuty);
        AddAlert(patrol.Id, DomainAlertSeverity.Critical, matchesFilter: true, live: Live, crewAssignable: true);

        var snapshot = await Snapshot().GetAsync(new OperationsSnapshotQuery(), Ct);

        Assert.False(snapshot.AgentPositionsVisible);
        Assert.Empty(snapshot.Agents);
        Assert.False(_queries.AgentsListed);
        var patrolMarker = snapshot.Equipment.Single(marker => marker.Id == patrol.Id);
        Assert.Equal((PositionSource.Base, Base.Latitude, (DateTimeOffset?)null),
            (patrolMarker.PositionSource, patrolMarker.Latitude, patrolMarker.PositionRecordedAt));
        Assert.Equal(PositionSource.Live, snapshot.Equipment.Single(marker => marker.Id == scooter.Id).PositionSource);
        var alert = Assert.Single(snapshot.Alerts);
        Assert.Empty(alert.AllowedActions);
        Assert.Equal((Base.Latitude, Base.Longitude), (alert.Latitude, alert.Longitude));
    }

    [Fact]
    public async Task Snapshot_Operator_SeesAgentsLiveCrewVehiclesAndAlertActions_RN27_RN20()
    {
        ActAs(UserRole.Operator);
        var patrol = AddEquipment(EquipmentCategory.Mobile, crewAssignable: true, live: Live);
        var agent = AddAgentRow(DomainAgentStatus.Responding);
        AddAlert(patrol.Id, DomainAlertSeverity.Critical, matchesFilter: true);

        var snapshot = await Snapshot().GetAsync(new OperationsSnapshotQuery(), Ct);

        Assert.True(snapshot.AgentPositionsVisible);
        var marker = Assert.Single(snapshot.Agents);
        Assert.Equal((agent.Id, EquipmentStatusSeverity.AgentInActivityRank), (marker.Id, marker.SeverityRank));
        Assert.Equal(PositionSource.Live, Assert.Single(snapshot.Equipment).PositionSource);
        Assert.Equal([ContractAlertAction.Acknowledge], Assert.Single(snapshot.Alerts).AllowedActions);
    }

    [Fact]
    public async Task Snapshot_AgentMarker_OnlyInActivityAndWithPositionOfTheCurrentPeriod_RN13_ArtVIII2()
    {
        ActAs(UserRole.Administrator);
        var current = AddAgentRow(DomainAgentStatus.OnDuty);
        AddAgentRow(DomainAgentStatus.OnBreak);
        AddAgentRow(DomainAgentStatus.OnDuty, positionBeforeStatusChange: true);

        var snapshot = await Snapshot().GetAsync(new OperationsSnapshotQuery(), Ct);

        Assert.Equal([current.Id], snapshot.Agents.Select(agent => agent.Id));
    }

    [Fact]
    public async Task Snapshot_FixedEquipment_IsAtTheInstalledLocation_WithTheSeverityRank_D82()
    {
        AddEquipment(EquipmentCategory.Fixed, status: DomainEquipmentStatus.Stolen);
        AddEquipment(EquipmentCategory.Mobile, status: DomainEquipmentStatus.Malfunction);

        var snapshot = await Snapshot().GetAsync(new OperationsSnapshotQuery(), Ct);

        Assert.Equal(
            [(PositionSource.Installed, Installed.Latitude, 1), (PositionSource.Base, Base.Latitude, 2)],
            snapshot.Equipment.Select(marker => (marker.PositionSource, marker.Latitude, marker.SeverityRank)));
    }

    // Retrato: indicadores (RN-14 a RN-16)

    [Fact]
    public async Task Snapshot_IndicatorsOverTheSameRows_RN14_RN15_RN16()
    {
        foreach (var status in new[]
        {
            DomainEquipmentStatus.Operational, DomainEquipmentStatus.Operational, DomainEquipmentStatus.Operational,
            DomainEquipmentStatus.Malfunction, DomainEquipmentStatus.Stolen, DomainEquipmentStatus.NotInstalled,
        })
        {
            AddEquipment(EquipmentCategory.Fixed, status: status);
        }

        _queries.RegionCounts.AddRange([new RegionAgentCountRow(Guid.NewGuid(), "Centro", 4), new RegionAgentCountRow(Guid.NewGuid(), "Zona Sul", 0)]);
        AddAlert(Guid.NewGuid(), DomainAlertSeverity.High, matchesFilter: true);
        AddAlert(Guid.NewGuid(), DomainAlertSeverity.Critical, matchesFilter: false);

        var indicators = (await Snapshot().GetAsync(new OperationsSnapshotQuery(), Ct)).Indicators;

        Assert.Equal((6, 3, 2, (decimal?)60.0m), (indicators.TotalEquipment, indicators.Operational, indicators.Unavailable, indicators.AvailabilityPercent));
        Assert.Equal(6, indicators.CountsByStatus.Count);
        Assert.Equal(1, indicators.CountsByStatus.Single(count => count.Status == Argos.Contracts.Inventory.EquipmentStatus.NotInstalled).Count);
        Assert.Equal(4, indicators.AgentsInActivity);
        Assert.Equal(["Centro", "Zona Sul"], indicators.AgentsInActivityByRegion.Select(region => region.RegionName));
        Assert.Equal(
            [(Argos.Contracts.Operations.AlertSeverity.Critical, 0), (Argos.Contracts.Operations.AlertSeverity.High, 1),
                (Argos.Contracts.Operations.AlertSeverity.Medium, 0), (Argos.Contracts.Operations.AlertSeverity.Low, 0)],
            indicators.OpenAlertsBySeverity.Select(count => (count.Severity, count.Count)));
    }

    [Fact]
    public async Task Snapshot_NoEquipment_AvailabilityIsNull_RN14()
    {
        var indicators = (await Snapshot().GetAsync(new OperationsSnapshotQuery(), Ct)).Indicators;

        Assert.Null(indicators.AvailabilityPercent);
        Assert.Equal(0, indicators.TotalEquipment);
    }

    // Retrato: filtros e alertas (RN-21, RN-36)

    [Fact]
    public async Task Snapshot_AlertsOutsideTheFilter_OnlyCriticalWithOutsideFilter_InOrder_RN21()
    {
        var inside = AddAlert(Guid.NewGuid(), DomainAlertSeverity.High, matchesFilter: true);
        var outsideCritical = AddAlert(Guid.NewGuid(), DomainAlertSeverity.Critical, matchesFilter: false);
        AddAlert(Guid.NewGuid(), DomainAlertSeverity.High, matchesFilter: false);

        var alerts = (await Snapshot().GetAsync(new OperationsSnapshotQuery(), Ct)).Alerts;

        Assert.Equal([(outsideCritical.Id, true), (inside.Id, false)], alerts.Select(alert => (alert.Id, alert.OutsideFilter)));
    }

    [Fact]
    public async Task Snapshot_OnlyEquipmentFilters_LeavesAgentsOutOfTheView_RN36()
    {
        AddAgentRow(DomainAgentStatus.OnDuty);

        var snapshot = await Snapshot().GetAsync(
            new OperationsSnapshotQuery { Status = [Argos.Contracts.Inventory.EquipmentStatus.Malfunction] }, Ct);

        Assert.Empty(snapshot.Agents);
        Assert.False(_queries.AgentsListed);
        Assert.Equal([DomainEquipmentStatus.Malfunction], _queries.LastFilter!.Statuses);
    }

    [Fact]
    public async Task Snapshot_OnlyAgentFilters_LeavesEquipmentOutAndNoAlertMatches_RN36_RN21()
    {
        AddEquipment(EquipmentCategory.Fixed);
        AddAlert(Guid.NewGuid(), DomainAlertSeverity.High, matchesFilter: true);
        var critical = AddAlert(Guid.NewGuid(), DomainAlertSeverity.Critical, matchesFilter: true);

        var snapshot = await Snapshot().GetAsync(new OperationsSnapshotQuery { AgentRole = [AgentRole.MilitaryPolice] }, Ct);

        Assert.Empty(snapshot.Equipment);
        Assert.False(_queries.EquipmentListed);
        Assert.Equal([(critical.Id, true)], snapshot.Alerts.Select(alert => (alert.Id, alert.OutsideFilter)));
        Assert.Equal([DomainAgentRole.MilitaryPolice], _queries.LastFilter!.AgentRoles);
    }

    // Detalhes (US-14; RN-27, A-38)

    [Fact]
    public async Task EquipmentDetails_Manager_CrewVehicleWithoutCurrentPosition_A38()
    {
        var created = await CreatePatrolAsync();
        _queries.EquipmentLive[created.Id] = new EquipmentLiveRow(true, new LiveStateRow(Live, InventoryTestData.Now, InventoryTestData.Now));
        ActAs(UserRole.Manager);

        var details = await Details().GetEquipmentAsync(created.Id, Ct);

        Assert.Null(details.CurrentPosition);
        Assert.Equal(InventoryTestData.Now, details.LastCommunicationAt);
        Assert.Empty(details.Equipment.AllowedStatusChanges);
    }

    [Fact]
    public async Task EquipmentDetails_Operator_SeesCurrentPositionAndOpenAlerts_US14()
    {
        var created = await CreatePatrolAsync();
        _queries.EquipmentLive[created.Id] = new EquipmentLiveRow(true, new LiveStateRow(Live, InventoryTestData.Now, InventoryTestData.Now));
        AddAlert(created.Id, DomainAlertSeverity.High, matchesFilter: true);
        ActAs(UserRole.Operator);

        var details = await Details().GetEquipmentAsync(created.Id, Ct);

        Assert.Equal((Live.Latitude, InventoryTestData.Now), (details.CurrentPosition!.Latitude, details.CurrentPosition.RecordedAt));
        Assert.Equal([ContractAlertAction.Acknowledge, ContractAlertAction.Resolve], Assert.Single(details.OpenAlerts).AllowedActions);
    }

    [Fact]
    public async Task EquipmentDetails_Unknown_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Details().GetEquipmentAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task AgentDetails_Manager_IsForbidden_RN27()
    {
        ActAs(UserRole.Manager);

        var exception = await Assert.ThrowsAsync<ForbiddenOperationException>(() => Details().GetAgentAsync(Guid.NewGuid(), Ct));

        Assert.Equal("Seu perfil não pode ver a posição de agentes.", exception.Message);
    }

    [Fact]
    public async Task AgentDetails_Operator_PositionOnlyInTheCurrentActivity_US14_ArtVIII2()
    {
        var agent = await _services.AgentService().CreateAsync(new CreateAgentRequest
        {
            FullName = "Agente Fictício de Teste",
            DisplayName = "Carcará",
            RegistrationNumber = "SMM-0001",
            Role = AgentRole.TrafficAgent,
            OrganizationId = _services.Organization.Id,
            BaseId = _services.Base.Id,
        }, Ct);
        _queries.AgentLive[agent.Id] = new LiveStateRow(Live, InventoryTestData.Now.AddMinutes(5), InventoryTestData.Now.AddMinutes(5));
        ActAs(UserRole.Operator);

        var offDuty = await Details().GetAgentAsync(agent.Id, Ct);
        await _services.AgentStatusService().ChangeAsync(agent.Id, new ChangeAgentStatusRequest { Status = AgentStatus.OnDuty }, Ct);
        var onDuty = await Details().GetAgentAsync(agent.Id, Ct);

        Assert.Null(offDuty.Position);
        Assert.Equal((("Carcará", "SMM"), Live.Latitude), ((onDuty.DisplayName, onDuty.OrganizationAcronym), onDuty.Position!.Latitude));
        Assert.Equal(OperationsChangeKind.AgentStatus, ((OperationsChanged)_services.Notifier.Messages.Last().Message).Kind);
    }

    // Alertas: reconhecer e resolver (RN-20)

    [Fact]
    public async Task Acknowledge_RecordsTheActor_PublishesAndReturnsTheSummary_RN20()
    {
        var alert = AddStoredAlert(DomainAlertSeverity.Critical);
        ActAs(UserRole.Operator);
        _services.Actor.Name = "operador";

        var summary = await AlertService().AcknowledgeAsync(alert.Id, Ct);

        Assert.Equal((DomainAlertState.Acknowledged, "operador"), (alert.State, alert.AcknowledgedBy));
        Assert.Equal(1, _services.UnitOfWork.SaveCalls);
        var message = Assert.Single(_services.Notifier.Messages);
        Assert.Equal((OperationsHubContract.Messages.OperationsChanged, 1), (message.Name, message.SavesBefore));
        Assert.Equal(alert.Id, summary.Id);
        Assert.Equal("Equipamento furtado", summary.Title);
    }

    [Fact]
    public async Task Resolve_CriticalNotAcknowledged_IsRefusedAndNothingIsSaved_RN20()
    {
        var alert = AddStoredAlert(DomainAlertSeverity.Critical);
        ActAs(UserRole.Operator);

        var exception = await Assert.ThrowsAsync<OperationsRuleException>(
            () => AlertService().ResolveAsync(alert.Id, new ResolveAlertRequest { Notes = "Recuperada." }, Ct));

        Assert.Equal("Alertas críticos precisam ser reconhecidos antes de serem resolvidos.", exception.Message);
        Assert.Equal(0, _services.UnitOfWork.SaveCalls);
        Assert.Empty(_services.Notifier.Messages);
    }

    [Fact]
    public async Task HandleAlert_Manager_IsForbidden_And_Unknown_IsNotFound_RN26()
    {
        var alert = AddStoredAlert(DomainAlertSeverity.High);
        ActAs(UserRole.Manager);
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => AlertService().AcknowledgeAsync(alert.Id, Ct));

        ActAs(UserRole.Operator);
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => AlertService().AcknowledgeAsync(Guid.NewGuid(), Ct));
        Assert.Equal("Alerta não encontrado.", exception.Message);
    }

    private EquipmentMarkerRow AddEquipment(
        EquipmentCategory category,
        bool crewAssignable = false,
        GeoPosition? live = null,
        DomainEquipmentStatus status = DomainEquipmentStatus.Operational)
    {
        var row = new EquipmentMarkerRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            category == EquipmentCategory.Fixed ? "Radar de trânsito" : "Viatura",
            "radar",
            category,
            crewAssignable,
            $"SN-{_queries.Equipment.Count}",
            status,
            DomainMaintenanceNeed.None,
            null,
            category == EquipmentCategory.Fixed ? Installed : Base,
            live,
            live is null ? null : InventoryTestData.Now,
            "SMM",
            "Jardim das Musas",
            "Centro");
        _queries.Equipment.Add(row);
        return row;
    }

    private AgentMarkerRow AddAgentRow(DomainAgentStatus status, bool positionBeforeStatusChange = false)
    {
        var statusChangedAt = InventoryTestData.Now;
        var row = new AgentMarkerRow(
            Guid.NewGuid(),
            "Carcará",
            DomainAgentRole.TrafficAgent,
            "SMM",
            status,
            statusChangedAt,
            Live,
            positionBeforeStatusChange ? statusChangedAt.AddMinutes(-1) : statusChangedAt.AddSeconds(5),
            null);
        _queries.Agents.Add(row);
        return row;
    }

    private AlertRow AddAlert(
        Guid resourceId, DomainAlertSeverity severity, bool matchesFilter, GeoPosition? live = null, bool crewAssignable = false)
    {
        var row = new AlertRow(
            Guid.NewGuid(),
            severity == DomainAlertSeverity.Critical ? DomainAlertCause.EquipmentStolen : DomainAlertCause.EquipmentMalfunction,
            severity,
            DomainAlertState.Open,
            DomainResourceKind.Equipment,
            resourceId,
            "Viatura",
            "PM-0001",
            crewAssignable,
            ResourceIsActive: true,
            "Jardim das Musas",
            "Centro",
            Base,
            live,
            InventoryTestData.Now,
            InventoryTestData.Now.AddMinutes(_queries.Alerts.Count),
            1,
            null,
            null,
            matchesFilter);
        _queries.Alerts.Add(row);
        return row;
    }

    private Argos.Domain.Operations.Alerts.Alert AddStoredAlert(DomainAlertSeverity severity)
    {
        var definition = severity == DomainAlertSeverity.Critical
            ? new Argos.Domain.Operations.Alerts.AlertDefinition(DomainAlertCause.EquipmentStolen, severity)
            : new Argos.Domain.Operations.Alerts.AlertDefinition(DomainAlertCause.EquipmentMalfunction, severity);
        var alert = Argos.Domain.Operations.Alerts.Alert.Open(
            definition, DomainResourceKind.Equipment, Guid.NewGuid(), InventoryTestData.Now, "admin");
        _services.Alerts.Add(alert);
        _queries.Alerts.Add(new AlertRow(
            alert.Id, alert.Cause, alert.Severity, alert.State, alert.ResourceKind, alert.ResourceId, "Câmera", "CAM-1", false, true,
            "Jardim das Musas", "Centro", Installed, null, alert.OpenedAt, alert.LastOccurredAt, 1, null, null, true));
        return alert;
    }

    private Task<EquipmentDetails> CreatePatrolAsync() => _services.EquipmentService().CreateAsync(new CreateEquipmentRequest
    {
        TypeId = _services.Type(EquipmentTypeCatalog.PatrolVehicle).Id,
        SerialNumber = "VT-0001",
        Brand = "Orbis",
        Model = "Patrulha 1",
        OrganizationId = _services.Organization.Id,
        BaseId = _services.Base.Id,
        Attributes = EquipmentTestData.PatrolAttributes().ToDictionary(pair => pair.Key, pair => pair.Value?.ToString()),
        Status = Argos.Contracts.Inventory.EquipmentStatus.Operational,
    }, Ct);
}
