using Argos.Application.Common.Exceptions;
using Argos.Contracts.Common;
using Argos.Contracts.Inventory;
using Argos.Domain.Access;
using Argos.Domain.Common;
using Argos.Domain.Inventory;
using Argos.Domain.Inventory.Equipments;
using Argos.Domain.Inventory.Support;
using Argos.Domain.Inventory.Types;
using Argos.UnitTests.Domain.Inventory;
using ContractStatus = Argos.Contracts.Inventory.EquipmentStatus;
using ContractMaintenanceKind = Argos.Contracts.Inventory.MaintenanceKind;
using ContractMaintenanceNeed = Argos.Contracts.Inventory.MaintenanceNeed;
using DomainStatus = Argos.Domain.Inventory.Equipments.EquipmentStatus;
using StatusChangeOrigin = Argos.Domain.Inventory.Equipments.StatusChangeOrigin;

namespace Argos.UnitTests.Application.Inventory;

/// <summary>
/// Casos de uso de equipamentos (US-01 a US-05, RN-01, RN-05 a RN-11; D-46, D-47, D-49, D-59, D-60; plan 15.12).
/// </summary>
public sealed class EquipmentServicesTests
{
    private readonly InventoryServices _services = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Cadastro (US-01, US-02, RN-01)

    [Fact]
    public async Task Create_FixedRadar_RecordsInitialStatusWithoutRegistrationLog_US01()
    {
        var details = await _services.EquipmentService().CreateAsync(RadarRequest(), Ct);

        var equipment = Assert.Single(_services.Inventory.Equipment);
        var initial = Assert.Single(equipment.StatusChanges);
        Assert.Null(initial.FromStatus);
        Assert.Equal(("admin", InventoryTestData.Now), (initial.Author, initial.OccurredAt));
        Assert.Empty(_services.Inventory.Changes);
        Assert.Equal(1, _services.UnitOfWork.SaveCalls);

        Assert.Equal(equipment.Id, details.Id);
        Assert.Equal(ContractStatus.Operational, details.Status);
        Assert.Equal(
            ["modality=speed (Velocidade)", "maxSpeedKmh=60 (60 km/h)", "monitoredLanes=2 (2)"],
            details.Attributes.Select(attribute => $"{attribute.Key}={attribute.Value} ({attribute.DisplayValue})"));
    }

