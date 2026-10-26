using System.Net;
using Argos.Contracts.Access;
using Argos.Contracts.Common;
using Argos.Contracts.Inventory;
using Argos.Contracts.Operations;
using Argos.Contracts.Telemetry;
using Argos.IntegrationTests.Infrastructure;
using static Argos.IntegrationTests.Infrastructure.InventoryHttp;

namespace Argos.IntegrationTests.Operations;

/// <summary>
/// Retrato e detalhes da Central pela API (T120, reduzido ao essencial; US-12 a US-14, RN-16, RN-21, RN-27, RN-36;
/// CS-05; plan 16.7, 16.13, 18.4): a equivalência com a pesquisa do Inventário, o Gestor sem posição de agentes (A-38)
/// e o caminho feliz dos detalhes. Os demais casos ficam nos unitários do T112 e na verificação do T128.
/// </summary>
public sealed class OperationsEndpointsTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    public const string Snapshot = "api/operations/snapshot";

    public const string OperationsEquipment = "api/operations/equipment";

    public const string OperationsAgents = "api/operations/agents";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Snapshot_WithTypeStatusAndRegion_CountsLikeTheInventorySearchAndHasNoAgents_RN16_RN36()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var admin = Fixture.CreateUserClient(Fixture.Tokens.For(UserRoles.Administrator));
        foreach (var serial in new[] { "CAM-001", "CAM-002" })
        {
            var camera = await admin.CreateEquipmentAsync(scenario.Camera(serial), Cancellation);
            await admin.ChangeStatusAsync(camera.Id, EquipmentStatus.Malfunction, null, Cancellation);
        }

        await admin.CreateEquipmentAsync(scenario.Camera("CAM-003"), Cancellation);
        var radar = await admin.CreateEquipmentAsync(scenario.Radar("RAD-001", "speed", "60"), Cancellation);
        await admin.ChangeStatusAsync(radar.Id, EquipmentStatus.Malfunction, null, Cancellation);
        await PutAgentOnDutyAsync(admin, scenario, "AT-0001", vehicleId: null);

        var filters = $"typeId={scenario.CameraTypeId}&status=Malfunction&regionId={scenario.RegionId}";
        var snapshot = await (await admin.GetAsync($"{Snapshot}?{filters}", Cancellation)).ReadAsync<OperationsSnapshot>(Cancellation);
        var inventory = await (await admin.GetAsync($"{Equipment}?{filters}&activity=Active", Cancellation))
            .ReadAsync<PagedResult<EquipmentSummary>>(Cancellation);

        Assert.Equal(2, inventory.TotalCount);
        Assert.Equal(inventory.TotalCount, snapshot.Indicators.TotalEquipment);
        Assert.Equal(inventory.TotalCount, snapshot.Indicators.CountsByStatus.Single(count => count.Status == EquipmentStatus.Malfunction).Count);
        Assert.All(
            snapshot.Indicators.CountsByStatus.Where(count => count.Status != EquipmentStatus.Malfunction),
            count => Assert.Equal(0, count.Count));
        Assert.Equal(inventory.Items.Select(item => item.Id).Order(), snapshot.Equipment.Select(marker => marker.Id).Order());
        Assert.True(snapshot.AgentPositionsVisible);
        Assert.Empty(snapshot.Agents);
    }

    /// <summary>
    /// RN-21 com tipo + função (plan 16.7): "atende ao filtro" usa só os filtros de equipamento, então o alerta da câmera
    /// do tipo filtrado sai sem "Fora do filtro", e o crítico do radar, fora do tipo, sai marcado.
    /// </summary>
    [Fact]
    public async Task Snapshot_WithTypeAndRole_AlertOfTheFilteredTypeMatchesTheFilter_RN21()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var admin = Fixture.CreateUserClient(Fixture.Tokens.For(UserRoles.Administrator));
        var camera = await admin.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);
        await admin.ChangeStatusAsync(camera.Id, EquipmentStatus.Malfunction, null, Cancellation);
        var radar = await admin.CreateEquipmentAsync(scenario.Radar("RAD-001", "speed", "60"), Cancellation);
        await admin.ChangeStatusAsync(radar.Id, EquipmentStatus.Stolen, "Furtado na madrugada.", Cancellation);

        var snapshot = await (await admin.GetAsync(
                $"{Snapshot}?typeId={scenario.CameraTypeId}&agentRole=TrafficAgent", Cancellation))
            .ReadAsync<OperationsSnapshot>(Cancellation);

        var cameraAlert = Assert.Single(snapshot.Alerts, alert => alert.ResourceId == camera.Id);
        Assert.Equal((AlertSeverity.High, false), (cameraAlert.Severity, cameraAlert.OutsideFilter));
        var radarAlert = Assert.Single(snapshot.Alerts, alert => alert.ResourceId == radar.Id);
        Assert.Equal((AlertSeverity.Critical, true), (radarAlert.Severity, radarAlert.OutsideFilter));
    }

    [Fact]
    public async Task Manager_SeesNoAgentPositionNorTheCrewVehicleLivePosition_RN27_A38()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var admin = Fixture.CreateUserClient(Fixture.Tokens.For(UserRoles.Administrator));
        var patrol = await admin.CreateEquipmentAsync(scenario.Patrol("VTR-001"), Cancellation);
        var agent = await PutAgentOnDutyAsync(admin, scenario, "AT-0001", patrol.Id);

        // O Administrador vê a viatura na rua e o agente: o cenário tem posição ao vivo.
        var adminSnapshot = await (await admin.GetAsync(Snapshot, Cancellation)).ReadAsync<OperationsSnapshot>(Cancellation);
        Assert.Equal(PositionSource.Live, adminSnapshot.Equipment.Single(marker => marker.Id == patrol.Id).PositionSource);
        Assert.Equal(agent.Id, Assert.Single(adminSnapshot.Agents).Id);

        using var manager = Fixture.CreateUserClient(Fixture.Tokens.For(UserRoles.Manager));
        var snapshot = await (await manager.GetAsync(Snapshot, Cancellation)).ReadAsync<OperationsSnapshot>(Cancellation);
        Assert.False(snapshot.AgentPositionsVisible);
        Assert.Empty(snapshot.Agents);
        Assert.Equal(PositionSource.Base, snapshot.Equipment.Single(marker => marker.Id == patrol.Id).PositionSource);
        Assert.Equal(1, snapshot.Indicators.AgentsInActivity);

        var details = await (await manager.GetAsync($"{OperationsEquipment}/{patrol.Id}", Cancellation))
            .ReadAsync<OperationsEquipmentDetails>(Cancellation);
        Assert.Null(details.CurrentPosition);
        Assert.NotNull(details.LastCommunicationAt);

        await ProblemAssert.IsForbiddenAsync(await manager.GetAsync($"{OperationsAgents}/{agent.Id}", Cancellation), Cancellation);
    }

    [Fact]
    public async Task Details_OfEquipmentAndAgent_AreOkForTheAdministrator_US14()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var admin = Fixture.CreateUserClient(Fixture.Tokens.For(UserRoles.Administrator));
        var patrol = await admin.CreateEquipmentAsync(scenario.Patrol("VTR-001"), Cancellation);
        var agent = await PutAgentOnDutyAsync(admin, scenario, "AT-0001", patrol.Id);

        var equipmentResponse = await admin.GetAsync($"{OperationsEquipment}/{patrol.Id}", Cancellation);
        var agentResponse = await admin.GetAsync($"{OperationsAgents}/{agent.Id}", Cancellation);

        Assert.Equal(HttpStatusCode.OK, equipmentResponse.StatusCode);
        var equipment = await equipmentResponse.ReadAsync<OperationsEquipmentDetails>(Cancellation);
        Assert.Equal((patrol.Id, -22.25), (equipment.Equipment.Id, equipment.CurrentPosition!.Latitude));

        Assert.Equal(HttpStatusCode.OK, agentResponse.StatusCode);
        var details = await agentResponse.ReadAsync<OperationsAgentDetails>(Cancellation);
        Assert.Equal((agent.Id, "Falcão", AgentStatus.OnDuty), (details.Id, details.DisplayName, details.Status));
        Assert.Equal(patrol.Id, details.AssignedEquipment!.EquipmentId);
        Assert.NotNull(details.Position);
    }

    /// <summary>
    /// Cadastra o agente, vincula-o à viatura (se houver) e o põe em serviço pela telemetria, com a posição dele e a da
    /// viatura no mesmo lote (plan 16.4).
    /// </summary>
    private async Task<AgentDetails> PutAgentOnDutyAsync(
        HttpClient admin, InventoryScenario scenario, string registration, Guid? vehicleId)
    {
        var agent = await admin.CreateAgentAsync(scenario.Agent(registration), Cancellation);
        if (vehicleId is { } vehicle)
        {
            Assert.Equal(HttpStatusCode.Created, (await admin.AssignAsync(agent.Id, vehicle, Cancellation)).StatusCode);
        }

        // Depois do cadastro: a situação reportada é a mudança mais recente do agente (plan 16.4).
        var now = DateTimeOffset.UtcNow;
        List<TelemetryPositionItem> positions =
        [
            new() { ResourceKind = ResourceKind.Agent, ResourceId = agent.Id, Latitude = -22.25, Longitude = -49.65, RecordedAt = now },
        ];
        if (vehicleId is { } patrol)
        {
            positions.Add(new() { ResourceKind = ResourceKind.Equipment, ResourceId = patrol, Latitude = -22.25, Longitude = -49.65, RecordedAt = now });
        }

        using var system = Fixture.CreateSystemClient();
        var response = await system.PostJsonAsync(TelemetryHttp.BatchesPath, new TelemetryBatchRequest
        {
            SentAt = now,
            IsSimulated = true,
            AgentStatusReports = [new AgentStatusReportItem { AgentId = agent.Id, Status = AgentStatus.OnDuty, ReportedAt = now }],
            Positions = positions,
        }, Cancellation);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return agent;
    }
}
