using Argos.Application.Common.Abstractions;
using Argos.Application.Common.Exceptions;
using Argos.Application.Inventory.Agents;
using Argos.Application.Inventory.Equipments;
using Argos.Application.Inventory.Support;
using Argos.Application.Inventory.Timeline;
using Argos.Application.Inventory.Types;
using Argos.Application.Seeding.Inventory;
using Argos.Contracts.Common;
using Argos.Contracts.Inventory;
using Argos.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using DomainAgent = Argos.Domain.Inventory.Agents.Agent;
using DomainAgentRole = Argos.Domain.Inventory.Agents.AgentRole;
using DomainAssignmentPolicy = Argos.Domain.Inventory.Agents.AssignmentPolicy;

namespace Argos.IntegrationTests.Inventory;

/// <summary>
/// Persistência dos agentes, dos vínculos, da linha do tempo e do filtro de inativos contra o PostgreSQL real (T086;
/// US-06 a US-09, RF-019; D-60, D-62, D-68; plan 15.13): índices únicos traduzidos em 409, tradução da
/// <c>AssignmentPolicy</c> na consulta dos vinculáveis e agente ativo na base (A-19). Os casos de uso são chamados
/// direto pela DI da API; os endpoints entram na Onda 5.
/// </summary>
public sealed class AgentPersistenceTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PhaseThreeServices_AreResolvedFromTheContainer_T086()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        Type[] registered =
        [
            typeof(EquipmentInactivationService), typeof(AgentQueryService), typeof(AgentService), typeof(AgentStatusService),
            typeof(AssignmentService), typeof(TimelineService), typeof(OperationalBaseService),
            typeof(AgentCatalogSeeder), typeof(InactivationSeeder),
        ];

        Assert.All(registered, type => Assert.NotNull(services.GetRequiredService(type)));
    }

    [Fact]
    public async Task EquipmentDetails_ListTheAgentsWithOpenAssignment_OrEmpty_US07()
    {
        var scenario = await ScenarioAsync();
        var patrol = await CreateEquipmentAsync(scenario.Patrol("VTR-001"));
        var motorcycle = await CreateEquipmentAsync(scenario.Motorcycle("MOTO-001"));
        var first = await CreateAgentAsync(scenario.Agent("AT-0001", "Falcão"));
        var second = await CreateAgentAsync(scenario.Agent("AT-0002", "Condor"));
        await AssignAsync(first.Id, patrol.Id);
        await AssignAsync(second.Id, patrol.Id);

        var details = await UseAsync<EquipmentQueryService, EquipmentDetails>(
            service => service.GetDetailsAsync(patrol.Id, Cancellation));
        var withoutCrew = await UseAsync<EquipmentQueryService, EquipmentDetails>(
            service => service.GetDetailsAsync(motorcycle.Id, Cancellation));

        Assert.Equal(["Condor", "Falcão"], details.AssignedAgents.Select(agent => agent.DisplayName));
        Assert.Equal([second.Id, first.Id], details.AssignedAgents.Select(agent => agent.AgentId));
        Assert.All(details.AssignedAgents, agent => Assert.Equal(AgentStatus.OffDuty, agent.Status));
        Assert.Empty(withoutCrew.AssignedAgents);
    }

    [Fact]
    public async Task Reassign_EndsThePreviousAndStartsTheNewInOneSave_AndAppearsInTheTimeline_US07_US08()
    {
        var scenario = await ScenarioAsync();
        var patrol = await CreateEquipmentAsync(scenario.Patrol("VTR-002"));
        var motorcycle = await CreateEquipmentAsync(scenario.Motorcycle("MOTO-002"));
        var agent = await CreateAgentAsync(scenario.Agent("AT-0003"));
        await AssignAsync(agent.Id, patrol.Id);

        var moved = await AssignAsync(agent.Id, motorcycle.Id);

        var details = await UseAsync<AgentQueryService, AgentDetails>(service => service.GetDetailsAsync(agent.Id, Cancellation));
        Assert.Equal(moved.Id, details.AssignedEquipment!.AssignmentId);
        Assert.Equal(motorcycle.Id, details.AssignedEquipment.EquipmentId);
        Assert.Empty((await UseAsync<EquipmentQueryService, EquipmentDetails>(
            service => service.GetDetailsAsync(patrol.Id, Cancellation))).AssignedAgents);

        var timeline = await UseAsync<TimelineService, PagedResult<TimelineEntry>>(
            service => service.ForAgentAsync(agent.Id, new PageQuery(), Cancellation));
        Assert.Equal(
            [TimelineEntryKind.CrewAssigned, TimelineEntryKind.CrewUnassigned, TimelineEntryKind.CrewAssigned, TimelineEntryKind.Registered],
            timeline.Items.Select(entry => entry.Kind));
        Assert.Equal("Agente vinculado a outro equipamento", timeline.Items[1].Details.Single().Value);
    }

    [Fact]
    public async Task TwoOpenAssignmentsForTheSameAgent_AreRefusedByTheDatabase_US07()
    {
        var scenario = await ScenarioAsync();
        var patrol = await CreateEquipmentAsync(scenario.Patrol("VTR-003"));
        var motorcycle = await CreateEquipmentAsync(scenario.Motorcycle("MOTO-003"));
        var agent = await CreateAgentAsync(scenario.Agent("AT-0004"));
        await AssignAsync(agent.Id, patrol.Id);

        // Como uma segunda gravação simultânea, que não viu o vínculo aberto: a política recebe "nenhum vínculo atual".
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var loadedAgent = await services.GetRequiredService<IAgentRepository>().FindByIdAsync(agent.Id, Cancellation);
        var loadedMotorcycle = await services.GetRequiredService<IEquipmentRepository>().FindByIdAsync(motorcycle.Id, Cancellation);
        var type = await services.GetRequiredService<IEquipmentTypeRepository>().FindByIdAsync(loadedMotorcycle!.TypeId, Cancellation);
        var change = DomainAssignmentPolicy.Assign(loadedAgent!, loadedMotorcycle, type!, null, DateTimeOffset.UtcNow, "teste");
        services.GetRequiredService<IAgentAssignmentRepository>().Add(change.Started);

        var conflict = await Assert.ThrowsAsync<ConflictException>(
            () => services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Cancellation));

        // A mensagem genérica do UnitOfWork prova que o 409 veio do índice único, não de uma regra da Aplicação.
        Assert.Equal("Já existe um registro com esses dados.", conflict.Message);

        var details = await UseAsync<AgentQueryService, AgentDetails>(service => service.GetDetailsAsync(agent.Id, Cancellation));
        Assert.Equal(patrol.Id, details.AssignedEquipment!.EquipmentId);
    }

    [Fact]
    public async Task DuplicateRegistration_CaughtByUniqueIndex_ReturnsConflictWithTheServiceMessage_US06()
    {
        var scenario = await ScenarioAsync();
        await CreateAgentAsync(scenario.Agent("SMM-0101"));

        // Grava direto pelas portas, sem a verificação da Aplicação, como a segunda de duas gravações simultâneas.
        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var organization = await services.GetRequiredService<IOrganizationRepository>()
                .FindByIdAsync(scenario.OrganizationId, Cancellation);
            var operationalBase = await services.GetRequiredService<IOperationalBaseRepository>()
                .FindByIdAsync(scenario.BaseId, Cancellation);
            services.GetRequiredService<IAgentRepository>().Add(DomainAgent.Register(
                "Outro Agente Fictício", "Gavião", "smm-0101", DomainAgentRole.TrafficAgent, organization, null,
                operationalBase, DateTimeOffset.UtcNow, "teste"));

            var conflict = await Assert.ThrowsAsync<ConflictException>(
                () => services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Cancellation));

            Assert.Equal("Já existe um agente com essa matrícula neste órgão.", conflict.Message);
            Assert.Equal(AgentService.DuplicateRegistrationMessage, conflict.Message);
        }

        var all = await UseAsync<AgentQueryService, PagedResult<AgentSummary>>(
            service => service.SearchAsync(new AgentSearchQuery { Activity = ActivityFilter.All }, Cancellation));
        Assert.Equal(["SMM-0101"], all.Items.Select(agent => agent.RegistrationNumber));
    }

    [Fact]
    public async Task AssignableEquipment_OnlyActiveMobileCrewTypesOutsideRefusedStatuses_US07_D62_D68()
    {
        var scenario = await ScenarioAsync();
        var operational = await CreateEquipmentAsync(scenario.Patrol("VTR-OPER"));
        var deactivated = await CreateEquipmentAsync(scenario.Patrol("VTR-DESAT"));
        await DeactivateAsync(deactivated.Id);
        await CreateEquipmentAsync(scenario.Scooter("PAT-001"));
        // Inativada em Ainda não instalado, sem passar por Desativado: só o filtro de ativos a tira da lista, e não o
        // da situação (Ainda não instalado aceita vínculo, D-62).
        var inactivated = await CreateEquipmentAsync(scenario.Patrol("VTR-INAT") with { Status = EquipmentStatus.NotInstalled });
        await UseAsync<EquipmentInactivationService, bool>(async service =>
        {
            await service.InactivateAsync(inactivated.Id, new InactivationRequest { Reason = "Baixa patrimonial." }, Cancellation);
            return true;
        });

        var assignable = await UseAsync<AgentQueryService, PagedResult<EquipmentSummary>>(
            service => service.SearchAssignableEquipmentAsync(new AssignableEquipmentQuery(), Cancellation));

        Assert.Equal(1, assignable.TotalCount);
        Assert.Equal([operational.Id], assignable.Items.Select(item => item.Id));

        var bySearch = await UseAsync<AgentQueryService, PagedResult<EquipmentSummary>>(
            service => service.SearchAssignableEquipmentAsync(new AssignableEquipmentQuery { Search = "vtr-desat" }, Cancellation));
        Assert.Equal(0, bySearch.TotalCount);
    }

    [Fact]
    public async Task EquipmentSearch_ActivityFilter_SeparatesActiveAndInactive_US09()
    {
        var scenario = await ScenarioAsync();
        var active = await CreateEquipmentAsync(scenario.Camera("CAM-ATIVA"));
        var inactive = await CreateEquipmentAsync(scenario.Camera("CAM-INATIVA", EquipmentStatus.NotInstalled));
        await UseAsync<EquipmentInactivationService, bool>(async service =>
        {
            await service.InactivateAsync(inactive.Id, new InactivationRequest(), Cancellation);
            return true;
        });

        async Task<IEnumerable<Guid>> IdsAsync(ActivityFilter activity) =>
            (await UseAsync<EquipmentQueryService, PagedResult<EquipmentSummary>>(service => service.SearchAsync(
                new EquipmentSearchQuery { Activity = activity }, Cancellation))).Items.Select(item => item.Id).Order();

        Assert.Equal([active.Id], await IdsAsync(ActivityFilter.Active));
        Assert.Equal([inactive.Id], await IdsAsync(ActivityFilter.Inactive));
        Assert.Equal(new[] { active.Id, inactive.Id }.Order(), await IdsAsync(ActivityFilter.All));
    }

    [Fact]
    public async Task AnyActiveInBase_TrueWithActiveAgent_FalseAfterItsInactivation_RF019_RN35()
    {
        var scenario = await ScenarioAsync();
        var agent = await CreateAgentAsync(scenario.Agent("AT-0005"));

        Assert.True(await AnyActiveInBaseAsync(scenario.BaseId));

        await UseAsync<AgentService, bool>(async service =>
        {
            await service.InactivateAsync(agent.Id, new InactivationRequest { Reason = "Desligado." }, Cancellation);
            return true;
        });

        Assert.False(await AnyActiveInBaseAsync(scenario.BaseId));
    }

    private Task<InventoryScenario> ScenarioAsync() => InventoryScenario.CreateAsync(Fixture, Cancellation);

    private Task<EquipmentDetails> CreateEquipmentAsync(CreateEquipmentRequest request) =>
        UseAsync<EquipmentService, EquipmentDetails>(service => service.CreateAsync(request, Cancellation));

    private Task<AgentDetails> CreateAgentAsync(CreateAgentRequest request) =>
        UseAsync<AgentService, AgentDetails>(service => service.CreateAsync(request, Cancellation));

    private Task<AssignmentData> AssignAsync(Guid agentId, Guid equipmentId) =>
        UseAsync<AssignmentService, AssignmentData>(service => service.AssignAsync(
            agentId, new AssignEquipmentRequest { EquipmentId = equipmentId }, Cancellation));

    private Task<EquipmentDetails> DeactivateAsync(Guid equipmentId) =>
        UseAsync<EquipmentStatusService, EquipmentDetails>(service => service.ChangeAsync(
            equipmentId,
            new ChangeEquipmentStatusRequest { Status = EquipmentStatus.Deactivated, Justification = "Fim de vida útil." },
            Cancellation));

    private Task<bool> AnyActiveInBaseAsync(Guid baseId) =>
        UseAsync<IAgentRepository, bool>(repository => repository.AnyActiveInBaseAsync(baseId, Cancellation));

    /// <summary>Executa um caso de uso num escopo próprio, como uma requisição.</summary>
    private async Task<TResult> UseAsync<TService, TResult>(Func<TService, Task<TResult>> action)
        where TService : notnull
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }
}
