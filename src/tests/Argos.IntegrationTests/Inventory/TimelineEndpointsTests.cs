using System.Net;
using Argos.Contracts.Access;
using Argos.Contracts.Common;
using Argos.Contracts.Inventory;
using Argos.IntegrationTests.Infrastructure;
using static Argos.IntegrationTests.Infrastructure.InventoryHttp;

namespace Argos.IntegrationTests.Inventory;

/// <summary>
/// Linha do tempo pela API (T098; US-08, RF-011, RF-018; Art. V.1; plan 15.6, 15.16): a ordem do mais recente para o
/// mais antigo, com os fatos de uma mesma operação na ordem inversa à que aconteceram; a presença de cadastro,
/// situação, manutenção aberta, concluída e cancelada, alteração cadastral, inativação e vínculos, com autor e data;
/// a paginação e o 404. A resposta aos três perfis está em <see cref="PhaseThreeEndpointsTests"/>.
/// </summary>
public sealed class TimelineEndpointsTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EquipmentTimeline_ListsEveryFactNewestFirst_WithAuthorAndDate_US08()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        var before = DateTimeOffset.UtcNow;
        Guid id;
        using (var admin = Client(UserRoles.Administrator))
        {
            var created = await admin.CreateEquipmentAsync(scenario.Patrol("VTR-001"), Cancellation);
            id = created.Id;
            var agent = await admin.CreateAgentAsync(scenario.Agent("AT-0001"), Cancellation);
            Assert.Equal(HttpStatusCode.Created, (await admin.AssignAsync(agent.Id, id, Cancellation)).StatusCode);
            var patrol = scenario.Patrol("VTR-001");
            var updated = await admin.PutJsonAsync(
                $"{Equipment}/{id}",
                new UpdateEquipmentRequest
                {
                    SerialNumber = patrol.SerialNumber, Brand = patrol.Brand, Model = "Patrulha 3.0",
                    OrganizationId = patrol.OrganizationId, BaseId = patrol.BaseId, Attributes = patrol.Attributes,
                    Version = created.Version,
                },
                Cancellation);
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            var malfunction = await admin.ChangeStatusAsync(id, EquipmentStatus.Malfunction, justification: null, Cancellation);
            var completed = await admin.PostJsonAsync(
                $"{Equipment}/{id}/maintenances/{malfunction.OpenMaintenances.Single().Id}/completion",
                new CompleteMaintenanceRequest
                {
                    Result = MaintenanceResult.Success, CompletedAt = DateTimeOffset.UtcNow, Notes = "Bateria trocada.",
                },
                Cancellation);
            Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
            var today = await Fixture.TodayAsync(Cancellation);
            var scheduled = await admin.PostJsonAsync(
                $"{Equipment}/{id}/maintenances", InventoryScenario.Scheduled(today.AddDays(10)), Cancellation);
            Assert.Equal(HttpStatusCode.Created, scheduled.StatusCode);
            await admin.ChangeStatusAsync(id, EquipmentStatus.Deactivated, "Baixa da frota.", Cancellation);
            var inactivated = await admin.PostJsonAsync(
                $"{Equipment}/{id}/inactivation", new InactivationRequest { Reason = "Leilão." }, Cancellation);
            Assert.Equal(HttpStatusCode.NoContent, inactivated.StatusCode);
        }

        using var client = Client(UserRoles.Operator);
        var timeline = await TimelineAsync(client, $"{Equipment}/{id}/timeline?pageSize=50");

        Assert.Equal(
            [
                TimelineEntryKind.CrewUnassigned, TimelineEntryKind.Inactivated,
                TimelineEntryKind.MaintenanceCancelled, TimelineEntryKind.StatusChanged,
                TimelineEntryKind.MaintenanceOpened,
                TimelineEntryKind.StatusChanged, TimelineEntryKind.MaintenanceCompleted,
                TimelineEntryKind.MaintenanceOpened, TimelineEntryKind.StatusChanged,
                TimelineEntryKind.RegistrationUpdated,
                TimelineEntryKind.CrewAssigned,
                TimelineEntryKind.Registered,
            ],
            timeline.Items.Select(entry => entry.Kind));
        Assert.Equal(12, timeline.TotalCount);
        AssertNewestFirst(timeline.Items, before);
        Assert.All(timeline.Items, entry => Assert.Equal("admin", entry.Author));

        var items = timeline.Items;
        Assert.Equal(("Agente desvinculado", "Falcão · matrícula AT-0001"), (items[0].Title, items[0].Description));
        Assert.Equal([new TimelineDetailData("Motivo", "Equipamento inativado")], items[0].Details);
        Assert.Equal([new TimelineDetailData("Motivo", "Leilão.")], items[1].Details);
        Assert.Equal("Cancelada automaticamente: equipamento desativado.", items[2].Description);
        Assert.Equal("Operacional → Desativado", items[3].Title);
        Assert.Equal("Mau funcionamento → Operacional", items[5].Title);
        Assert.Equal("Manutenção imediata concluída com sucesso", items[6].Title);
        Assert.Equal([new TimelineDetailData("Observações", "Bateria trocada.")], items[6].Details);
        Assert.Equal("Operacional → Mau funcionamento", items[8].Title);
        Assert.Equal(new TimelineDetailData("Modelo", "Patrulha 2.0 → Patrulha 3.0"), Assert.Single(items[9].Details));
        Assert.Equal(("Agente vinculado", "Falcão · matrícula AT-0001"), (items[10].Title, items[10].Description));
        Assert.Equal(("Equipamento cadastrado", "Situação inicial: Operacional."), (items[11].Title, items[11].Description));
    }

    [Fact]
    public async Task AgentTimeline_ListsRegistrationStatusAssignmentsAndInactivationNewestFirst_US08()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        var before = DateTimeOffset.UtcNow;
        using var admin = Client(UserRoles.Administrator);
        var patrol = await admin.CreateEquipmentAsync(scenario.Patrol("VTR-001"), Cancellation);
        var motorcycle = await admin.CreateEquipmentAsync(scenario.Motorcycle("MOT-001"), Cancellation);
        var agent = await admin.CreateAgentAsync(scenario.Agent("AT-0001"), Cancellation);
        var request = scenario.Agent("AT-0001", "Condor");
        var updated = await admin.PutJsonAsync(
            $"{Agents}/{agent.Id}",
            new UpdateAgentRequest
            {
                FullName = request.FullName, DisplayName = request.DisplayName, RegistrationNumber = request.RegistrationNumber,
                Role = request.Role, OrganizationId = request.OrganizationId, BaseId = request.BaseId, Version = agent.Version,
            },
            Cancellation);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        await admin.ChangeAgentStatusAsync(agent.Id, AgentStatus.OnDuty, Cancellation);
        Assert.Equal(HttpStatusCode.Created, (await admin.AssignAsync(agent.Id, patrol.Id, Cancellation)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await admin.AssignAsync(agent.Id, motorcycle.Id, Cancellation)).StatusCode);
        await admin.ChangeAgentStatusAsync(agent.Id, AgentStatus.OnLeave, Cancellation);
        var inactivated = await admin.PostJsonAsync(
            $"{Agents}/{agent.Id}/inactivation", new InactivationRequest { Reason = "Desligamento." }, Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, inactivated.StatusCode);

        using var manager = Client(UserRoles.Manager);
        var timeline = await TimelineAsync(manager, $"{Agents}/{agent.Id}/timeline");

        Assert.Equal(
            [
                TimelineEntryKind.CrewUnassigned, TimelineEntryKind.Inactivated,
                TimelineEntryKind.StatusChanged,
                TimelineEntryKind.CrewAssigned, TimelineEntryKind.CrewUnassigned,
                TimelineEntryKind.CrewAssigned,
                TimelineEntryKind.StatusChanged,
                TimelineEntryKind.RegistrationUpdated,
                TimelineEntryKind.Registered,
            ],
            timeline.Items.Select(entry => entry.Kind));
        AssertNewestFirst(timeline.Items, before);
        Assert.All(timeline.Items, entry => Assert.Equal("admin", entry.Author));

        var items = timeline.Items;
        Assert.Equal([new TimelineDetailData("Motivo", "Agente inativado")], items[0].Details);
        Assert.Equal(("Vínculo encerrado", "Moto de agente de trânsito · MOT-001"), (items[0].Title, items[0].Description));
        Assert.Equal([new TimelineDetailData("Motivo", "Desligamento.")], items[1].Details);
        Assert.Equal("Em serviço → Afastado", items[2].Title);
        Assert.Equal(("Vinculado a equipamento", "Moto de agente de trânsito · MOT-001"), (items[3].Title, items[3].Description));
        Assert.Equal(items[3].OccurredAt, items[4].OccurredAt);
        Assert.Equal(("Vínculo encerrado", "Viatura · VTR-001"), (items[4].Title, items[4].Description));
        Assert.Equal([new TimelineDetailData("Motivo", "Agente vinculado a outro equipamento")], items[4].Details);
        Assert.Equal("Fora de serviço → Em serviço", items[6].Title);
        Assert.Equal(new TimelineDetailData("Nome de exibição", "Falcão → Condor"), Assert.Single(items[7].Details));
        Assert.Equal(("Agente cadastrado", "Situação inicial: Fora de serviço."), (items[8].Title, items[8].Description));
    }

    [Fact]
    public async Task Timelines_PageInTheSameOrder_AndRefuseAnInvalidPage()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var camera = await client.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);
        foreach (var status in new[] { EquipmentStatus.UnderMaintenance, EquipmentStatus.Operational, EquipmentStatus.Malfunction })
        {
            await client.ChangeStatusAsync(camera.Id, status, justification: null, Cancellation);
        }

        var all = await TimelineAsync(client, $"{Equipment}/{camera.Id}/timeline");
        var second = await TimelineAsync(client, $"{Equipment}/{camera.Id}/timeline?page=2&pageSize=2");
        var invalid = await client.GetAsync($"{Equipment}/{camera.Id}/timeline?page=0&pageSize=101", Cancellation);

        Assert.Equal(all.TotalCount, second.TotalCount);
        Assert.Equal((2, 2), (second.Page, second.PageSize));
        Assert.Equal(all.Items.Skip(2).Take(2).Select(entry => (entry.Kind, entry.OccurredAt)), second.Items.Select(entry => (entry.Kind, entry.OccurredAt)));
        var errors = await ProblemAssert.IsValidationAsync(invalid, Cancellation);
        Assert.Equal(["page", "pageSize"], errors.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Timelines_OfUnknownEquipmentOrAgent_AreNotFound()
    {
        using var client = Client(UserRoles.Operator);

        await ProblemAssert.IsNotFoundAsync(
            await client.GetAsync($"{Equipment}/{Guid.NewGuid()}/timeline", Cancellation), "Equipamento não encontrado.", Cancellation);
        await ProblemAssert.IsNotFoundAsync(
            await client.GetAsync($"{Agents}/{Guid.NewGuid()}/timeline", Cancellation), "Agente não encontrado.", Cancellation);
    }

    /// <summary>Datas em UTC, entre o começo do teste e agora, sem nenhuma entrada mais recente que a anterior.</summary>
    private static void AssertNewestFirst(IReadOnlyList<TimelineEntry> items, DateTimeOffset before)
    {
        var now = DateTimeOffset.UtcNow;
        Assert.All(items, entry => Assert.InRange(entry.OccurredAt, before.AddSeconds(-1), now));
        Assert.All(items.Zip(items.Skip(1)), pair => Assert.True(pair.First.OccurredAt >= pair.Second.OccurredAt));
    }

    private static async Task<PagedResult<TimelineEntry>> TimelineAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<PagedResult<TimelineEntry>>(Cancellation);
    }

    private HttpClient Client(string role) => Fixture.CreateUserClient(Fixture.Tokens.For(role));
}