    [Fact]
    public async Task Create_SameBrandAndSerialIgnoringCaseAndSpaces_IsConflictPointingToExisting_RN01()
    {
        var existing = await _services.EquipmentService().CreateAsync(RadarRequest(), Ct);
        var request = RadarRequest() with { Brand = " VIGILIS ", SerialNumber = "rd-2024 -0057" };

        var exception = await Assert.ThrowsAsync<ConflictException>(() => _services.EquipmentService().CreateAsync(request, Ct));

        Assert.Equal(existing.Id, exception.Extensions["existingEquipmentId"]);
        Assert.Single(_services.Inventory.Equipment);
        Assert.Equal(1, _services.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Create_InvalidLocationAndSerial_AllErrorsAtOnceWithLocationPrefix_D46()
    {
        var request = RadarRequest() with
        {
            SerialNumber = " ",
            Location = Location() with { ZipCode = "123", Latitude = -23.50 },
        };

        var exception = await Assert.ThrowsAsync<DomainValidationException>(() => _services.EquipmentService().CreateAsync(request, Ct));

        Assert.Equal(
            ["location.latitude", "location.longitude", "location.zipCode", "serialNumber"],
            exception.Errors.Keys.Order(StringComparer.Ordinal));
        Assert.Empty(_services.Inventory.Equipment);
    }

    [Fact]
    public async Task Create_SpeedRadarWithoutMaxSpeed_IsErrorOnlyOnThatAttribute_US02()
    {
        var request = RadarRequest();
        request.Attributes.Remove("maxSpeedKmh");

        var exception = await Assert.ThrowsAsync<DomainValidationException>(() => _services.EquipmentService().CreateAsync(request, Ct));

        Assert.Equal(["attributes.maxSpeedKmh"], exception.Errors.Keys);
    }

    [Fact]
    public async Task Create_InitialScheduledMaintenanceWithoutDate_IsErrorUnderInitialMaintenance_D46()
    {
        var request = RadarRequest() with
        {
            InitialMaintenance = new RegisterMaintenanceRequest { Kind = ContractMaintenanceKind.Scheduled },
        };

        var exception = await Assert.ThrowsAsync<DomainValidationException>(() => _services.EquipmentService().CreateAsync(request, Ct));

        Assert.Equal(["initialMaintenance.dueDate"], exception.Errors.Keys);
        Assert.Equal(0, _services.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Create_InitialPeriodicMaintenanceSoon_ShowsUpcomingNeed_RF002_RN10()
    {
        var request = RadarRequest() with
        {
            InitialMaintenance = new RegisterMaintenanceRequest
            {
                Kind = ContractMaintenanceKind.Periodic,
                DueDate = EquipmentTestData.Today.AddDays(5),
                PeriodicityDays = 365,
                Description = "Aferição anual",
            },
        };

        var details = await _services.EquipmentService().CreateAsync(request, Ct);

        Assert.Equal(ContractMaintenanceNeed.Upcoming, details.MaintenanceNeed);
        Assert.Equal(ContractMaintenanceNeed.Upcoming, Assert.Single(details.OpenMaintenances).Need);
    }

    [Fact]
    public async Task Create_InMalfunction_OpensImmediateMaintenance_RN07()
    {
        var details = await _services.EquipmentService().CreateAsync(RadarRequest() with { Status = ContractStatus.Malfunction }, Ct);

        Assert.Equal(ContractMaintenanceNeed.Immediate, details.MaintenanceNeed);
        Assert.Equal(ContractMaintenanceKind.Immediate, Assert.Single(details.OpenMaintenances).Kind);
    }

    [Fact]
    public async Task Create_UnknownType_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _services.EquipmentService().CreateAsync(RadarRequest() with { TypeId = Guid.NewGuid() }, Ct));
    }

    [Fact]
    public async Task Create_MobileWithBase_TakesNeighborhoodFromBase_RF007()
    {
        var request = new CreateEquipmentRequest
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

        await _services.EquipmentService().CreateAsync(request, Ct);

        var equipment = Assert.Single(_services.Inventory.Equipment);
        Assert.Equal(_services.Base.Id, equipment.BaseId);
        Assert.Null(equipment.Location);
    }

    // Edição (RF-001, D-49, D-60)

    [Fact]
    public async Task Update_WithoutChange_DoesNotSaveNorLog_D49()
    {
        var created = await _services.EquipmentService().CreateAsync(RadarRequest(), Ct);

        await _services.EquipmentService().UpdateAsync(created.Id, UpdateFrom(RadarRequest(), created.Version), Ct);

        Assert.Equal(1, _services.UnitOfWork.SaveCalls);
        Assert.Empty(_services.Inventory.Changes);
    }

    [Fact]
    public async Task Update_NewOrganization_LogsOrganizationNames_D49()
    {
        var created = await _services.EquipmentService().CreateAsync(RadarRequest(), Ct);
        var guard = Organization.Create("Guarda Civil Municipal", "GCM");
        _services.Inventory.Organizations.Add(guard);
        var request = UpdateFrom(RadarRequest(), created.Version) with { OrganizationId = guard.Id };

        await _services.EquipmentService().UpdateAsync(created.Id, request, Ct);

        var change = Assert.Single(_services.Inventory.Changes);
        Assert.Equal((InventoryEntityType.Equipment, InventoryChangeKind.Updated), (change.EntityType, change.Kind));
        Assert.Equal(
            new FieldChange("organizationId", "Órgão responsável", "Secretaria Municipal de Mobilidade", "Guarda Civil Municipal"),
            Assert.Single(change.Changes));
        Assert.Equal(2, _services.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Update_StaleVersion_IsConflict_D60()
    {
        var created = await _services.EquipmentService().CreateAsync(RadarRequest(), Ct);
        _services.Inventory.EquipmentVersions[created.Id] = created.Version + 1;

        await Assert.ThrowsAsync<ConflictException>(
            () => _services.EquipmentService().UpdateAsync(created.Id, UpdateFrom(RadarRequest() with { Model = "VX-400" }, created.Version), Ct));

        Assert.Empty(_services.Inventory.Changes);
    }

    [Fact]
    public async Task Update_ToBrandAndSerialOfAnother_IsConflict_RN01()
    {
        var first = await _services.EquipmentService().CreateAsync(RadarRequest(), Ct);
        var second = await _services.EquipmentService().CreateAsync(RadarRequest() with { SerialNumber = "RD-2024-0058" }, Ct);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => _services.EquipmentService().UpdateAsync(second.Id, UpdateFrom(RadarRequest(), second.Version), Ct));

        Assert.Equal(first.Id, exception.Extensions["existingEquipmentId"]);
    }

    // Situação (US-04, RN-05 a RN-07, D-47)

    [Fact]
    public async Task ChangeStatus_ToMalfunction_RecordsHistoryAndOpensImmediate_RN07()
    {
        var created = await _services.EquipmentService().CreateAsync(RadarRequest(), Ct);
        _services.Actor.Name = "operador";
        _services.Actor.Role = UserRole.Operator;
        _services.Time.Now = InventoryTestData.Now.AddHours(2);

        var details = await _services.EquipmentStatusService().ChangeAsync(
            created.Id, new ChangeEquipmentStatusRequest { Status = ContractStatus.Malfunction }, Ct);

        var change = _services.Inventory.Equipment.Single().StatusChanges.Last();
        Assert.Equal(
            ((DomainStatus?)DomainStatus.Operational, DomainStatus.Malfunction, StatusChangeOrigin.Manual, "operador", InventoryTestData.Now.AddHours(2)),
            (change.FromStatus, change.ToStatus, change.Origin, change.Author, change.OccurredAt));
        Assert.Equal(ContractMaintenanceNeed.Immediate, details.MaintenanceNeed);
        Assert.Equal(2, _services.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task ChangeStatus_ReactivationByAdministrator_IsAllowed_RN05_D47()
    {
        var id = await DeactivatedRadarAsync();

        var details = await _services.EquipmentStatusService().ChangeAsync(
            id, new ChangeEquipmentStatusRequest { Status = ContractStatus.Operational, Justification = "Equipamento recuperado." }, Ct);

        Assert.Equal(ContractStatus.Operational, details.Status);
    }

    [Fact]
    public async Task ChangeStatus_ReactivationByOperator_IsForbidden_RN05_D47()
    {
        var id = await DeactivatedRadarAsync();
        _services.Actor.Role = UserRole.Operator;

        var exception = await Assert.ThrowsAsync<ForbiddenOperationException>(() => _services.EquipmentStatusService().ChangeAsync(
            id, new ChangeEquipmentStatusRequest { Status = ContractStatus.Operational, Justification = "Equipamento recuperado." }, Ct));

        Assert.Equal("Somente o Administrador pode reativar um equipamento desativado.", exception.Message);
        Assert.Equal(DomainStatus.Deactivated, _services.Inventory.Equipment.Single().Status);
    }

    [Fact]
    public async Task ChangeStatus_ReactivationByActorWithoutRole_IsForbidden_RN05_D47()
    {
        var id = await DeactivatedRadarAsync();
        _services.Actor.Name = "PROMETHEUS";
        _services.Actor.Role = null;

        await Assert.ThrowsAsync<ForbiddenOperationException>(() => _services.EquipmentStatusService().ChangeAsync(
            id, new ChangeEquipmentStatusRequest { Status = ContractStatus.Operational, Justification = "Equipamento recuperado." }, Ct));
    }

    [Fact]
    public async Task ChangeStatus_TransitionOutsideTable_IsBusinessRuleWithReason_RN05()
    {
        var created = await _services.EquipmentService().CreateAsync(RadarRequest() with { Status = ContractStatus.NotInstalled }, Ct);

        var exception = await Assert.ThrowsAsync<InventoryRuleException>(() => _services.EquipmentStatusService().ChangeAsync(
            created.Id, new ChangeEquipmentStatusRequest { Status = ContractStatus.Stolen, Justification = "Furtado na madrugada." }, Ct));

        Assert.Equal("Não é permitido passar de Ainda não instalado para Furtado.", exception.Message);
    }

    [Fact]
    public async Task ChangeStatus_UnknownEquipment_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _services.EquipmentStatusService().ChangeAsync(
            Guid.NewGuid(), new ChangeEquipmentStatusRequest { Status = ContractStatus.Operational }, Ct));
    }

    // Detalhes (D-59)

    [Fact]
    public async Task Details_AllowedStatusChanges_FollowTheActorPermissions_D47_D59()
    {
        var id = await DeactivatedRadarAsync();

        var administrator = await _services.EquipmentQueryService().GetDetailsAsync(id, Ct);
        _services.Actor.Role = UserRole.Operator;
        var @operator = await _services.EquipmentQueryService().GetDetailsAsync(id, Ct);
        _services.Actor.Role = UserRole.Manager;
        var manager = await _services.EquipmentQueryService().GetDetailsAsync(id, Ct);

        Assert.Equal([new StatusChangeOptionData(ContractStatus.Operational, true)], administrator.AllowedStatusChanges);
        Assert.Empty(@operator.AllowedStatusChanges);
        Assert.Empty(manager.AllowedStatusChanges);
    }

    [Fact]
    public async Task Details_OperatorOnOperational_GetsTheRn05TargetsWithJustificationFlags_RN06()
    {
        var created = await _services.EquipmentService().CreateAsync(RadarRequest(), Ct);
        _services.Actor.Role = UserRole.Operator;

        var details = await _services.EquipmentQueryService().GetDetailsAsync(created.Id, Ct);

        Assert.Equal(
            [
                new StatusChangeOptionData(ContractStatus.Malfunction, false),
                new StatusChangeOptionData(ContractStatus.UnderMaintenance, false),
                new StatusChangeOptionData(ContractStatus.Deactivated, true),
                new StatusChangeOptionData(ContractStatus.Stolen, true),
            ],
            details.AllowedStatusChanges);
    }

    [Fact]
    public async Task Search_UnknownSort_IsValidationErrorOnSort()
    {
        var exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _services.EquipmentQueryService().SearchAsync(new EquipmentSearchQuery { Sort = "brand" }, Ct));

        Assert.Equal(["sort"], exception.Errors.Keys);
    }

    [Fact]
    public async Task Search_WithoutSort_UsesDefaultAndMunicipalTodayAndWindowParameter_RN10_RN34()
    {
        _services.Inventory.Parameters[global::Argos.Domain.Common.Parameters.BusinessParameter.MaintenanceUpcomingWindowDays] = 10;

        await _services.EquipmentQueryService().SearchAsync(new EquipmentSearchQuery(), Ct);

        Assert.Equal(EquipmentSortFields.SerialNumber, _services.Queries.LastSearch!.Sort);
        Assert.Equal((EquipmentTestData.Today, 10), _services.Queries.LastSearchCalendar);
    }

    [Fact]
    public async Task ListStatusChanges_UnknownEquipment_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _services.EquipmentQueryService().ListStatusChangesAsync(Guid.NewGuid(), new PageQuery(), Ct));
    }

