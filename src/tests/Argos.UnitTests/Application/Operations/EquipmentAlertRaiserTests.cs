using Argos.Application.Common.Exceptions;
using Argos.Contracts.Inventory;
using Argos.Contracts.Operations;
using Argos.Contracts.Realtime;
using Argos.Domain.Inventory.Types;
using Argos.Domain.Operations.Alerts;
using Argos.UnitTests.Application.Inventory;
using Argos.UnitTests.Domain.Inventory;
using ContractStatus = Argos.Contracts.Inventory.EquipmentStatus;
using DomainAlertCause = Argos.Domain.Operations.Alerts.AlertCause;
using DomainAlertSeverity = Argos.Domain.Operations.Alerts.AlertSeverity;
using DomainAlertState = Argos.Domain.Operations.Alerts.AlertState;

namespace Argos.UnitTests.Application.Operations;

/// <summary>
/// Alertas abertos pelos casos de uso do Inventário e avisos à Central (US-15, RF-035; RN-11, RN-18, RN-19; D-66, D-77,
/// D-80; plan 16.6, 16.8).
/// </summary>
public sealed class EquipmentAlertRaiserTests
{
    private readonly InventoryServices _services = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ManualChange_ToStolen_OpensCriticalAlertInTheSameSave_RN18()
    {
        var created = await CreateRadarAsync();
        _services.Actor.Name = "operador";
        _services.Time.Now = InventoryTestData.Now.AddHours(1);

        await ChangeAsync(created.Id, ContractStatus.Stolen, "Furtada durante a madrugada.");

        var alert = Assert.Single(_services.Alerts.Alerts);
        Assert.Equal(
            (DomainAlertCause.EquipmentStolen, DomainAlertSeverity.Critical, DomainAlertState.Open, created.Id, "operador", InventoryTestData.Now.AddHours(1)),
            (alert.Cause, alert.Severity, alert.State, alert.ResourceId, alert.OpenedBy, alert.OpenedAt));
        Assert.Equal(2, _services.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task ManualChange_ToOtherStatuses_OpensNothing_RN18()
    {
        var created = await CreateRadarAsync();

        await ChangeAsync(created.Id, ContractStatus.UnderMaintenance);
        await ChangeAsync(created.Id, ContractStatus.Operational);

        Assert.Empty(_services.Alerts.Alerts);
    }

    [Fact]
    public async Task Malfunction_Maintenance_Malfunction_KeepsOneAlertWithTwoOccurrences_RN19()
    {
        var created = await CreateRadarAsync();

        await ChangeAsync(created.Id, ContractStatus.Malfunction);
        _services.Time.Now = InventoryTestData.Now.AddHours(2);
        await ChangeAsync(created.Id, ContractStatus.UnderMaintenance);
        _services.Time.Now = InventoryTestData.Now.AddHours(3);
        await ChangeAsync(created.Id, ContractStatus.Malfunction);

        var alert = Assert.Single(_services.Alerts.Alerts);
        Assert.Equal((DomainAlertCause.EquipmentMalfunction, 2, InventoryTestData.Now.AddHours(3)),
            (alert.Cause, alert.OccurrenceCount, alert.LastOccurredAt));
    }

    [Fact]
    public async Task Malfunction_AfterTheAlertWasResolved_OpensAnother_RN19()
    {
        var created = await CreateRadarAsync();
        await ChangeAsync(created.Id, ContractStatus.Malfunction);
        _services.Alerts.Alerts.Single().Resolve("Reparado.", InventoryTestData.Now, "operador");
        await ChangeAsync(created.Id, ContractStatus.Operational);

        await ChangeAsync(created.Id, ContractStatus.Malfunction);

        Assert.Equal(2, _services.Alerts.Alerts.Count);
        Assert.Single(_services.Alerts.Alerts, alert => alert.IsOpen);
    }

    [Fact]
    public async Task Create_InMalfunction_OpensHighAlert_D66()
    {
        var created = await _services.EquipmentService().CreateAsync(RadarRequest() with { Status = ContractStatus.Malfunction }, Ct);

        var alert = Assert.Single(_services.Alerts.Alerts);
        Assert.Equal((DomainAlertCause.EquipmentMalfunction, DomainAlertSeverity.High, created.Id, "admin"),
            (alert.Cause, alert.Severity, alert.ResourceId, alert.OpenedBy));
    }

    [Fact]
    public async Task Create_Operational_OpensNothing_RN18()
    {
        await CreateRadarAsync();

        Assert.Empty(_services.Alerts.Alerts);
    }

    [Fact]
    public async Task CompleteMaintenance_WithoutSuccess_OpensHighAlert_RN11()
    {
        var created = await CreateRadarAsync();
        await ChangeAsync(created.Id, ContractStatus.UnderMaintenance);
        var maintenance = await _services.MaintenanceService().RegisterAsync(created.Id, new RegisterMaintenanceRequest
        {
            Kind = Argos.Contracts.Inventory.MaintenanceKind.Immediate,
        }, Ct);
        _services.Time.Now = InventoryTestData.Now.AddHours(1);

        var completion = await _services.MaintenanceService().CompleteAsync(created.Id, maintenance.Id, new CompleteMaintenanceRequest
        {
            Result = MaintenanceResult.Failure,
            CompletedAt = InventoryTestData.Now.AddMinutes(30),
        }, Ct);

        Assert.Equal(ContractStatus.Malfunction, completion.Status);
        var alert = Assert.Single(_services.Alerts.Alerts);
        Assert.Equal((DomainAlertCause.EquipmentMalfunction, InventoryTestData.Now.AddHours(1)), (alert.Cause, alert.OpenedAt));
        Assert.Equal(OperationsChangeKind.EquipmentStatus, LastChange().Kind);
    }

    [Fact]
    public async Task CompleteMaintenance_WithSuccess_OpensNothingAndDoesNotResolve_RN11()
    {
        var created = await _services.EquipmentService().CreateAsync(RadarRequest() with { Status = ContractStatus.Malfunction }, Ct);
        var immediate = Assert.Single(created.OpenMaintenances);

        await _services.MaintenanceService().CompleteAsync(created.Id, immediate.Id, new CompleteMaintenanceRequest
        {
            Result = MaintenanceResult.Success,
            CompletedAt = InventoryTestData.Now,
        }, Ct);

        var alert = Assert.Single(_services.Alerts.Alerts);
        Assert.Equal((1, DomainAlertState.Open), (alert.OccurrenceCount, alert.State));
    }

    // Avisos à Central (D-80)

    [Fact]
    public async Task StatusChange_PublishesOperationsChangedAfterTheCommit_D80()
    {
        var created = await CreateRadarAsync();
        _services.Notifier.Messages.Clear();
        _services.Time.Now = InventoryTestData.Now.AddHours(1);

        await ChangeAsync(created.Id, ContractStatus.Malfunction);

        var message = Assert.Single(_services.Notifier.Messages);
        Assert.Equal((null, OperationsHubContract.Messages.OperationsChanged, 2), (message.Permission, message.Name, message.SavesBefore));
        Assert.Equal(new OperationsChanged { Kind = OperationsChangeKind.EquipmentStatus, OccurredAt = InventoryTestData.Now.AddHours(1) },
            message.Message);
    }

    [Fact]
    public async Task StatusChange_FailedSave_PublishesNothing_D80()
    {
        var created = await CreateRadarAsync();
        _services.Notifier.Messages.Clear();
        _services.UnitOfWork.Failure = new ConflictException("Conflito de concorrência.");

        await Assert.ThrowsAsync<ConflictException>(() => ChangeAsync(created.Id, ContractStatus.Stolen, "Furtada."));

        Assert.Empty(_services.Notifier.Messages);
    }

    [Fact]
    public async Task InventoryWrites_PublishOperationsChangedWithTheKind_D80()
    {
        var created = await CreateRadarAsync();
        await _services.MaintenanceService().RegisterAsync(created.Id, new RegisterMaintenanceRequest
        {
            Kind = Argos.Contracts.Inventory.MaintenanceKind.Scheduled,
            DueDate = EquipmentTestData.Today.AddDays(10),
        }, Ct);

        Assert.Equal(
            [(OperationsChangeKind.Registration, 1), (OperationsChangeKind.Maintenance, 2)],
            _services.Notifier.Messages.Select(message => (((OperationsChanged)message.Message).Kind, message.SavesBefore)));
    }

    private OperationsChanged LastChange() => (OperationsChanged)_services.Notifier.Messages.Last().Message;

    private Task<EquipmentDetails> CreateRadarAsync() => _services.EquipmentService().CreateAsync(RadarRequest(), Ct);

    private Task<EquipmentDetails> ChangeAsync(Guid id, ContractStatus status, string? justification = null) =>
        _services.EquipmentStatusService().ChangeAsync(
            id, new ChangeEquipmentStatusRequest { Status = status, Justification = justification }, Ct);

    private CreateEquipmentRequest RadarRequest() => new()
    {
        TypeId = _services.Type(EquipmentTypeCatalog.TrafficRadar).Id,
        SerialNumber = "RD-2024-0057",
        Brand = "Vigilis",
        Model = "VX-300",
        OrganizationId = _services.Organization.Id,
        Location = new LocationInput
        {
            ZipCode = "16985-001",
            Street = "Rua das Acácias",
            NeighborhoodId = _services.Neighborhood.Id,
            Reference = "número 181",
            Latitude = -22.21,
            Longitude = -49.66,
        },
        Attributes = new() { ["modality"] = "speed", ["maxSpeedKmh"] = "60", ["monitoredLanes"] = "2" },
        Status = ContractStatus.Operational,
    };
}
