using Argos.Application.Common.Exceptions;
using Argos.Contracts.Common;
using Argos.Contracts.Inventory;
using Argos.Domain.Common;
using Argos.Domain.Inventory;
using Argos.Domain.Inventory.Types;
using Argos.UnitTests.Domain.Inventory;
using ContractMaintenanceKind = Argos.Contracts.Inventory.MaintenanceKind;
using ContractStatus = Argos.Contracts.Inventory.EquipmentStatus;
using DomainEndReason = Argos.Domain.Inventory.Agents.AssignmentEndReason;
using DomainMaintenanceState = Argos.Domain.Inventory.Equipments.MaintenanceState;

namespace Argos.UnitTests.Application.Inventory;

/// <summary>
/// Inativação de equipamentos e filtro de ativos e inativos (US-09, RF-001, RF-010, RN-01, RN-35; D-49, D-62;
/// plan 15.2.6, 15.12).
/// </summary>
public sealed class EquipmentInactivationServiceTests
{
    private const string Reason = "Fim do contrato de concessão.";

    private readonly InventoryServices _services = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Inactivate_NotInstalledWithScheduledMaintenance_CancelsItAndLogsTheReasonInOneSave_RN35()
    {
        var created = await _services.EquipmentService().CreateAsync(RadarRequest() with
        {
            Status = ContractStatus.NotInstalled,
            InitialMaintenance = new RegisterMaintenanceRequest
            {
                Kind = ContractMaintenanceKind.Scheduled,
                DueDate = EquipmentTestData.Today.AddDays(10),
            },
        }, Ct);
        var savesBefore = _services.UnitOfWork.SaveCalls;
        _services.Time.Now = InventoryTestData.Now.AddHours(1);

        await _services.EquipmentInactivationService().InactivateAsync(
            created.Id, new InactivationRequest { Reason = $"  {Reason} " }, Ct);

        var equipment = _services.Inventory.Equipment.Single();
        Assert.False(equipment.IsActive);
        Assert.Equal(("admin", (DateTimeOffset?)InventoryTestData.Now.AddHours(1)), (equipment.DeactivatedBy, equipment.DeactivatedAt));
        var maintenance = Assert.Single(equipment.Maintenances);
        Assert.Equal(
            (DomainMaintenanceState.Cancelled, "Cancelada automaticamente: equipamento inativado."),
            (maintenance.State, maintenance.CancellationReason));

        var change = Assert.Single(_services.Inventory.Changes);
        Assert.Equal(
            (InventoryEntityType.Equipment, created.Id, InventoryChangeKind.Inactivated, Reason, "admin", InventoryTestData.Now.AddHours(1)),
            (change.EntityType, change.EntityId, change.Kind, change.Reason, change.Author, change.OccurredAt));
        Assert.Equal(savesBefore + 1, _services.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Inactivate_WithoutReason_LogsWithoutReason_RN35()
    {
        var created = await _services.EquipmentService().CreateAsync(RadarRequest() with { Status = ContractStatus.NotInstalled }, Ct);

        await _services.EquipmentInactivationService().InactivateAsync(created.Id, new InactivationRequest(), Ct);

        Assert.Null(Assert.Single(_services.Inventory.Changes).Reason);
    }

    [Fact]
    public async Task Inactivate_Operational_IsRefusedExplainingWhenItIsPossible_RN35_US09()
    {
        var created = await _services.EquipmentService().CreateAsync(RadarRequest(), Ct);
        var savesBefore = _services.UnitOfWork.SaveCalls;

        var exception = await Assert.ThrowsAsync<InventoryRuleException>(
            () => _services.EquipmentInactivationService().InactivateAsync(created.Id, new InactivationRequest { Reason = Reason }, Ct));

        Assert.Equal(
            "Equipamento na situação Operacional não pode ser inativado. A inativação só é possível nas situações "
            + "Ainda não instalado, Desativado ou Furtado.",
            exception.Message);
        Assert.True(_services.Inventory.Equipment.Single().IsActive);
        Assert.Empty(_services.Inventory.Changes);
        Assert.Equal(savesBefore, _services.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Inactivate_Twice_IsConflict_RN35()
    {
        var created = await _services.EquipmentService().CreateAsync(RadarRequest() with { Status = ContractStatus.NotInstalled }, Ct);
        await _services.EquipmentInactivationService().InactivateAsync(created.Id, new InactivationRequest(), Ct);

        var exception = await Assert.ThrowsAsync<InventoryRuleException>(
            () => _services.EquipmentInactivationService().InactivateAsync(created.Id, new InactivationRequest(), Ct));

        Assert.Equal("O equipamento já está inativo.", exception.Message);
        Assert.Single(_services.Inventory.Changes);
    }

    [Fact]
    public async Task Inactivate_ReasonTooLong_IsErrorOnReason_RN35()
    {
        var created = await _services.EquipmentService().CreateAsync(RadarRequest() with { Status = ContractStatus.NotInstalled }, Ct);

        var exception = await Assert.ThrowsAsync<DomainValidationException>(() => _services.EquipmentInactivationService()
            .InactivateAsync(created.Id, new InactivationRequest { Reason = new string('x', 501) }, Ct));

        Assert.Equal(["reason"], exception.Errors.Keys);
        Assert.True(_services.Inventory.Equipment.Single().IsActive);
    }

    [Fact]
    public async Task Inactivate_UnknownEquipment_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _services.EquipmentInactivationService().InactivateAsync(Guid.NewGuid(), new InactivationRequest(), Ct));
    }

    [Fact]
    public async Task Inactivate_DeactivatedPatrolWithTwoAgents_EndsAllAssignmentsInTheSameSave_RN35_D62()
    {
        var patrol = await _services.EquipmentService().CreateAsync(PatrolRequest(), Ct);
        foreach (var registration in new[] { "SMM-0101", "SMM-0102" })
        {
            var agent = await _services.AgentService().CreateAsync(AgentRequest(registration), Ct);
            await _services.AssignmentService().AssignAsync(agent.Id, new AssignEquipmentRequest { EquipmentId = patrol.Id }, Ct);
        }

        await _services.EquipmentStatusService().ChangeAsync(
            patrol.Id, new ChangeEquipmentStatusRequest { Status = ContractStatus.Deactivated, Justification = "Viatura baixada." }, Ct);
        var savesBefore = _services.UnitOfWork.SaveCalls;
        _services.Time.Now = InventoryTestData.Now.AddDays(1);

        await _services.EquipmentInactivationService().InactivateAsync(patrol.Id, new InactivationRequest { Reason = Reason }, Ct);

        Assert.Equal(2, _services.Inventory.Assignments.Count);
        Assert.All(_services.Inventory.Assignments, assignment => Assert.Equal(
            (false, (DateTimeOffset?)InventoryTestData.Now.AddDays(1), "admin", (DomainEndReason?)DomainEndReason.EquipmentInactivated),
            (assignment.IsOpen, assignment.EndedAt, assignment.EndedBy, assignment.EndReason)));
        Assert.False(_services.Inventory.Equipment.Single().IsActive);
        Assert.Equal(InventoryChangeKind.Inactivated, Assert.Single(_services.Inventory.Changes).Kind);
        Assert.Equal(savesBefore + 1, _services.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Inactivate_RefusedByRn35_KeepsTheAssignmentsOpen_RN35_D62()
    {
        var patrol = await _services.EquipmentService().CreateAsync(PatrolRequest(), Ct);
        var agent = await _services.AgentService().CreateAsync(AgentRequest("SMM-0101"), Ct);
        await _services.AssignmentService().AssignAsync(agent.Id, new AssignEquipmentRequest { EquipmentId = patrol.Id }, Ct);

        await Assert.ThrowsAsync<InventoryRuleException>(
            () => _services.EquipmentInactivationService().InactivateAsync(patrol.Id, new InactivationRequest(), Ct));

        Assert.True(Assert.Single(_services.Inventory.Assignments).IsOpen);
    }

    // RN-01 contra cadastro inativo

    [Fact]
    public async Task Create_SameBrandAndSerialAsInactive_IsConflictPointingToTheInactive_RN01()
    {
        var inactive = await _services.EquipmentService().CreateAsync(RadarRequest() with { Status = ContractStatus.NotInstalled }, Ct);
        await _services.EquipmentInactivationService().InactivateAsync(inactive.Id, new InactivationRequest(), Ct);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => _services.EquipmentService().CreateAsync(RadarRequest() with { Brand = "VIGILIS" }, Ct));

        Assert.Equal("Já existe um equipamento com essa marca e esse número de série.", exception.Message);
        Assert.Equal(inactive.Id, exception.Extensions["existingEquipmentId"]);
        Assert.Single(_services.Inventory.Equipment);
    }

    [Fact]
    public async Task Update_InactiveEquipment_IsConflict_RN35()
    {
        var created = await _services.EquipmentService().CreateAsync(RadarRequest() with { Status = ContractStatus.NotInstalled }, Ct);
        await _services.EquipmentInactivationService().InactivateAsync(created.Id, new InactivationRequest(), Ct);

        var exception = await Assert.ThrowsAsync<InventoryRuleException>(() => _services.EquipmentService().UpdateAsync(
            created.Id, UpdateFrom(RadarRequest() with { Model = "VX-400" }, created.Version), Ct));

        Assert.Equal("Equipamento inativo não pode ser editado.", exception.Message);
    }

    // Filtro de ativos e inativos (RF-010, US-09)

    [Fact]
    public async Task Search_InactiveFilter_ReachesTheQueryAndListsOnlyInactive_US09()
    {
        var inactive = await _services.EquipmentService().CreateAsync(RadarRequest() with { Status = ContractStatus.NotInstalled }, Ct);
        var active = await _services.EquipmentService().CreateAsync(RadarRequest() with { SerialNumber = "RD-2024-0058" }, Ct);
        await _services.EquipmentInactivationService().InactivateAsync(inactive.Id, new InactivationRequest(), Ct);

        var inactiveOnly = await _services.EquipmentQueryService().SearchAsync(new EquipmentSearchQuery { Activity = ActivityFilter.Inactive }, Ct);
        Assert.Equal(ActivityFilter.Inactive, _services.Queries.LastSearch!.Activity);
        var all = await _services.EquipmentQueryService().SearchAsync(new EquipmentSearchQuery { Activity = ActivityFilter.All }, Ct);
        var standard = await _services.EquipmentQueryService().SearchAsync(new EquipmentSearchQuery(), Ct);

        Assert.Equal([inactive.Id], inactiveOnly.Items.Select(item => item.Id));
        Assert.False(Assert.Single(inactiveOnly.Items).IsActive);
        Assert.Equal(2, all.TotalCount);
        Assert.Equal([active.Id], standard.Items.Select(item => item.Id));
        Assert.Equal(ActivityFilter.Active, _services.Queries.LastSearch!.Activity);
    }

    [Fact]
    public async Task Search_ActivityOutsideTheList_IsValidationErrorOnActivity_US09()
    {
        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => _services.EquipmentQueryService()
            .SearchAsync(new EquipmentSearchQuery { Activity = (ActivityFilter)7 }, Ct));

        Assert.Equal(["activity"], exception.Errors.Keys);
        Assert.Null(_services.Queries.LastSearch);
    }

    [Fact]
    public async Task Search_InvalidPageSortAndActivity_AllErrorsAtOnce_D53()
    {
        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => _services.EquipmentQueryService()
            .SearchAsync(new EquipmentSearchQuery { Page = 0, Sort = "brand", Activity = (ActivityFilter)7 }, Ct));

        Assert.Equal(["activity", "page", "sort"], exception.Errors.Keys.Order(StringComparer.Ordinal));
    }

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

    private CreateEquipmentRequest PatrolRequest() => new()
    {
        TypeId = _services.Type(EquipmentTypeCatalog.PatrolVehicle).Id,
        SerialNumber = "9BWZZZ377VT004251",
        Brand = "Orbis",
        Model = "Patrulha 4x4",
        OrganizationId = _services.Organization.Id,
        BaseId = _services.Base.Id,
        Attributes = new() { ["licensePlate"] = "ABC1D23", ["prefix"] = "SMM-01", ["vehicleKind"] = "patrol-car" },
        Status = ContractStatus.Operational,
    };

    private CreateAgentRequest AgentRequest(string registrationNumber) => new()
    {
        FullName = "Agente Fictício de Teste",
        DisplayName = "Carcará",
        RegistrationNumber = registrationNumber,
        Role = Argos.Contracts.Inventory.AgentRole.TrafficAgent,
        OrganizationId = _services.Organization.Id,
        BaseId = _services.Base.Id,
    };

    private static UpdateEquipmentRequest UpdateFrom(CreateEquipmentRequest request, uint version) => new()
    {
        SerialNumber = request.SerialNumber,
        Brand = request.Brand,
        Model = request.Model,
        OrganizationId = request.OrganizationId,
        Location = request.Location,
        BaseId = request.BaseId,
        Attributes = request.Attributes,
        Version = version,
    };
}