    // Manutenções (US-05, RN-08 a RN-11)

    [Fact]
    public async Task RegisterMaintenance_ScheduledWithoutDate_IsErrorOnDueDate_RN08()
    {
        var created = await _services.EquipmentService().CreateAsync(RadarRequest(), Ct);

        var exception = await Assert.ThrowsAsync<DomainValidationException>(() => _services.MaintenanceService().RegisterAsync(
            created.Id, new RegisterMaintenanceRequest { Kind = ContractMaintenanceKind.Scheduled }, Ct));

        Assert.Equal(["dueDate"], exception.Errors.Keys);
    }

    [Fact]
    public async Task RegisterMaintenance_ScheduledPastTheWindow_IsScheduledNeed_RN10()
    {
        var created = await _services.EquipmentService().CreateAsync(RadarRequest(), Ct);

        var maintenance = await _services.MaintenanceService().RegisterAsync(created.Id, new RegisterMaintenanceRequest
        {
            Kind = ContractMaintenanceKind.Scheduled,
            DueDate = EquipmentTestData.Today.AddDays(30),
        }, Ct);

        Assert.Equal(ContractMaintenanceNeed.Scheduled, maintenance.Need);
        Assert.Equal(2, _services.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task CompleteMaintenance_PeriodicWithoutSuccess_SchedulesNextAndGoesToMalfunction_RN09_RN11()
    {
        var created = await _services.EquipmentService().CreateAsync(RadarRequest() with
        {
            InitialMaintenance = new RegisterMaintenanceRequest
            {
                Kind = ContractMaintenanceKind.Periodic,
                DueDate = EquipmentTestData.Today,
                PeriodicityDays = 180,
            },
        }, Ct);
        var periodic = Assert.Single(created.OpenMaintenances);
        _services.Time.Now = InventoryTestData.Now.AddHours(1);

        var completion = await _services.MaintenanceService().CompleteAsync(created.Id, periodic.Id, new CompleteMaintenanceRequest
        {
            Result = Argos.Contracts.Inventory.MaintenanceResult.Failure,
            CompletedAt = InventoryTestData.Now.AddMinutes(30),
            Notes = "Sensor danificado.",
        }, Ct);

        Assert.Equal(ContractStatus.Malfunction, completion.Status);
        Assert.Equal(Argos.Contracts.Inventory.MaintenanceState.Completed, completion.Completed.State);
        Assert.Null(completion.Completed.Need);
        Assert.Equal(EquipmentTestData.Today.AddDays(180), completion.NextPeriodic!.DueDate);

        var equipment = _services.Inventory.Equipment.Single();
        var change = equipment.StatusChanges.Last();
        Assert.Equal((StatusChangeOrigin.MaintenanceCompletion, (Guid?)periodic.Id), (change.Origin, change.MaintenanceId));
        Assert.True(equipment.HasOpenImmediateMaintenance);
    }

    [Fact]
    public async Task CompleteMaintenance_UnknownMaintenance_IsNotFound()
    {
        var created = await _services.EquipmentService().CreateAsync(RadarRequest(), Ct);

        await Assert.ThrowsAsync<NotFoundException>(() => _services.MaintenanceService().CompleteAsync(
            created.Id,
            Guid.NewGuid(),
            new CompleteMaintenanceRequest
            {
                Result = Argos.Contracts.Inventory.MaintenanceResult.Success,
                CompletedAt = InventoryTestData.Now,
            }, Ct));
    }

    [Fact]
    public async Task CompleteMaintenance_AlreadyClosed_IsBusinessRule_RN08()
    {
        var created = await _services.EquipmentService().CreateAsync(RadarRequest() with { Status = ContractStatus.Malfunction }, Ct);
        var immediate = Assert.Single(created.OpenMaintenances);
        var request = new CompleteMaintenanceRequest
        {
            Result = Argos.Contracts.Inventory.MaintenanceResult.Success,
            CompletedAt = InventoryTestData.Now,
        };
        await _services.MaintenanceService().CompleteAsync(created.Id, immediate.Id, request, Ct);

        await Assert.ThrowsAsync<InventoryRuleException>(
            () => _services.MaintenanceService().CompleteAsync(created.Id, immediate.Id, request, Ct));
    }

    [Fact]
    public async Task ListMaintenances_FillsNeedOnlyForOpenOnes_RN10()
    {
        var created = await _services.EquipmentService().CreateAsync(RadarRequest() with { Status = ContractStatus.Malfunction }, Ct);
        var immediate = Assert.Single(created.OpenMaintenances);
        await _services.MaintenanceService().CompleteAsync(created.Id, immediate.Id, new CompleteMaintenanceRequest
        {
            Result = Argos.Contracts.Inventory.MaintenanceResult.Success,
            CompletedAt = InventoryTestData.Now,
        }, Ct);
        await _services.MaintenanceService().RegisterAsync(created.Id, new RegisterMaintenanceRequest
        {
            Kind = ContractMaintenanceKind.Scheduled,
            DueDate = EquipmentTestData.Today.AddDays(2),
        }, Ct);

        var maintenances = await _services.EquipmentQueryService().ListMaintenancesAsync(created.Id, state: null, Ct);

        Assert.Equal(
            [ContractMaintenanceNeed.Upcoming, null],
            maintenances.OrderBy(maintenance => maintenance.State).Select(maintenance => maintenance.Need));
    }

    private async Task<Guid> DeactivatedRadarAsync()
    {
        var created = await _services.EquipmentService().CreateAsync(RadarRequest(), Ct);
        await _services.EquipmentStatusService().ChangeAsync(
            created.Id, new ChangeEquipmentStatusRequest { Status = ContractStatus.Deactivated, Justification = "Fim da concessão." }, Ct);
        return created.Id;
    }

    private CreateEquipmentRequest RadarRequest() => new()
    {
        TypeId = _services.Type(EquipmentTypeCatalog.TrafficRadar).Id,
        SerialNumber = "RD-2024-0057",
        Brand = "Vigilis",
        Model = "VX-300",
        OrganizationId = _services.Organization.Id,
        Location = Location(),
        Attributes = new() { ["modality"] = "speed", ["maxSpeedKmh"] = "60", ["monitoredLanes"] = "2" },
        Status = ContractStatus.Operational,
    };

    private LocationInput Location() => new()
    {
        ZipCode = "16985-001",
        Street = "Rua das Acácias",
        NeighborhoodId = _services.Neighborhood.Id,
        Reference = "número 181",
        Latitude = -22.21,
        Longitude = -49.66,
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
