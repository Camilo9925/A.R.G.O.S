using System.Net;
using System.Text.Json;
using Argos.Contracts.Access;
using Argos.Contracts.Common;
using Argos.Contracts.Inventory;
using Argos.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using static Argos.IntegrationTests.Infrastructure.InventoryHttp;

namespace Argos.IntegrationTests.Inventory;

/// <summary>
/// Rotas da Fase 3 (T089 a T091; US-06 a US-09, RF-011, RF-018, RN-35; D-58, D-68; plan 15.14): o documento OpenAPI
/// (Scalar) com as rotas novas e sem <c>DELETE</c>, o caminho feliz de cada endpoint novo e a linha do tempo para os
/// três perfis (<c>history.view</c>). Os casos de erro e de regra ficam nos testes da Onda 7 (T096 a T098).
/// </summary>
public sealed class PhaseThreeEndpointsTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OpenApiDocument_ListsThePhaseThreeRoutes_WithoutDelete_D58()
    {
        // O documento e o Scalar só existem no ambiente Demo (D-12).
        await using var demo = Fixture.Factory.WithWebHostBuilder(builder => builder.UseEnvironment("Demo"));
        using var client = demo.CreateClient();

        var response = await client.GetAsync("openapi/v1.json", Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        var paths = document.RootElement.GetProperty("paths");

        (string Path, string Method)[] expected =
        [
            ("/api/inventory/equipment/{id}/inactivation", "post"),
            ("/api/inventory/equipment/{id}/timeline", "get"),
            ("/api/inventory/agents", "get"),
            ("/api/inventory/agents", "post"),
            ("/api/inventory/agents/assignable-equipment", "get"),
            ("/api/inventory/agents/{id}", "get"),
            ("/api/inventory/agents/{id}", "put"),
            ("/api/inventory/agents/{id}/status-changes", "post"),
            ("/api/inventory/agents/{id}/inactivation", "post"),
            ("/api/inventory/agents/{id}/assignments", "post"),
            ("/api/inventory/agents/{id}/assignments/{assignmentId}/end", "post"),
            ("/api/inventory/agents/{id}/timeline", "get"),
        ];
        Assert.All(expected, route =>
        {
            Assert.True(paths.TryGetProperty(route.Path, out var item), $"Rota ausente: {route.Path}.");
            Assert.True(item.TryGetProperty(route.Method, out _), $"Método ausente: {route.Method} {route.Path}.");
        });

        Assert.Equal(
            ["direction", "page", "pageSize", "search", "sort"],
            QueryParameters(paths, "/api/inventory/agents/assignable-equipment").Order(StringComparer.Ordinal));
        Assert.Contains("activity", QueryParameters(paths, "/api/inventory/equipment"));
        Assert.Contains("activity", QueryParameters(paths, "/api/inventory/agents"));
        // A linha do tempo recebe PageQuery, como o histórico de situação: só page e pageSize valem (ordem fixa).
        Assert.Contains("page", QueryParameters(paths, "/api/inventory/agents/{id}/timeline"));
        Assert.Contains("pageSize", QueryParameters(paths, "/api/inventory/equipment/{id}/timeline"));
        Assert.DoesNotContain(
            paths.EnumerateObject(),
            path => path.Name.StartsWith("/api/inventory", StringComparison.Ordinal) && path.Value.TryGetProperty("delete", out _));
    }

    [Fact]
    public async Task EquipmentInactivation_WithoutBody_Returns204_AndTheEquipmentMovesToInactive_US09()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var camera = await client.CreateEquipmentAsync(scenario.Camera("CAM-001", EquipmentStatus.NotInstalled), Cancellation);

        var response = await client.PostAsync($"{Equipment}/{camera.Id}/inactivation", content: null, Cancellation);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False((await client.GetEquipmentAsync(camera.Id, Cancellation)).IsActive);
        var inactive = await (await client.GetAsync($"{Equipment}?activity=inactive", Cancellation))
            .ReadAsync<PagedResult<EquipmentSummary>>(Cancellation);
        Assert.Equal([camera.Id], inactive.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task AgentRoutes_HappyPath_RegisterEditAssignEndChangeStatusAndInactivate_US06_US07_US09()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var patrol = await client.CreateEquipmentAsync(scenario.Patrol("VTR-001"), Cancellation);

        var created = await client.PostJsonAsync(Agents, scenario.Agent("AT-0001"), Cancellation);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var agent = await created.ReadAsync<AgentDetails>(Cancellation);
        Assert.EndsWith($"/{Agents}/{agent.Id}", created.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);

        var request = scenario.Agent("AT-0001", "Condor");
        var updated = await client.PutJsonAsync(
            $"{Agents}/{agent.Id}",
            new UpdateAgentRequest
            {
                FullName = request.FullName, DisplayName = request.DisplayName, RegistrationNumber = request.RegistrationNumber,
                Role = request.Role, OrganizationId = request.OrganizationId, BaseId = request.BaseId, Version = agent.Version,
            },
            Cancellation);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Condor", (await updated.ReadAsync<AgentDetails>(Cancellation)).DisplayName);

        var search = await (await client.GetAsync($"{Agents}?search=at-0001", Cancellation))
            .ReadAsync<PagedResult<AgentSummary>>(Cancellation);
        Assert.Equal([agent.Id], search.Items.Select(item => item.Id));

        var assignable = await (await client.GetAsync($"{Agents}/assignable-equipment?search=vtr", Cancellation))
            .ReadAsync<PagedResult<EquipmentSummary>>(Cancellation);
        Assert.Equal([patrol.Id], assignable.Items.Select(item => item.Id));

        var assigned = await client.PostJsonAsync(
            $"{Agents}/{agent.Id}/assignments", new AssignEquipmentRequest { EquipmentId = patrol.Id }, Cancellation);
        Assert.Equal(HttpStatusCode.Created, assigned.StatusCode);
        var assignment = await assigned.ReadAsync<AssignmentData>(Cancellation);
        Assert.Equal(
            [agent.Id], (await client.GetEquipmentAsync(patrol.Id, Cancellation)).AssignedAgents.Select(item => item.AgentId));

        var ended = await client.PostAsync($"{Agents}/{agent.Id}/assignments/{assignment.Id}/end", content: null, Cancellation);
        Assert.Equal(HttpStatusCode.OK, ended.StatusCode);
        Assert.NotNull((await ended.ReadAsync<AssignmentData>(Cancellation)).EndedAt);

        var changed = await client.PostJsonAsync(
            $"{Agents}/{agent.Id}/status-changes", new ChangeAgentStatusRequest { Status = AgentStatus.OnLeave }, Cancellation);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(AgentStatus.OnLeave, (await changed.ReadAsync<AgentDetails>(Cancellation)).Status);

        var inactivated = await client.PostJsonAsync(
            $"{Agents}/{agent.Id}/inactivation", new InactivationRequest { Reason = "Desligamento." }, Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, inactivated.StatusCode);
        var details = await (await client.GetAsync($"{Agents}/{agent.Id}", Cancellation)).ReadAsync<AgentDetails>(Cancellation);
        Assert.False(details.IsActive);
    }

    [Theory]
    [InlineData(UserRoles.Administrator)]
    [InlineData(UserRoles.Operator)]
    [InlineData(UserRoles.Manager)]
    public async Task Timelines_RespondOkToTheThreeProfiles_US08_D58(string role)
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        Guid equipmentId;
        Guid agentId;
        using (var admin = Client(UserRoles.Administrator))
        {
            equipmentId = (await admin.CreateEquipmentAsync(scenario.Patrol("VTR-001"), Cancellation)).Id;
            agentId = (await (await admin.PostJsonAsync(Agents, scenario.Agent("AT-0001"), Cancellation))
                .ReadAsync<AgentDetails>(Cancellation)).Id;
        }

        using var client = Client(role);
        var equipment = await client.GetAsync($"{Equipment}/{equipmentId}/timeline?page=1&pageSize=10", Cancellation);
        var agent = await client.GetAsync($"{Agents}/{agentId}/timeline?page=1&pageSize=10", Cancellation);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (equipment.StatusCode, agent.StatusCode));
        Assert.NotEmpty((await equipment.ReadAsync<PagedResult<TimelineEntry>>(Cancellation)).Items);
        Assert.NotEmpty((await agent.ReadAsync<PagedResult<TimelineEntry>>(Cancellation)).Items);
    }

    private static string[] QueryParameters(JsonElement paths, string path) =>
        [.. paths.GetProperty(path).GetProperty("get").GetProperty("parameters").EnumerateArray()
            .Where(parameter => parameter.GetProperty("in").GetString() == "query")
            .Select(parameter => parameter.GetProperty("name").GetString()!)];

    private HttpClient Client(string role) => Fixture.CreateUserClient(Fixture.Tokens.For(role));
}
