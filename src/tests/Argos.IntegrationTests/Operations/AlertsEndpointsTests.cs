using System.Net;
using Argos.Application.Seeding.Inventory;
using Argos.Application.Seeding.Operations;
using Argos.Contracts.Access;
using Argos.Contracts.Inventory;
using Argos.Contracts.Operations;
using DutyShift = Argos.Domain.Operations.DutyShift;
using Argos.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using DomainAgentRole = Argos.Domain.Inventory.Agents.AgentRole;
using DomainEquipmentStatus = Argos.Domain.Inventory.Equipments.EquipmentStatus;
using EquipmentTypeCatalog = Argos.Domain.Inventory.Types.EquipmentTypeCatalog;

namespace Argos.IntegrationTests.Operations;

/// <summary>
/// Alertas, cargas e ausência de exclusão da Central pela API (T121, reduzido ao essencial; US-15, RN-18 a RN-20,
/// RN-23; D-78, D-94; plan 16.6, 16.13, 18.4). Os demais casos ficam nos unitários do T107 e do T112 e no T128.
/// </summary>
public sealed class AlertsEndpointsTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    private const int Seed = 42;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Malfunction_Maintenance_Malfunction_KeepsOneHighAlertWithTwoOccurrences_RN19()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var admin = Fixture.CreateUserClient(Fixture.Tokens.For(UserRoles.Administrator));
        var camera = await admin.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);

        await admin.ChangeStatusAsync(camera.Id, EquipmentStatus.Malfunction, null, Cancellation);
        await admin.ChangeStatusAsync(camera.Id, EquipmentStatus.UnderMaintenance, null, Cancellation);
        await admin.ChangeStatusAsync(camera.Id, EquipmentStatus.Malfunction, null, Cancellation);

        var snapshot = await SnapshotAsync(admin);
        var alert = Assert.Single(snapshot.Alerts);
        Assert.Equal(
            (camera.Id, AlertCause.EquipmentMalfunction, AlertSeverity.High, AlertState.Open, 2),
            (alert.ResourceId, alert.Cause, alert.Severity, alert.State, alert.OccurrenceCount));
        Assert.Equal(1, await Fixture.CountAsync("SELECT count(*) FROM alerts", Cancellation));
    }

    [Fact]
    public async Task Stolen_OpensCriticalThatIsResolvedOnlyAfterAcknowledgement_RN18_RN20()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var admin = Fixture.CreateUserClient(Fixture.Tokens.For(UserRoles.Administrator));
        var camera = await admin.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);
        await admin.ChangeStatusAsync(camera.Id, EquipmentStatus.Stolen, "Furtada na madrugada.", Cancellation);
        var alert = Assert.Single((await SnapshotAsync(admin)).Alerts);
        Assert.Equal(AlertSeverity.Critical, alert.Severity);
        Assert.Equal([AlertAction.Acknowledge], alert.AllowedActions);

        using var @operator = Fixture.CreateUserClient(Fixture.Tokens.For(UserRoles.Operator));
        var resolution = new ResolveAlertRequest { Notes = "Câmera recuperada pela guarda." };

        var refused = await @operator.PostJsonAsync(AlertPath(alert.Id, "resolution"), resolution, Cancellation);
        await ProblemAssert.IsBusinessRuleAsync(refused, "Alertas críticos precisam ser reconhecidos antes de serem resolvidos.", Cancellation);

        var acknowledged = await @operator.PostJsonAsync(AlertPath(alert.Id, "acknowledgement"), new { }, Cancellation);
        Assert.Equal(HttpStatusCode.OK, acknowledged.StatusCode);
        var afterAcknowledgement = await acknowledged.ReadAsync<AlertSummary>(Cancellation);
        Assert.Equal((AlertState.Acknowledged, "operador"), (afterAcknowledgement.State, afterAcknowledgement.AcknowledgedBy));
        Assert.Equal([AlertAction.Resolve], afterAcknowledgement.AllowedActions);

        var resolved = await @operator.PostJsonAsync(AlertPath(alert.Id, "resolution"), resolution, Cancellation);
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        Assert.Equal(AlertState.Resolved, (await resolved.ReadAsync<AlertSummary>(Cancellation)).State);
        Assert.Empty((await SnapshotAsync(admin)).Alerts);
    }

    [Fact]
    public async Task DutyRosterAndAlertLoads_RunTwice_WriteOnce_D78_D94()
    {
        await SeedInventoryAsync();
        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var roster = await scope.ServiceProvider.GetRequiredService<DutyRosterSeeder>().SeedAsync(Roster(), Seed, InventorySeedData.Anchor, Cancellation);
            Assert.Equal(
                new Dictionary<string, int> { ["morning"] = 1, ["afternoon"] = 0, ["night"] = 1, ["shiftsSkipped"] = 1 },
                roster.Counts);
        }

        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var alerts = await scope.ServiceProvider.GetRequiredService<OperationsAlertSeeder>().SeedAsync(Seed, Cancellation);
            Assert.Equal(new Dictionary<string, int> { ["critical"] = 1, ["high"] = 1 }, alerts.Counts);
        }

        Assert.Equal(2, await Fixture.CountAsync("SELECT count(*) FROM agent_duty_shifts WHERE assigned_by = 'simulação'", Cancellation));
        Assert.Equal(2, await Fixture.CountAsync(
            "SELECT count(*) FROM alerts a JOIN equipment e ON e.id = a.resource_id "
                + "WHERE a.opened_by = 'simulação' AND a.opened_at = e.status_changed_at AND a.state = 'Open'",
            Cancellation));

        var before = await DatabaseFingerprint.TakeAsync(Fixture.ConnectionString, Cancellation);
        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            Assert.True((await scope.ServiceProvider.GetRequiredService<DutyRosterSeeder>()
                .SeedAsync(Roster(), Seed, InventorySeedData.Anchor, Cancellation)).AlreadyApplied);
            Assert.True((await scope.ServiceProvider.GetRequiredService<OperationsAlertSeeder>()
                .SeedAsync(Seed, Cancellation)).AlreadyApplied);
        }

        Assert.Equal(before, await DatabaseFingerprint.TakeAsync(Fixture.ConnectionString, Cancellation));
    }

    [Fact]
    public void RegisteredTelemetryAndOperationsRoutes_HaveNoDelete_RN23()
    {
        var routes = Fixture.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => (
                Pattern: endpoint.RoutePattern.RawText!.TrimStart('/'),
                Methods: endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? (IReadOnlyList<string>)[]))
            .Where(route => route.Pattern.StartsWith("api/telemetry", StringComparison.OrdinalIgnoreCase)
                || route.Pattern.StartsWith("api/operations", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Prova de que a varredura enxerga as 7 rotas novas: sem isso, uma lista vazia passaria no teste.
        Assert.Equal(
            [
                "GET api/operations/agents/{id:guid}",
                "GET api/operations/equipment/{id:guid}",
                "GET api/operations/snapshot",
                "GET api/telemetry/resources",
                "POST api/operations/alerts/{id:guid}/acknowledgement",
                "POST api/operations/alerts/{id:guid}/resolution",
                "POST api/telemetry/batches",
            ],
            routes.SelectMany(route => route.Methods.Select(method => $"{method} {route.Pattern}")).Order(StringComparer.Ordinal));
        Assert.Equal(2, routes.Count(route => route.Pattern.StartsWith("api/telemetry", StringComparison.Ordinal)));
        Assert.Equal(5, routes.Count(route => route.Pattern.StartsWith("api/operations", StringComparison.Ordinal)));

        Assert.DoesNotContain(routes, route => route.Methods.Contains("DELETE"));
    }

    private static string AlertPath(Guid id, string action) => $"api/operations/alerts/{id}/{action}";

    private static async Task<OperationsSnapshot> SnapshotAsync(HttpClient client)
    {
        var response = await client.GetAsync(OperationsEndpointsTests.Snapshot, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<OperationsSnapshot>(Cancellation);
    }

    /// <summary>Escala com dois agentes da carga e um que não existe, que é pulado e contado.</summary>
    private static DutyRosterSeed Roster() => new(
    [
        new DutyShiftSeed(InventorySeedData.Organization, "AT-0001", DutyShift.Morning),
        new DutyShiftSeed(InventorySeedData.Organization, "AT-0002", DutyShift.Night),
        new DutyShiftSeed(InventorySeedData.Organization, "AT-9999", DutyShift.Afternoon),
    ]);

    /// <summary>
    /// Banco como o da Fase 3, pelas cargas do Inventário: uma câmera em Mau funcionamento, uma Furtada e uma
    /// Operacional, dois agentes e as inativações (vazias) registradas.
    /// </summary>
    private async Task SeedInventoryAsync()
    {
        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<InventoryReferenceSeeder>().SeedAsync(
                InventorySeedData.Reference(), Seed, InventorySeedData.Anchor.AddDays(-200), Cancellation);
        }

        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<EquipmentCatalogSeeder>().SeedAsync(
                new EquipmentCatalogSeed(
                [
                    Camera("CAM-001", new StatusChangeSeed(DomainEquipmentStatus.Malfunction, TimeSpan.FromDays(2), null)),
                    Camera("CAM-002", new StatusChangeSeed(DomainEquipmentStatus.Stolen, TimeSpan.FromDays(3), "Furtada.")),
                    Camera("CAM-003", null),
                ]),
                Seed,
                InventorySeedData.Anchor,
                Cancellation);
        }

        static AgentSeed Agent(string registration, string displayName) => new(
            $"Agente Fictício {displayName}", displayName, registration, DomainAgentRole.TrafficAgent,
            InventorySeedData.Organization, null, InventorySeedData.Base, TimeSpan.FromDays(170));

        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AgentCatalogSeeder>().SeedAsync(
                new AgentCatalogSeed([Agent("AT-0001", "Falcão"), Agent("AT-0002", "Condor")], []),
                Seed,
                InventorySeedData.Anchor,
                Cancellation);
        }

        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<InactivationSeeder>().SeedAsync(
                new InactivationSeed([], []), Seed, InventorySeedData.Anchor, Cancellation);
        }
    }

    private static EquipmentSeed Camera(string serialNumber, StatusChangeSeed? change) =>
        new(
            EquipmentTypeCatalog.SurveillanceCamera,
            serialNumber,
            "Ômega Vision",
            "OV-200",
            InventorySeedData.Organization,
            new LocationSeed("16985001", "Rua das Flores", InventorySeedData.Hesperia, "número 181", -22.241, -49.641),
            null,
            new Dictionary<string, object?>
            {
                ["resolution"] = "full-hd-1080p", ["colorMode"] = "color", ["nightVision"] = true, ["panTiltZoom"] = false,
            },
            TimeSpan.FromDays(180),
            DomainEquipmentStatus.Operational,
            null,
            change);
}
