using System.Net;
using Argos.Contracts.Access;
using Argos.Contracts.Common;
using Argos.Contracts.Inventory;
using Argos.IntegrationTests.Infrastructure;
using static Argos.IntegrationTests.Infrastructure.InventoryHttp;

namespace Argos.IntegrationTests.Inventory;

/// <summary>
/// Inativação de equipamentos pela API (T096; US-09, RF-001, RF-010, RN-01, RN-23, RN-35; Art. XII.3, XII.4; plan
/// 15.12, 15.16): as situações que a RN-35 recusa e a dupla inativação (409 com o motivo), a manutenção aberta
/// cancelada na inativação, o cadastro inativo recusando edição, situação e manutenção, a RN-01 contra o inativo, o
/// filtro <c>activity</c> da pesquisa com os erros por parâmetro, o 403 do Gestor e o 404. O caminho feliz sem corpo
/// está em <see cref="PhaseThreeEndpointsTests"/>.
/// </summary>
public sealed class EquipmentInactivationEndpointsTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Inactivate_DeactivatedWithReason_Returns204_AndRecordsTheReasonInTheTimeline_US09()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var camera = await client.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);
        await client.ChangeStatusAsync(camera.Id, EquipmentStatus.Deactivated, "Retirada do poste.", Cancellation);

        var response = await InactivateAsync(client, camera.Id, "Baixa patrimonial.");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var details = await client.GetEquipmentAsync(camera.Id, Cancellation);
        Assert.Equal((false, EquipmentStatus.Deactivated), (details.IsActive, details.Status));
        var entry = (await TimelineAsync(client, camera.Id)).Items[0];
        Assert.Equal((TimelineEntryKind.Inactivated, "Cadastro inativado", "admin"), (entry.Kind, entry.Title, entry.Author));
        Assert.Equal([new TimelineDetailData("Motivo", "Baixa patrimonial.")], entry.Details);
    }

    [Theory]
    [InlineData(EquipmentStatus.Operational, "Operacional")]
    [InlineData(EquipmentStatus.Malfunction, "Mau funcionamento")]
    [InlineData(EquipmentStatus.UnderMaintenance, "Em manutenção")]
    public async Task Inactivate_InAStatusOutsideRn35_IsBusinessRuleNamingTheStatus_AndNothingChanges_RN35(
        EquipmentStatus status, string label)
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var camera = await client.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);
        if (status != EquipmentStatus.Operational)
        {
            await client.ChangeStatusAsync(camera.Id, status, justification: null, Cancellation);
        }

        var response = await InactivateAsync(client, camera.Id, reason: null);

        await ProblemAssert.IsBusinessRuleAsync(
            response,
            $"Equipamento na situação {label} não pode ser inativado. A inativação só é possível nas situações Ainda não "
            + "instalado, Desativado ou Furtado.",
            Cancellation);
        Assert.True((await client.GetEquipmentAsync(camera.Id, Cancellation)).IsActive);
    }

    [Fact]
    public async Task Inactivate_Twice_IsBusinessRule_RN35()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var camera = await client.CreateEquipmentAsync(scenario.Camera("CAM-001", EquipmentStatus.NotInstalled), Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, (await InactivateAsync(client, camera.Id, reason: null)).StatusCode);

        var response = await InactivateAsync(client, camera.Id, reason: null);

        await ProblemAssert.IsBusinessRuleAsync(response, "O equipamento já está inativo.", Cancellation);
    }

    [Fact]
    public async Task Inactivate_NotInstalledWithAnOpenScheduledMaintenance_CancelsIt_RN35()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var today = await Fixture.TodayAsync(Cancellation);
        var camera = await client.CreateEquipmentAsync(
            scenario.Camera("CAM-001", EquipmentStatus.NotInstalled, InventoryScenario.Scheduled(today.AddDays(10))), Cancellation);
        Assert.Single(camera.OpenMaintenances);

        Assert.Equal(HttpStatusCode.NoContent, (await InactivateAsync(client, camera.Id, reason: null)).StatusCode);

        Assert.Empty((await client.GetEquipmentAsync(camera.Id, Cancellation)).OpenMaintenances);
        var cancelled = Assert.Single(
            (await TimelineAsync(client, camera.Id)).Items, entry => entry.Kind == TimelineEntryKind.MaintenanceCancelled);
        Assert.Equal(
            ("Cancelada automaticamente: equipamento inativado.", "admin"), (cancelled.Description, cancelled.Author));
    }

    [Fact]
    public async Task InactiveEquipment_RefusesEditStatusChangeAndMaintenance_RN35()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var created = await client.CreateEquipmentAsync(scenario.Camera("CAM-001", EquipmentStatus.NotInstalled), Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, (await InactivateAsync(client, created.Id, reason: null)).StatusCode);
        var current = await client.GetEquipmentAsync(created.Id, Cancellation);
        var today = await Fixture.TodayAsync(Cancellation);

        var edit = await client.PutJsonAsync(
            $"{Equipment}/{created.Id}",
            new UpdateEquipmentRequest
            {
                SerialNumber = current.SerialNumber,
                Brand = current.Brand,
                Model = "OV-300",
                OrganizationId = scenario.OrganizationId,
                Location = scenario.HesperiaLocation(),
                Attributes = InventoryScenario.CameraAttributes("full-hd-1080p"),
                Version = current.Version,
            },
            Cancellation);
        var status = await client.PostJsonAsync(
            $"{Equipment}/{created.Id}/status-changes",
            new ChangeEquipmentStatusRequest { Status = EquipmentStatus.Operational },
            Cancellation);
        var maintenance = await client.PostJsonAsync(
            $"{Equipment}/{created.Id}/maintenances", InventoryScenario.Scheduled(today.AddDays(5)), Cancellation);

        await ProblemAssert.IsBusinessRuleAsync(edit, "Equipamento inativo não pode ser editado.", Cancellation);
        await ProblemAssert.IsBusinessRuleAsync(status, "Equipamento inativo não pode mudar de situação.", Cancellation);
        await ProblemAssert.IsBusinessRuleAsync(maintenance, "Equipamento inativo não recebe manutenção.", Cancellation);
        var after = await client.GetEquipmentAsync(created.Id, Cancellation);
        Assert.Equal((current.Model, current.Status, current.Version), (after.Model, after.Status, after.Version));
    }

    [Fact]
    public async Task Create_SameBrandAndSerialAsAnInactive_IsConflictPointingToTheInactive_RN01()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var inactive = await client.CreateEquipmentAsync(scenario.Camera("ABC-123", EquipmentStatus.NotInstalled), Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, (await InactivateAsync(client, inactive.Id, reason: null)).StatusCode);

        var response = await client.PostJsonAsync(Equipment, scenario.Camera("abc-123", downtown: true), Cancellation);

        var problem = await ProblemAssert.IsConflictAsync(
            response, "Já existe um equipamento com essa marca e esse número de série.", Cancellation);
        Assert.Equal(inactive.Id, problem.GetProperty("existingEquipmentId").GetGuid());
    }

    [Fact]
    public async Task Search_ActivityFilter_DefaultsToActive_AndSeparatesInactiveAndAll_US09()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Manager);
        Guid active;
        Guid inactive;
        using (var admin = Client(UserRoles.Administrator))
        {
            active = (await admin.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation)).Id;
            inactive = (await admin.CreateEquipmentAsync(scenario.Camera("CAM-002", EquipmentStatus.NotInstalled), Cancellation)).Id;
            Assert.Equal(HttpStatusCode.NoContent, (await InactivateAsync(admin, inactive, reason: null)).StatusCode);
        }

        Assert.Equal([active], await SearchIdsAsync(client, string.Empty));
        Assert.Equal([active], await SearchIdsAsync(client, "?activity=active"));
        Assert.Equal([inactive], await SearchIdsAsync(client, "?activity=inactive"));
        Assert.Equal([active, inactive], await SearchIdsAsync(client, "?activity=all&sort=serialNumber"));
        var summary = Assert.Single(
            (await (await client.GetAsync($"{Equipment}?activity=inactive", Cancellation))
                .ReadAsync<PagedResult<EquipmentSummary>>(Cancellation)).Items);
        Assert.False(summary.IsActive);
    }

    /// <summary>
    /// Chaves e mensagens exatas (plan 15.12, Pesquisas): o número fora do enum é recusado pelo binder, com a mensagem
    /// de <c>PortugueseModelBindingMessages</c>; a página zero, pelo <c>[Range]</c> de <c>PageQuery</c>.
    /// </summary>
    [Theory]
    [InlineData("?activity=99", "activity", "O valor '99' é inválido.")]
    [InlineData("?page=0", "page", "A página deve ser 1 ou maior.")]
    public async Task Search_InvalidActivityOrPage_IsBadRequestOnThatParameterWithTheExactMessage(
        string query, string field, string message)
    {
        using var client = Client(UserRoles.Administrator);

        var response = await client.GetAsync($"{Equipment}{query}", Cancellation);

        var errors = await ProblemAssert.IsValidationAsync(response, Cancellation);
        Assert.Equal([field], errors.Keys);
        Assert.Equal([message], errors[field]);
    }

    [Fact]
    public async Task Inactivate_ByManager_IsForbidden_AndUnknownIsNotFound_D58()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        Guid id;
        using (var admin = Client(UserRoles.Administrator))
        {
            id = (await admin.CreateEquipmentAsync(scenario.Camera("CAM-001", EquipmentStatus.NotInstalled), Cancellation)).Id;
            await ProblemAssert.IsNotFoundAsync(
                await InactivateAsync(admin, Guid.NewGuid(), reason: null), "Equipamento não encontrado.", Cancellation);
        }

        using var manager = Client(UserRoles.Manager);
        using var operatorClient = Client(UserRoles.Operator);

        await ProblemAssert.IsForbiddenAsync(await InactivateAsync(manager, id, reason: null), Cancellation);
        await ProblemAssert.IsForbiddenAsync(await InactivateAsync(operatorClient, id, reason: null), Cancellation);
        Assert.True((await manager.GetEquipmentAsync(id, Cancellation)).IsActive);
    }

    [Fact]
    public async Task Inactivate_ReasonTooLong_IsBadRequestOnReason()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var camera = await client.CreateEquipmentAsync(scenario.Camera("CAM-001", EquipmentStatus.NotInstalled), Cancellation);

        var response = await InactivateAsync(client, camera.Id, new string('x', 501));

        Assert.Equal(["reason"], (await ProblemAssert.IsValidationAsync(response, Cancellation)).Keys);
        Assert.True((await client.GetEquipmentAsync(camera.Id, Cancellation)).IsActive);
    }

    private static Task<HttpResponseMessage> InactivateAsync(HttpClient client, Guid id, string? reason) =>
        client.PostJsonAsync($"{Equipment}/{id}/inactivation", new InactivationRequest { Reason = reason }, Cancellation);

    private static async Task<PagedResult<TimelineEntry>> TimelineAsync(HttpClient client, Guid id)
    {
        var response = await client.GetAsync($"{Equipment}/{id}/timeline", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<PagedResult<TimelineEntry>>(Cancellation);
    }

    private static async Task<Guid[]> SearchIdsAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"{Equipment}{query}", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return [.. (await response.ReadAsync<PagedResult<EquipmentSummary>>(Cancellation)).Items.Select(item => item.Id)];
    }

    private HttpClient Client(string role) => Fixture.CreateUserClient(Fixture.Tokens.For(role));
}
