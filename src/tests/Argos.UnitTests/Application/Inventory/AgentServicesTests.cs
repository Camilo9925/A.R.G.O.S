using Argos.Application.Common.Exceptions;
using Argos.Contracts.Common;
using Argos.Contracts.Inventory;
using Argos.Domain.Access;
using Argos.Domain.Common;
using Argos.Domain.Inventory;
using Argos.Domain.Inventory.Support;
using Argos.Domain.Inventory.Types;
using Argos.UnitTests.Domain.Inventory;
using ContractAgentRole = Argos.Contracts.Inventory.AgentRole;
using ContractAgentStatus = Argos.Contracts.Inventory.AgentStatus;
using ContractEndReason = Argos.Contracts.Inventory.AssignmentEndReason;
using ContractStatus = Argos.Contracts.Inventory.EquipmentStatus;
using DomainAgentStatus = Argos.Domain.Inventory.Agents.AgentStatus;
using DomainEndReason = Argos.Domain.Inventory.Agents.AssignmentEndReason;

namespace Argos.UnitTests.Application.Inventory;

/// <summary>
/// Casos de uso de agentes e vínculos (US-06, US-07, US-09, RF-013 a RF-017, RN-12, RN-22, RN-35; D-46, D-49, D-59,
/// D-60, D-62; plan 15.2.9, 15.2.10, 15.12).
/// </summary>
public sealed class AgentServicesTests
{
    private const string Registration = "SMM-0101";

    private readonly InventoryServices _services = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Cadastro (US-06, RN-12, RN-22)

    [Fact]
    public async Task Create_StartsOffDutyWithInitialRecordAndWithoutRegistrationLog_US06_RN12()
    {
        var details = await _services.AgentService().CreateAsync(AgentRequest(), Ct);

        var agent = Assert.Single(_services.Inventory.Agents);
        var initial = Assert.Single(agent.StatusChanges);
        Assert.Equal(
            ((DomainAgentStatus?)null, DomainAgentStatus.OffDuty, "admin", InventoryTestData.Now),
            (initial.FromStatus, initial.ToStatus, initial.Author, initial.OccurredAt));
        Assert.Empty(_services.Inventory.Changes);
        Assert.Equal(1, _services.UnitOfWork.SaveCalls);

        Assert.Equal(agent.Id, details.Id);
        Assert.Equal(ContractAgentStatus.OffDuty, details.Status);
        Assert.Equal((_services.Organization.Id, _services.Base.Id), (details.OrganizationId, details.BaseId));
    }

    [Fact]
    public async Task Create_SameRegistrationInSameOrganizationIgnoringCase_IsConflict_US06()
    {
        await _services.AgentService().CreateAsync(AgentRequest(), Ct);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => _services.AgentService().CreateAsync(AgentRequest(" smm-0101 ") with { DisplayName = "Gavião" }, Ct));

