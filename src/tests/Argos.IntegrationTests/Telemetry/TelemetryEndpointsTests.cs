using System.Net;
using Argos.Contracts.Access;
using Argos.Contracts.Common;
using Argos.Contracts.Inventory;
using Argos.Contracts.Operations;
using Argos.Contracts.Telemetry;
using Argos.IntegrationTests.Infrastructure;
using static Argos.IntegrationTests.Infrastructure.InventoryHttp;

namespace Argos.IntegrationTests.Telemetry;

/// <summary>
/// Telemetria do PROMETHEUS pela API (T118, reduzido ao essencial; RF-023, RF-024, RN-12; D-34, D-71, D-73; plan 16.4,
/// 16.10, 18.4): descoberta só com ativos, uma ingestão em lote feliz e a recusa do perfil errado nas duas rotas. Os
/// demais casos de 16.13 ficam nos testes unitários do T111.
/// </summary>
public sealed class TelemetryEndpointsTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Resources_ListsOnlyActiveResources_RF023()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var admin = Fixture.CreateUserClient(Fixture.Tokens.For(UserRoles.Administrator));
        var patrol = await admin.CreateEquipmentAsync(scenario.Patrol("VTR-001"), Cancellation);
        var camera = await admin.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);
        var inactive = await admin.CreateEquipmentAsync(scenario.Camera("CAM-002", EquipmentStatus.NotInstalled), Cancellation);
        var inactivation = await admin.PostJsonAsync($"{Equipment}/{inactive.Id}/inactivation", new InactivationRequest(), Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, inactivation.StatusCode);
        var agent = await admin.CreateAgentAsync(scenario.Agent("AT-0001"), Cancellation);

        using var system = Fixture.CreateSystemClient();
        var response = await system.GetAsync(TelemetryHttp.ResourcesPath, Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var resources = await response.ReadAsync<TelemetryResourceList>(Cancellation);
        Assert.Equal(new[] { patrol.Id, camera.Id }.Order(), resources.Equipment.Select(item => item.Id).Order());
        var mobile = resources.Equipment.Single(item => item.Id == patrol.Id);
        Assert.Equal(("Rodaris/VTR-001", EquipmentCategory.Mobile, true), (mobile.Key, mobile.Category, mobile.CrewAssignable));
        Assert.NotNull(mobile.BaseRegion);
        Assert.Null(resources.Equipment.Single(item => item.Id == camera.Id).BaseRegion);
        var listed = Assert.Single(resources.Agents);
        Assert.Equal((agent.Id, "DMT/AT-0001", (DutyShift?)null, false), (listed.Id, listed.Key, listed.Shift, listed.ScheduledOnDuty));
    }

    [Fact]
    public async Task Batches_ValidBatch_UpdatesLiveStateRecordsReadingsAndAppliesReportedStatus_RN12()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var admin = Fixture.CreateUserClient(Fixture.Tokens.For(UserRoles.Administrator));
        var patrol = await admin.CreateEquipmentAsync(scenario.Patrol("VTR-001"), Cancellation);
        var camera = await admin.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);
        var agent = await admin.CreateAgentAsync(scenario.Agent("AT-0001"), Cancellation);
        // Depois do cadastro: a situação reportada é a mudança mais recente do agente (plan 16.4).
        var now = DateTimeOffset.UtcNow;

        using var system = Fixture.CreateSystemClient();
        var response = await system.PostJsonAsync(TelemetryHttp.BatchesPath, new TelemetryBatchRequest
        {
            SentAt = now,
            IsSimulated = true,
            AgentStatusReports = [new AgentStatusReportItem { AgentId = agent.Id, Status = AgentStatus.OnDuty, ReportedAt = now }],
            Positions =
            [
                new TelemetryPositionItem
                {
                    ResourceKind = ResourceKind.Agent, ResourceId = agent.Id, Latitude = -22.25, Longitude = -49.65, RecordedAt = now,
                },
                new TelemetryPositionItem
                {
                    ResourceKind = ResourceKind.Equipment, ResourceId = patrol.Id, Latitude = -22.25, Longitude = -49.65, RecordedAt = now,
                },
            ],
            Heartbeats = [new TelemetryHeartbeatItem { EquipmentId = camera.Id, RecordedAt = now }],
        }, Cancellation);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var result = await response.ReadAsync<TelemetryBatchResult>(Cancellation);
        Assert.Equal((1, 2, 1, 0), (result.StatusReportsApplied, result.PositionsAccepted, result.HeartbeatsAccepted, result.Ignored));

        Assert.Equal(3, await Fixture.CountAsync("SELECT count(*) FROM telemetry_readings WHERE author = 'PROMETHEUS'", Cancellation));
        Assert.Equal(1, await Fixture.CountAsync(
            $"SELECT count(*) FROM resource_live_states WHERE resource_kind = 'Equipment' AND resource_id = '{patrol.Id}' "
                + "AND latitude = -22.25 AND position_recorded_at IS NOT NULL",
            Cancellation));
        Assert.Equal(1, await Fixture.CountAsync(
            $"SELECT count(*) FROM resource_live_states WHERE resource_kind = 'Equipment' AND resource_id = '{camera.Id}' "
                + "AND position_recorded_at IS NULL",
            Cancellation));

        Assert.Equal(AgentStatus.OnDuty, (await admin.GetAgentAsync(agent.Id, Cancellation)).Status);
        var timeline = await (await admin.GetAsync($"{Agents}/{agent.Id}/timeline", Cancellation))
            .ReadAsync<PagedResult<TimelineEntry>>(Cancellation);
        var latest = timeline.Items[0];
        Assert.Equal(("Fora de serviço → Em serviço", "PROMETHEUS"), (latest.Title, latest.Author));
    }

    [Theory]
    [InlineData("GET", TelemetryHttp.ResourcesPath)]
    [InlineData("POST", TelemetryHttp.BatchesPath)]
    public async Task TelemetryRoutes_WithoutKeyOrWithUserToken_AreRefused_D34(string method, string path)
    {
        using var anonymous = Fixture.Factory.CreateClient();
        using var user = Fixture.CreateUserClient(Fixture.Tokens.For(UserRoles.Administrator));

        await ProblemAssert.IsUnauthenticatedAsync(await SendAsync(anonymous, method, path), Cancellation);
        await ProblemAssert.IsForbiddenAsync(await SendAsync(user, method, path), Cancellation);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string path) =>
        method == "GET"
            ? client.GetAsync(path, Cancellation)
            : client.PostJsonAsync(path, TelemetryHttp.EmptyBatch(), Cancellation);
}
