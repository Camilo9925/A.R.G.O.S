using System.Net;
using Argos.Contracts.Access;
using Argos.Contracts.Common;
using Argos.Contracts.Inventory;
using Argos.IntegrationTests.Infrastructure;
using static Argos.IntegrationTests.Infrastructure.InventoryHttp;

namespace Argos.IntegrationTests.Inventory;

/// <summary>
/// Agentes e vínculos pela API (T097; US-06, US-07, US-09, RF-019, RN-12, RN-22, RN-25, RN-35; D-57, D-62, D-68; plan
/// 15.2.3, 15.16): matrícula repetida, erros por campo, perfis, 404, troca e encerramento de vínculos, recusas com o
/// motivo, a lista de vinculáveis equivalente ao <c>Assign</c> (A-20) e a base com agente ativo (A-19). O caminho feliz
/// encadeado de cada rota está em <see cref="PhaseThreeEndpointsTests"/>.
/// </summary>
public sealed class AgentEndpointsTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    private const string NotFound = "Agente não encontrado.";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // Cadastro (US-06, RN-25)

    [Fact]
    public async Task Create_RegistrationNumberRepeatedInTheOrganization_EvenOfAnInactive_IsConflict_US06()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var inactive = await client.CreateAgentAsync(scenario.Agent("AT-0001"), Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, (await InactivateAsync(client, inactive.Id)).StatusCode);
        var other = await client.CreateAgentAsync(scenario.Agent("AT-0002", "Gavião"), Cancellation);

        var created = await client.PostJsonAsync(Agents, scenario.Agent("AT-0001", "Condor"), Cancellation);
        var updated = await client.PutJsonAsync($"{Agents}/{other.Id}", Update(scenario, "AT-0001", other.Version), Cancellation);

        await ProblemAssert.IsConflictAsync(created, "Já existe um agente com essa matrícula neste órgão.", Cancellation);
        await ProblemAssert.IsConflictAsync(updated, "Já existe um agente com essa matrícula neste órgão.", Cancellation);
        Assert.Equal("AT-0002", (await client.GetAgentAsync(other.Id, Cancellation)).RegistrationNumber);
    }

    [Fact]
    public async Task Create_WithoutTheRequiredFields_IsBadRequestOnEachField_RN25()
    {
        using var client = Client(UserRoles.Administrator);

        var response = await client.PostJsonAsync(
            Agents, new { fullName = (string?)null, displayName = (string?)null, registrationNumber = (string?)null }, Cancellation);

        var errors = await ProblemAssert.IsValidationAsync(response, Cancellation);
        Assert.Equal(
            ["baseId", "displayName", "fullName", "organizationId", "registrationNumber", "role"],
            errors.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["Informe a matrícula."], errors["registrationNumber"]);
        Assert.Equal(["Escolha a função do agente."], errors["role"]);
        Assert.Equal(["Escolha a base de atuação do agente."], errors["baseId"]);
    }

    [Fact]
    public async Task Search_ByRegistrationNumberAndByNameWithoutAccents_UsesTheStoredSearchText_US06()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Manager);
        Guid falcon;
        Guid hawk;
        using (var admin = Client(UserRoles.Administrator))
        {
            falcon = (await admin.CreateAgentAsync(scenario.Agent("AT-0001", "Falcão"), Cancellation)).Id;
            hawk = (await admin.CreateAgentAsync(scenario.Agent("PM-0420", "Gavião"), Cancellation)).Id;
        }

        Assert.Equal([hawk], await SearchAgentIdsAsync(client, "?search=pm-04"));
        Assert.Equal([falcon], await SearchAgentIdsAsync(client, "?search=0001"));
        Assert.Equal([falcon], await SearchAgentIdsAsync(client, "?search=FALCAO"));
        Assert.Equal([hawk], await SearchAgentIdsAsync(client, "?search=gaviao"));
        Assert.Equal([falcon, hawk], await SearchAgentIdsAsync(client, "?search=ficticio&sort=registrationNumber"));
    }

    [Fact]
    public async Task InactiveAgent_LeavesTheDefaultList_AndAppearsInInactiveAndAll_US09()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var active = await client.CreateAgentAsync(scenario.Agent("AT-0001"), Cancellation);
        var inactive = await client.CreateAgentAsync(scenario.Agent("AT-0002"), Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, (await InactivateAsync(client, inactive.Id)).StatusCode);

        Assert.Equal([active.Id], await SearchAgentIdsAsync(client, string.Empty));
        Assert.Equal([inactive.Id], await SearchAgentIdsAsync(client, "?activity=inactive"));
        Assert.Equal([active.Id, inactive.Id], await SearchAgentIdsAsync(client, "?activity=all&sort=registrationNumber"));
    }

    // Perfis e 404 (D-58, Art. XII.4)

    [Fact]
    public async Task Manager_ReadsAgents_ButEveryWriteIsForbidden_D58()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        AgentDetails agent;
        Guid patrol;
        Guid assignment;
        using (var admin = Client(UserRoles.Administrator))
        {
            agent = await admin.CreateAgentAsync(scenario.Agent("AT-0001"), Cancellation);
            patrol = (await admin.CreateEquipmentAsync(scenario.Patrol("VTR-001"), Cancellation)).Id;
            assignment = (await (await admin.AssignAsync(agent.Id, patrol, Cancellation)).ReadAsync<AssignmentData>(Cancellation)).Id;
        }

        using var manager = Client(UserRoles.Manager);

        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync(Agents, Cancellation)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync($"{Agents}/{agent.Id}", Cancellation)).StatusCode);
        HttpResponseMessage[] writes =
        [
            await manager.PostJsonAsync(Agents, scenario.Agent("AT-0002"), Cancellation),
            await manager.PutJsonAsync($"{Agents}/{agent.Id}", Update(scenario, "AT-0001", agent.Version), Cancellation),
            await manager.PostJsonAsync(
                $"{Agents}/{agent.Id}/status-changes", new ChangeAgentStatusRequest { Status = AgentStatus.OnDuty }, Cancellation),
            await InactivateAsync(manager, agent.Id),
            await manager.AssignAsync(agent.Id, patrol, Cancellation),
            await manager.PostAsync($"{Agents}/{agent.Id}/assignments/{assignment}/end", content: null, Cancellation),
        ];
        foreach (var response in writes)
        {
            await ProblemAssert.IsForbiddenAsync(response, Cancellation);
        }

        var unchanged = await manager.GetAgentAsync(agent.Id, Cancellation);
        Assert.Equal((true, AgentStatus.OffDuty, assignment), (unchanged.IsActive, unchanged.Status, unchanged.AssignedEquipment?.AssignmentId));
    }

    [Fact]
    public async Task Operator_HasNoInventory_ButChangesTheAgentStatus_RN26()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        Guid agentId;
        using (var admin = Client(UserRoles.Administrator))
        {
            agentId = (await admin.CreateAgentAsync(scenario.Agent("AT-0001"), Cancellation)).Id;
        }

        using var operatorClient = Client(UserRoles.Operator);

        await ProblemAssert.IsForbiddenAsync(await operatorClient.GetAsync(Agents, Cancellation), Cancellation);
        await ProblemAssert.IsForbiddenAsync(await operatorClient.GetAsync($"{Agents}/{agentId}", Cancellation), Cancellation);
        await operatorClient.ChangeAgentStatusAsync(agentId, AgentStatus.OnDuty, Cancellation);
    }

    [Fact]
    public async Task UnknownAgentEquipmentOrAssignment_IsNotFound()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var agent = await client.CreateAgentAsync(scenario.Agent("AT-0001"), Cancellation);
        var patrol = await client.CreateEquipmentAsync(scenario.Patrol("VTR-001"), Cancellation);
        var unknown = Guid.NewGuid();

        await ProblemAssert.IsNotFoundAsync(await client.GetAsync($"{Agents}/{unknown}", Cancellation), NotFound, Cancellation);
        await ProblemAssert.IsNotFoundAsync(
            await client.PutJsonAsync($"{Agents}/{unknown}", Update(scenario, "AT-0009", 1), Cancellation), NotFound, Cancellation);
        await ProblemAssert.IsNotFoundAsync(
            await client.PostJsonAsync(
                $"{Agents}/{unknown}/status-changes", new ChangeAgentStatusRequest { Status = AgentStatus.OnDuty }, Cancellation),
            NotFound,
            Cancellation);
        await ProblemAssert.IsNotFoundAsync(await InactivateAsync(client, unknown), NotFound, Cancellation);
        await ProblemAssert.IsNotFoundAsync(await client.AssignAsync(unknown, patrol.Id, Cancellation), NotFound, Cancellation);
        await ProblemAssert.IsNotFoundAsync(
            await client.GetAsync($"{Agents}/{unknown}/timeline", Cancellation), NotFound, Cancellation);
        await ProblemAssert.IsNotFoundAsync(
            await client.AssignAsync(agent.Id, unknown, Cancellation), "Equipamento não encontrado.", Cancellation);
        await ProblemAssert.IsNotFoundAsync(
            await client.PostAsync($"{Agents}/{agent.Id}/assignments/{unknown}/end", content: null, Cancellation),
            "Vínculo não encontrado.",
            Cancellation);
        var otherBase = scenario.Agent("AT-0009");
        otherBase.BaseId = unknown;
        await ProblemAssert.IsNotFoundAsync(
            await client.PostJsonAsync(Agents, otherBase, Cancellation), "Base operacional não encontrada.", Cancellation);
    }

    // Vínculos (US-07, RN-35; D-62)

    [Fact]
    public async Task Assign_ToAnotherEquipment_EndsThePreviousAssignment_US07()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var agent = await client.CreateAgentAsync(scenario.Agent("AT-0001"), Cancellation);
        var patrol = await client.CreateEquipmentAsync(scenario.Patrol("VTR-001"), Cancellation);
        var motorcycle = await client.CreateEquipmentAsync(scenario.Motorcycle("MOT-001"), Cancellation);
        Assert.Equal(HttpStatusCode.Created, (await client.AssignAsync(agent.Id, patrol.Id, Cancellation)).StatusCode);

        var response = await client.AssignAsync(agent.Id, motorcycle.Id, Cancellation);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location);
        var started = await response.ReadAsync<AssignmentData>(Cancellation);
        Assert.Equal((agent.Id, motorcycle.Id, (DateTimeOffset?)null), (started.AgentId, started.EquipmentId, started.EndedAt));
        Assert.Empty((await client.GetEquipmentAsync(patrol.Id, Cancellation)).AssignedAgents);
        Assert.Equal([agent.Id], (await client.GetEquipmentAsync(motorcycle.Id, Cancellation)).AssignedAgents.Select(item => item.AgentId));
        Assert.Equal(started.Id, (await client.GetAgentAsync(agent.Id, Cancellation)).AssignedEquipment?.AssignmentId);

        await ProblemAssert.IsBusinessRuleAsync(
            await client.AssignAsync(agent.Id, motorcycle.Id, Cancellation),
            "O agente já está vinculado a esse equipamento.",
            Cancellation);
    }

    [Theory]
    [InlineData(EquipmentStatus.Deactivated, "desativado")]
    [InlineData(EquipmentStatus.Stolen, "furtado")]
    public async Task Assign_ToDeactivatedOrStolen_IsRefusedWithTheReason_AndKeepsTheExistingAssignments_D62(
        EquipmentStatus status, string label)
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var crew = await client.CreateAgentAsync(scenario.Agent("AT-0001"), Cancellation);
        var newcomer = await client.CreateAgentAsync(scenario.Agent("AT-0002"), Cancellation);
        var patrol = await client.CreateEquipmentAsync(scenario.Patrol("VTR-001"), Cancellation);
        Assert.Equal(HttpStatusCode.Created, (await client.AssignAsync(crew.Id, patrol.Id, Cancellation)).StatusCode);
        await client.ChangeStatusAsync(patrol.Id, status, "Ocorrência registrada.", Cancellation);

        var response = await client.AssignAsync(newcomer.Id, patrol.Id, Cancellation);

        await ProblemAssert.IsBusinessRuleAsync(
            response, $"Não é possível vincular agentes a um equipamento {label}.", Cancellation);
        Assert.Equal([crew.Id], (await client.GetEquipmentAsync(patrol.Id, Cancellation)).AssignedAgents.Select(item => item.AgentId));
        Assert.Null((await client.GetAgentAsync(newcomer.Id, Cancellation)).AssignedEquipment);
    }

    [Fact]
    public async Task InactivatingTheAgent_EndsItsAssignment_RN35()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var agent = await client.CreateAgentAsync(scenario.Agent("AT-0001"), Cancellation);
        var patrol = await client.CreateEquipmentAsync(scenario.Patrol("VTR-001"), Cancellation);
        Assert.Equal(HttpStatusCode.Created, (await client.AssignAsync(agent.Id, patrol.Id, Cancellation)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await InactivateAsync(client, agent.Id)).StatusCode);

        Assert.Empty((await client.GetEquipmentAsync(patrol.Id, Cancellation)).AssignedAgents);
        var details = await client.GetAgentAsync(agent.Id, Cancellation);
        Assert.Equal((false, (AssignedEquipmentData?)null), (details.IsActive, details.AssignedEquipment));
        await ProblemAssert.IsBusinessRuleAsync(
            await client.AssignAsync(agent.Id, patrol.Id, Cancellation),
            "Agente inativo não pode ser vinculado a equipamentos.",
            Cancellation);
    }

    [Fact]
    public async Task InactivatingTheEquipment_EndsEveryAssignmentWithIt_RN35()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var first = await client.CreateAgentAsync(scenario.Agent("AT-0001"), Cancellation);
        var second = await client.CreateAgentAsync(scenario.Agent("AT-0002"), Cancellation);
        var patrol = await client.CreateEquipmentAsync(scenario.Patrol("VTR-001"), Cancellation);
        Assert.Equal(HttpStatusCode.Created, (await client.AssignAsync(first.Id, patrol.Id, Cancellation)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.AssignAsync(second.Id, patrol.Id, Cancellation)).StatusCode);
        await client.ChangeStatusAsync(patrol.Id, EquipmentStatus.Deactivated, "Baixa da frota.", Cancellation);

        var response = await client.PostAsync($"{Equipment}/{patrol.Id}/inactivation", content: null, Cancellation);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty((await client.GetEquipmentAsync(patrol.Id, Cancellation)).AssignedAgents);
        Assert.Null((await client.GetAgentAsync(first.Id, Cancellation)).AssignedEquipment);
        Assert.Null((await client.GetAgentAsync(second.Id, Cancellation)).AssignedEquipment);
    }

    [Fact]
    public async Task Inactivate_AgentOnDuty_IsBusinessRuleNamingTheStatus_AndKeepsTheAssignment_RN35()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var agent = await client.CreateAgentAsync(scenario.Agent("AT-0001"), Cancellation);
        var patrol = await client.CreateEquipmentAsync(scenario.Patrol("VTR-001"), Cancellation);
        Assert.Equal(HttpStatusCode.Created, (await client.AssignAsync(agent.Id, patrol.Id, Cancellation)).StatusCode);
        await client.ChangeAgentStatusAsync(agent.Id, AgentStatus.OnDuty, Cancellation);

        var response = await InactivateAsync(client, agent.Id);

        await ProblemAssert.IsBusinessRuleAsync(
            response,
            "Agente na situação Em serviço não pode ser inativado. A inativação só é possível nas situações Fora de serviço "
            + "ou Afastado.",
            Cancellation);
        var details = await client.GetAgentAsync(agent.Id, Cancellation);
        Assert.Equal((true, patrol.Id), (details.IsActive, details.AssignedEquipment?.EquipmentId));
    }

    // Equipamentos vinculáveis (A-20; D-62, D-68)

    /// <summary>
    /// A lista e o <c>Assign</c> dizem o mesmo: cada listado é aceito e cada excluído é recusado com o motivo. A viatura
    /// inativada está Ainda não instalado, situação que aceita vínculo, por isso só o filtro de ativos a exclui
    /// (refinamento da revisão).
    /// </summary>
    [Fact]
    public async Task AssignableEquipment_ListsExactlyWhatTheAssignmentAccepts_A20_D68()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var operational = await client.CreateEquipmentAsync(scenario.Patrol("VTR-OPE"), Cancellation);
        var malfunction = await client.CreateEquipmentAsync(scenario.Patrol("VTR-MAU"), Cancellation);
        await client.ChangeStatusAsync(malfunction.Id, EquipmentStatus.Malfunction, justification: null, Cancellation);
        var deactivated = await client.CreateEquipmentAsync(scenario.Patrol("VTR-DES"), Cancellation);
        await client.ChangeStatusAsync(deactivated.Id, EquipmentStatus.Deactivated, "Baixa da frota.", Cancellation);
        var stolen = await client.CreateEquipmentAsync(scenario.Patrol("VTR-FUR"), Cancellation);
        await client.ChangeStatusAsync(stolen.Id, EquipmentStatus.Stolen, "Boletim registrado.", Cancellation);
        var notInstalled = scenario.Patrol("VTR-INA");
        notInstalled.Status = EquipmentStatus.NotInstalled;
        var inactive = await client.CreateEquipmentAsync(notInstalled, Cancellation);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.PostAsync($"{Equipment}/{inactive.Id}/inactivation", content: null, Cancellation)).StatusCode);
        var motorcycle = await client.CreateEquipmentAsync(scenario.Motorcycle("MOT-001"), Cancellation);
        var scooter = await client.CreateEquipmentAsync(scenario.Scooter("PAT-001"), Cancellation);
        var taxi = await client.CreateEquipmentAsync(scenario.Taxi("TAX-001"), Cancellation);
        var camera = await client.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);

        var listed = await AssignableAsync(client, "?sort=serialNumber");

        Assert.Equal(3, listed.TotalCount);
        Assert.Equal([motorcycle.Id, malfunction.Id, operational.Id], listed.Items.Select(item => item.Id));

        (Guid Id, string Reason)[] refused =
        [
            (deactivated.Id, "Não é possível vincular agentes a um equipamento desativado."),
            (stolen.Id, "Não é possível vincular agentes a um equipamento furtado."),
            (inactive.Id, "Equipamento inativo não recebe vínculo de agentes."),
            (scooter.Id, "Equipamentos do tipo Patinete elétrico não recebem vínculo de agentes."),
            (taxi.Id, "Equipamentos do tipo Veículo de transporte não recebem vínculo de agentes."),
            (camera.Id, "Equipamentos do tipo Câmera de videomonitoramento não recebem vínculo de agentes."),
        ];
        var probe = await client.CreateAgentAsync(scenario.Agent("AT-0099"), Cancellation);
        foreach (var (id, reason) in refused)
        {
            await ProblemAssert.IsBusinessRuleAsync(await client.AssignAsync(probe.Id, id, Cancellation), reason, Cancellation);
        }

        var number = 0;
        foreach (var item in listed.Items)
        {
            var agent = await client.CreateAgentAsync(scenario.Agent($"AT-{++number:0000}"), Cancellation);
            Assert.Equal(HttpStatusCode.Created, (await client.AssignAsync(agent.Id, item.Id, Cancellation)).StatusCode);
        }
    }

    [Fact]
    public async Task AssignableEquipment_SearchesByTerm_Pages_AndRefusesAnInvalidSort_D68()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var first = await client.CreateEquipmentAsync(scenario.Patrol("VTR-001"), Cancellation);
        var second = await client.CreateEquipmentAsync(scenario.Patrol("VTR-002"), Cancellation);
        var motorcycle = await client.CreateEquipmentAsync(scenario.Motorcycle("MOT-001"), Cancellation);

        var byTerm = await AssignableAsync(client, "?search=vtr&sort=serialNumber");
        var firstPage = await AssignableAsync(client, "?sort=serialNumber&pageSize=2");
        var secondPage = await AssignableAsync(client, "?sort=serialNumber&pageSize=2&page=2");
        var invalid = await client.GetAsync($"{Agents}/assignable-equipment?sort=zzz", Cancellation);

        Assert.Equal((2, 3, 3), (byTerm.TotalCount, firstPage.TotalCount, secondPage.TotalCount));
        Assert.Equal([first.Id, second.Id], byTerm.Items.Select(item => item.Id));
        Assert.Equal([motorcycle.Id, first.Id], firstPage.Items.Select(item => item.Id));
        Assert.Equal([second.Id], secondPage.Items.Select(item => item.Id));
        var errors = await ProblemAssert.IsValidationAsync(invalid, Cancellation);
        Assert.Equal(["sort"], errors.Keys);
        Assert.Equal([$"Ordenação inválida. Use uma destas: {string.Join(", ", EquipmentSortFields.All)}."], errors["sort"]);
    }

    [Theory]
    [InlineData(UserRoles.Manager)]
    [InlineData(UserRoles.Operator)]
    public async Task AssignableEquipment_ByManagerOrOperator_IsForbidden_D58(string role)
    {
        using var client = Client(role);

        await ProblemAssert.IsForbiddenAsync(await client.GetAsync($"{Agents}/assignable-equipment", Cancellation), Cancellation);
    }

    // Base com agente ativo (A-19; RF-019, D-57)

    [Fact]
    public async Task InactivateBase_WithAnActiveAgentAndNoEquipment_IsRefusedUntilTheAgentIsInactivated_A19()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var created = await client.PostJsonAsync(
            "api/inventory/bases",
            new OperationalBaseRequest
            {
                Name = "Base Hespéria", OrganizationId = scenario.OrganizationId, Location = scenario.HesperiaLocation(),
            },
            Cancellation);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var baseId = (await created.ReadAsync<OperationalBaseData>(Cancellation)).Id;
        var request = scenario.Agent("AT-0001");
        request.BaseId = baseId;
        var agent = await client.CreateAgentAsync(request, Cancellation);

        var refused = await client.PostAsync($"api/inventory/bases/{baseId}/inactivation", content: null, Cancellation);

        await ProblemAssert.IsBusinessRuleAsync(
            refused, "A base tem equipamentos ou agentes ativos e não pode ser inativada.", Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, (await InactivateAsync(client, agent.Id)).StatusCode);
        var accepted = await client.PostAsync($"api/inventory/bases/{baseId}/inactivation", content: null, Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
    }

    private static Task<HttpResponseMessage> InactivateAsync(HttpClient client, Guid id) =>
        client.PostJsonAsync($"{Agents}/{id}/inactivation", new InactivationRequest { Reason = "Desligamento." }, Cancellation);

    private static async Task<PagedResult<EquipmentSummary>> AssignableAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"{Agents}/assignable-equipment{query}", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<PagedResult<EquipmentSummary>>(Cancellation);
    }

    private static async Task<Guid[]> SearchAgentIdsAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"{Agents}{query}", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return [.. (await response.ReadAsync<PagedResult<AgentSummary>>(Cancellation)).Items.Select(item => item.Id)];
    }

    private static UpdateAgentRequest Update(InventoryScenario scenario, string registrationNumber, uint version)
    {
        var request = scenario.Agent(registrationNumber);
        return new UpdateAgentRequest
        {
            FullName = request.FullName, DisplayName = request.DisplayName, RegistrationNumber = registrationNumber,
            Role = request.Role, OrganizationId = request.OrganizationId, BaseId = request.BaseId, Version = version,
        };
    }

    private HttpClient Client(string role) => Fixture.CreateUserClient(Fixture.Tokens.For(role));
}