        Assert.Equal("Já existe um agente com essa matrícula neste órgão.", exception.Message);
        Assert.Single(_services.Inventory.Agents);
        Assert.Equal(1, _services.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Create_SameRegistrationInAnotherOrganization_IsAccepted_US06()
    {
        await _services.AgentService().CreateAsync(AgentRequest(), Ct);
        var (guard, guardBase) = AnotherOrganizationWithBase();

        await _services.AgentService().CreateAsync(
            AgentRequest() with { OrganizationId = guard.Id, BaseId = guardBase.Id, Role = ContractAgentRole.MunicipalGuard }, Ct);

        Assert.Equal(2, _services.Inventory.Agents.Count);
        Assert.Equal(2, _services.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Create_SameRegistrationAsInactiveAgent_IsConflict_US06_RN35()
    {
        var inactive = await _services.AgentService().CreateAsync(AgentRequest(), Ct);
        await _services.AgentService().InactivateAsync(inactive.Id, new InactivationRequest(), Ct);

        await Assert.ThrowsAsync<ConflictException>(() => _services.AgentService().CreateAsync(AgentRequest(), Ct));
    }

    [Fact]
    public async Task Create_InvalidFieldsAndNoRole_AllErrorsAtOnce_D46()
    {
        var request = AgentRequest("SMM 0101") with { FullName = " ", Role = null };

        var exception = await Assert.ThrowsAsync<DomainValidationException>(() => _services.AgentService().CreateAsync(request, Ct));

        Assert.Equal(["fullName", "registrationNumber", "role"], exception.Errors.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["Escolha a função do agente."], exception.Errors["role"]);
        Assert.Empty(_services.Inventory.Agents);
        Assert.Equal(0, _services.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Create_UnknownBase_IsNotFound()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _services.AgentService().CreateAsync(AgentRequest() with { BaseId = Guid.NewGuid() }, Ct));

        Assert.Equal("Base operacional não encontrada.", exception.Message);
    }

    [Fact]
    public async Task Create_InactiveOrganization_IsErrorOnOrganizationId_RN22()
    {
        _services.Organization.Inactivate(InventoryTestData.Now, "admin");

        var exception = await Assert.ThrowsAsync<DomainValidationException>(() => _services.AgentService().CreateAsync(AgentRequest(), Ct));

        Assert.Equal(["organizationId"], exception.Errors.Keys);
    }

    // Edição (RF-013, D-49, D-60)

    [Fact]
    public async Task Update_NewBaseAndRank_LogsTheBaseNamesInOneSave_D49()
    {
        var created = await _services.AgentService().CreateAsync(AgentRequest(), Ct);
        var north = OperationalBase.Create("Base Norte", _services.Organization, _services.NewLocation());
        _services.Inventory.Bases.Add(north);

        await _services.AgentService().UpdateAsync(
            created.Id, UpdateFrom(AgentRequest(), created.Version) with { BaseId = north.Id, Rank = "Supervisor" }, Ct);

        var change = Assert.Single(_services.Inventory.Changes);
        Assert.Equal((InventoryEntityType.Agent, created.Id, InventoryChangeKind.Updated), (change.EntityType, change.EntityId, change.Kind));
        Assert.Equal(
            [
                new FieldChange("rank", "Posto ou graduação", null, "Supervisor"),
                new FieldChange("baseId", "Base operacional", "Central de Mobilidade", "Base Norte"),
            ],
            change.Changes);
        Assert.Equal(2, _services.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Update_WithoutChange_DoesNotSaveNorLog_D49()
    {
        var created = await _services.AgentService().CreateAsync(AgentRequest(), Ct);

        await _services.AgentService().UpdateAsync(created.Id, UpdateFrom(AgentRequest(), created.Version), Ct);

        Assert.Empty(_services.Inventory.Changes);
        Assert.Equal(1, _services.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Update_StaleVersion_IsConflict_D60()
    {
        var created = await _services.AgentService().CreateAsync(AgentRequest(), Ct);
        _services.Inventory.AgentVersions[created.Id] = created.Version + 1;

        await Assert.ThrowsAsync<ConflictException>(() => _services.AgentService().UpdateAsync(
            created.Id, UpdateFrom(AgentRequest() with { DisplayName = "Gavião" }, created.Version), Ct));

        Assert.Equal("Carcará", _services.Inventory.Agents.Single().DisplayName);
    }

    [Fact]
    public async Task Update_ToRegistrationOfAnotherAgentOfTheOrganization_IsConflict_US06()
    {
        await _services.AgentService().CreateAsync(AgentRequest(), Ct);
        var second = await _services.AgentService().CreateAsync(AgentRequest("SMM-0102"), Ct);

        await Assert.ThrowsAsync<ConflictException>(
            () => _services.AgentService().UpdateAsync(second.Id, UpdateFrom(AgentRequest(), second.Version), Ct));

        Assert.Empty(_services.Inventory.Changes);
    }

    [Fact]
    public async Task Update_InactiveAgent_IsConflict_RN35()
    {
        var created = await _services.AgentService().CreateAsync(AgentRequest(), Ct);
        await _services.AgentService().InactivateAsync(created.Id, new InactivationRequest(), Ct);

        var exception = await Assert.ThrowsAsync<InventoryRuleException>(() => _services.AgentService().UpdateAsync(
            created.Id, UpdateFrom(AgentRequest() with { DisplayName = "Gavião" }, created.Version), Ct));

        Assert.Equal("Agente inativo não pode ser editado.", exception.Message);
    }

    [Fact]
    public async Task Update_UnknownAgent_IsNotFound()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _services.AgentService().UpdateAsync(Guid.NewGuid(), UpdateFrom(AgentRequest(), 1), Ct));

        Assert.Equal("Agente não encontrado.", exception.Message);
    }

    // Inativação (US-09, RN-35)

    [Fact]
    public async Task Inactivate_OnLeaveWithAssignment_EndsItAndLogsTheReasonInOneSave_RN35()
    {
        var agent = await _services.AgentService().CreateAsync(AgentRequest(), Ct);
        var patrol = await _services.EquipmentService().CreateAsync(PatrolRequest(), Ct);
        await _services.AssignmentService().AssignAsync(agent.Id, new AssignEquipmentRequest { EquipmentId = patrol.Id }, Ct);
        await _services.AgentStatusService().ChangeAsync(agent.Id, new ChangeAgentStatusRequest { Status = ContractAgentStatus.OnLeave }, Ct);
        var savesBefore = _services.UnitOfWork.SaveCalls;
        _services.Time.Now = InventoryTestData.Now.AddDays(1);

        await _services.AgentService().InactivateAsync(agent.Id, new InactivationRequest { Reason = "Desligado do órgão." }, Ct);

        Assert.False(_services.Inventory.Agents.Single().IsActive);
        var assignment = Assert.Single(_services.Inventory.Assignments);
        Assert.Equal(
            (false, (DateTimeOffset?)InventoryTestData.Now.AddDays(1), (DomainEndReason?)DomainEndReason.AgentInactivated),
            (assignment.IsOpen, assignment.EndedAt, assignment.EndReason));
        var change = Assert.Single(_services.Inventory.Changes);
        Assert.Equal(
            (InventoryEntityType.Agent, InventoryChangeKind.Inactivated, "Desligado do órgão."),
            (change.EntityType, change.Kind, change.Reason));
        Assert.Equal(savesBefore + 1, _services.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Inactivate_OnDuty_IsRefusedExplainingWhenItIsPossible_RN35_US09()
    {
        var agent = await _services.AgentService().CreateAsync(AgentRequest(), Ct);
        var patrol = await _services.EquipmentService().CreateAsync(PatrolRequest(), Ct);
        await _services.AssignmentService().AssignAsync(agent.Id, new AssignEquipmentRequest { EquipmentId = patrol.Id }, Ct);
        await _services.AgentStatusService().ChangeAsync(agent.Id, new ChangeAgentStatusRequest { Status = ContractAgentStatus.OnDuty }, Ct);
        var savesBefore = _services.UnitOfWork.SaveCalls;

        var exception = await Assert.ThrowsAsync<InventoryRuleException>(
            () => _services.AgentService().InactivateAsync(agent.Id, new InactivationRequest(), Ct));

        Assert.Equal(
            "Agente na situação Em serviço não pode ser inativado. A inativação só é possível nas situações Fora de serviço "
            + "ou Afastado.",
            exception.Message);
        Assert.True(_services.Inventory.Agents.Single().IsActive);
        Assert.True(Assert.Single(_services.Inventory.Assignments).IsOpen);
        Assert.Empty(_services.Inventory.Changes);
        Assert.Equal(savesBefore, _services.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Inactivate_UnknownAgent_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _services.AgentService().InactivateAsync(Guid.NewGuid(), new InactivationRequest(), Ct));
    }

    // Situação (RN-12, D-59)

    [Fact]
    public async Task ChangeStatus_AnyOtherStatus_RecordsAuthorAndTimeWithoutJustification_RN12()
    {
        var created = await _services.AgentService().CreateAsync(AgentRequest(), Ct);
        _services.Actor.Name = "operador";
        _services.Actor.Role = UserRole.Operator;
        _services.Time.Now = InventoryTestData.Now.AddHours(2);

        var details = await _services.AgentStatusService().ChangeAsync(
            created.Id, new ChangeAgentStatusRequest { Status = ContractAgentStatus.Responding }, Ct);

        var change = _services.Inventory.Agents.Single().StatusChanges.Last();
        Assert.Equal(
            ((DomainAgentStatus?)DomainAgentStatus.OffDuty, DomainAgentStatus.Responding, "operador", InventoryTestData.Now.AddHours(2)),
            (change.FromStatus, change.ToStatus, change.Author, change.OccurredAt));
        Assert.Equal(ContractAgentStatus.Responding, details.Status);
        Assert.Equal(2, _services.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task ChangeStatus_SameStatus_IsBusinessRule_RN12()
    {
        var created = await _services.AgentService().CreateAsync(AgentRequest(), Ct);

        var exception = await Assert.ThrowsAsync<InventoryRuleException>(() => _services.AgentStatusService().ChangeAsync(
            created.Id, new ChangeAgentStatusRequest { Status = ContractAgentStatus.OffDuty }, Ct));

        Assert.Equal("O agente já está nessa situação.", exception.Message);
        Assert.Equal(1, _services.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task ChangeStatus_WithoutStatus_IsValidationErrorOnStatus()
    {
        var created = await _services.AgentService().CreateAsync(AgentRequest(), Ct);

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _services.AgentStatusService().ChangeAsync(created.Id, new ChangeAgentStatusRequest(), Ct));

        Assert.Equal(["status"], exception.Errors.Keys);
    }

    [Fact]
    public async Task ChangeStatus_UnknownAgent_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _services.AgentStatusService().ChangeAsync(
            Guid.NewGuid(), new ChangeAgentStatusRequest { Status = ContractAgentStatus.OnDuty }, Ct));
    }

    [Fact]
    public async Task Details_AllowedStatusChanges_AreTheOtherFourForWhoChangesStatus_RN12_D59()
    {
        var created = await _services.AgentService().CreateAsync(AgentRequest(), Ct);

        var administrator = await _services.AgentQueryService().GetDetailsAsync(created.Id, Ct);
        _services.Actor.Role = UserRole.Operator;
        var @operator = await _services.AgentQueryService().GetDetailsAsync(created.Id, Ct);
        _services.Actor.Role = UserRole.Manager;
        var manager = await _services.AgentQueryService().GetDetailsAsync(created.Id, Ct);

        ContractAgentStatus[] others =
            [ContractAgentStatus.OnDuty, ContractAgentStatus.Responding, ContractAgentStatus.OnBreak, ContractAgentStatus.OnLeave];
        Assert.Equal(others, administrator.AllowedStatusChanges);
        Assert.Equal(others, @operator.AllowedStatusChanges);
        Assert.Empty(manager.AllowedStatusChanges);
    }

    [Fact]
    public async Task Details_InactiveAgent_HasNoStatusOptions_RN35_D59()
    {
        var created = await _services.AgentService().CreateAsync(AgentRequest(), Ct);
        await _services.AgentService().InactivateAsync(created.Id, new InactivationRequest(), Ct);

        var details = await _services.AgentQueryService().GetDetailsAsync(created.Id, Ct);

        Assert.False(details.IsActive);
        Assert.Empty(details.AllowedStatusChanges);
    }

    [Fact]
    public async Task Details_UnknownAgent_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _services.AgentQueryService().GetDetailsAsync(Guid.NewGuid(), Ct));
    }

    // Pesquisa (RF-017, US-09)

    [Fact]
    public async Task Search_WithoutSort_UsesDisplayNameAndKeepsTheFilters_RF017()
    {
        var query = new AgentSearchQuery { Role = [ContractAgentRole.TrafficAgent], Status = [ContractAgentStatus.OnDuty] };

        await _services.AgentQueryService().SearchAsync(query, Ct);

        var received = _services.Queries.LastAgentSearch!;
        Assert.Equal(AgentSortFields.DisplayName, received.Sort);
        Assert.Equal([ContractAgentRole.TrafficAgent], received.Role);
        Assert.Equal([ContractAgentStatus.OnDuty], received.Status);
    }

    [Fact]
    public async Task Search_InactiveFilter_ListsOnlyInactive_US09()
    {
        var inactive = await _services.AgentService().CreateAsync(AgentRequest(), Ct);
        await _services.AgentService().CreateAsync(AgentRequest("SMM-0102"), Ct);
        await _services.AgentService().InactivateAsync(inactive.Id, new InactivationRequest(), Ct);

        var result = await _services.AgentQueryService().SearchAsync(new AgentSearchQuery { Activity = ActivityFilter.Inactive }, Ct);
        var standard = await _services.AgentQueryService().SearchAsync(new AgentSearchQuery(), Ct);

        Assert.Equal([inactive.Id], result.Items.Select(item => item.Id));
        Assert.DoesNotContain(standard.Items, item => item.Id == inactive.Id);
    }

    [Fact]
    public async Task Search_UnknownSortAndActivity_AreValidationErrors()
    {
        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => _services.AgentQueryService()
            .SearchAsync(new AgentSearchQuery { Sort = "fullName", Activity = (ActivityFilter)9 }, Ct));

        Assert.Equal(["activity", "sort"], exception.Errors.Keys.Order(StringComparer.Ordinal));
        Assert.Null(_services.Queries.LastAgentSearch);
    }

    // Vínculos (US-07, RF-016, D-62)

    [Fact]
    public async Task Assign_ToAnotherVehicle_EndsThePreviousAndStartsTheNewInOneSave_US07()
    {
        var agent = await _services.AgentService().CreateAsync(AgentRequest(), Ct);
        var first = await _services.EquipmentService().CreateAsync(PatrolRequest(), Ct);
        var second = await _services.EquipmentService().CreateAsync(MotorcycleRequest(), Ct);
        var previous = await _services.AssignmentService().AssignAsync(agent.Id, new AssignEquipmentRequest { EquipmentId = first.Id }, Ct);
        var savesBefore = _services.UnitOfWork.SaveCalls;
        _services.Time.Now = InventoryTestData.Now.AddHours(5);

        var started = await _services.AssignmentService().AssignAsync(agent.Id, new AssignEquipmentRequest { EquipmentId = second.Id }, Ct);

        Assert.Equal(savesBefore + 1, _services.UnitOfWork.SaveCalls);
        var ended = _services.Inventory.Assignments.Single(assignment => assignment.Id == previous.Id);
        Assert.Equal(
            (false, (DateTimeOffset?)InventoryTestData.Now.AddHours(5), "admin", (DomainEndReason?)DomainEndReason.Reassigned),
            (ended.IsOpen, ended.EndedAt, ended.EndedBy, ended.EndReason));
        Assert.Equal(
            (agent.Id, second.Id, InventoryTestData.Now.AddHours(5), "admin", (DateTimeOffset?)null, (ContractEndReason?)null),
            (started.AgentId, started.EquipmentId, started.StartedAt, started.StartedBy, started.EndedAt, started.EndReason));
        Assert.Equal(started.Id, Assert.Single(_services.Inventory.Assignments, assignment => assignment.IsOpen).Id);

        var details = await _services.AgentQueryService().GetDetailsAsync(agent.Id, Ct);
        Assert.Equal(second.Id, details.AssignedEquipment!.EquipmentId);
    }

    [Fact]
    public async Task Assign_Scooter_IsRefusedWithTheReason_US07()
    {
        var agent = await _services.AgentService().CreateAsync(AgentRequest(), Ct);
        var scooterType = _services.Type(EquipmentTypeCatalog.ElectricScooter);
        var scooter = await _services.EquipmentService().CreateAsync(new CreateEquipmentRequest
        {
            TypeId = scooterType.Id,
            SerialNumber = "PT-0001",
            Brand = "Rodas Leves",
            Model = "Urbano",
            OrganizationId = _services.Organization.Id,
            BaseId = _services.Base.Id,
            Attributes = new() { ["modality"] = "shared", ["operatorCompany"] = "Rodas Leves" },
            Status = ContractStatus.Operational,
        }, Ct);
        var savesBefore = _services.UnitOfWork.SaveCalls;

        var exception = await Assert.ThrowsAsync<InventoryRuleException>(() => _services.AssignmentService()
            .AssignAsync(agent.Id, new AssignEquipmentRequest { EquipmentId = scooter.Id }, Ct));

        Assert.Equal($"Equipamentos do tipo {scooterType.Name} não recebem vínculo de agentes.", exception.Message);
        Assert.Empty(_services.Inventory.Assignments);
        Assert.Equal(savesBefore, _services.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Assign_WithoutEquipment_IsValidationErrorOnEquipmentId()
    {
        var agent = await _services.AgentService().CreateAsync(AgentRequest(), Ct);

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => _services.AssignmentService().AssignAsync(agent.Id, new AssignEquipmentRequest(), Ct));

        Assert.Equal(["equipmentId"], exception.Errors.Keys);
    }

    [Fact]
    public async Task Assign_UnknownAgentOrEquipment_IsNotFound()
    {
        var agent = await _services.AgentService().CreateAsync(AgentRequest(), Ct);
        var patrol = await _services.EquipmentService().CreateAsync(PatrolRequest(), Ct);

        var unknownAgent = await Assert.ThrowsAsync<NotFoundException>(() => _services.AssignmentService()
            .AssignAsync(Guid.NewGuid(), new AssignEquipmentRequest { EquipmentId = patrol.Id }, Ct));
        var unknownEquipment = await Assert.ThrowsAsync<NotFoundException>(() => _services.AssignmentService()
            .AssignAsync(agent.Id, new AssignEquipmentRequest { EquipmentId = Guid.NewGuid() }, Ct));

        Assert.Equal(("Agente não encontrado.", "Equipamento não encontrado."), (unknownAgent.Message, unknownEquipment.Message));
    }

    [Fact]
    public async Task End_OpenAssignment_EndsItManuallyKeepingTheHistory_RF016()
    {
        var agent = await _services.AgentService().CreateAsync(AgentRequest(), Ct);
        var patrol = await _services.EquipmentService().CreateAsync(PatrolRequest(), Ct);
        var assignment = await _services.AssignmentService().AssignAsync(agent.Id, new AssignEquipmentRequest { EquipmentId = patrol.Id }, Ct);
        _services.Time.Now = InventoryTestData.Now.AddHours(8);

        var ended = await _services.AssignmentService().EndAsync(agent.Id, assignment.Id, Ct);

        Assert.Equal(
            ((DateTimeOffset?)InventoryTestData.Now.AddHours(8), "admin", (ContractEndReason?)ContractEndReason.Ended),
            (ended.EndedAt, ended.EndedBy, ended.EndReason));
        Assert.Equal(assignment.StartedAt, ended.StartedAt);
        Assert.Single(_services.Inventory.Assignments);
        Assert.Null((await _services.AgentQueryService().GetDetailsAsync(agent.Id, Ct)).AssignedEquipment);
    }

    [Fact]
    public async Task End_AssignmentOfAnotherAgent_IsNotFound()
    {
        var agent = await _services.AgentService().CreateAsync(AgentRequest(), Ct);
        var other = await _services.AgentService().CreateAsync(AgentRequest("SMM-0102"), Ct);
        var patrol = await _services.EquipmentService().CreateAsync(PatrolRequest(), Ct);
        var assignment = await _services.AssignmentService().AssignAsync(agent.Id, new AssignEquipmentRequest { EquipmentId = patrol.Id }, Ct);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _services.AssignmentService().EndAsync(other.Id, assignment.Id, Ct));

        Assert.Equal("Vínculo não encontrado.", exception.Message);
        Assert.True(Assert.Single(_services.Inventory.Assignments).IsOpen);
    }

    [Fact]
    public async Task End_AlreadyEnded_IsConflict_D62()
    {
        var agent = await _services.AgentService().CreateAsync(AgentRequest(), Ct);
        var patrol = await _services.EquipmentService().CreateAsync(PatrolRequest(), Ct);
        var assignment = await _services.AssignmentService().AssignAsync(agent.Id, new AssignEquipmentRequest { EquipmentId = patrol.Id }, Ct);
        await _services.AssignmentService().EndAsync(agent.Id, assignment.Id, Ct);

        var exception = await Assert.ThrowsAsync<InventoryRuleException>(
            () => _services.AssignmentService().EndAsync(agent.Id, assignment.Id, Ct));

        Assert.Equal("O vínculo já foi encerrado e não volta a abrir.", exception.Message);
    }

    private (Organization Organization, OperationalBase Base) AnotherOrganizationWithBase()
    {
        var guard = Organization.Create("Guarda Civil Municipal", "GCM");
        var guardBase = OperationalBase.Create("Base da Guarda", guard, _services.NewLocation());
        _services.Inventory.Organizations.Add(guard);
        _services.Inventory.Bases.Add(guardBase);
        return (guard, guardBase);
    }

    private CreateAgentRequest AgentRequest(string registrationNumber = Registration) => new()
    {
        FullName = "Agente Fictício de Teste",
        DisplayName = "Carcará",
        RegistrationNumber = registrationNumber,
        Role = ContractAgentRole.TrafficAgent,
        OrganizationId = _services.Organization.Id,
        BaseId = _services.Base.Id,
    };

    private static UpdateAgentRequest UpdateFrom(CreateAgentRequest request, uint version) => new()
    {
        FullName = request.FullName,
        DisplayName = request.DisplayName,
        RegistrationNumber = request.RegistrationNumber,
        Role = request.Role,
        OrganizationId = request.OrganizationId,
        Rank = request.Rank,
        BaseId = request.BaseId,
        Version = version,
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

    private CreateEquipmentRequest MotorcycleRequest() => new()
    {
        TypeId = _services.Type(EquipmentTypeCatalog.TrafficAgentMotorcycle).Id,
        SerialNumber = "9C2KC1670FR500123",
        Brand = "Orbis",
        Model = "Moto 300",
        OrganizationId = _services.Organization.Id,
        BaseId = _services.Base.Id,
        Attributes = new() { ["licensePlate"] = "MTO1A23", ["prefix"] = "SMM-07" },
        Status = ContractStatus.Operational,
    };
}
