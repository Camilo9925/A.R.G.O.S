using System.Net;
using Argos.Application.Seeding;
using Argos.Contracts.Access;
using Argos.Contracts.Common;
using Argos.Contracts.Inventory;
using Argos.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using static Argos.IntegrationTests.Infrastructure.InventoryHttp;

namespace Argos.IntegrationTests.Inventory;

/// <summary>
/// Pesquisa de equipamentos (T067; US-03, RF-010; plan 15.8, 15.16): busca sem acento e sem diferença de maiúsculas,
/// filtros "ou" dentro e "e" entre, paginação, ordenação e a equivalência entre a necessidade de manutenção traduzida na
/// consulta e a calculada pelo Domínio (Art. V.3; RN-10; D-54), nas fronteiras das janelas.
/// </summary>
public sealed class EquipmentSearchEndpointsTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    private const string WindowKey = "maintenance.upcoming-window-days";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // Busca

    [Theory]
    [InlineData("hesperia")]
    [InlineData("HESPÉRIA")]
    [InlineData("Vila Hespéria")]
    public async Task Search_ByNeighborhood_IgnoresAccentsAndCase_US03(string term)
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var inHesperia = await client.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);
        await client.CreateEquipmentAsync(scenario.Camera("CAM-002", downtown: true), Cancellation);
        await client.CreateEquipmentAsync(scenario.Motorcycle("MOTO-001"), Cancellation);

        var result = await SearchAsync(client, $"?search={Uri.EscapeDataString(term)}");

        Assert.Equal(1, result.TotalCount);
        Assert.Equal([inHesperia.Id], result.Items.Select(item => item.Id));
        Assert.Equal(InventorySeedData.Hesperia, result.Items[0].NeighborhoodName);
    }

    [Fact]
    public async Task Search_BySerialStreetBrandOrModel_AndMobileByTheBaseNeighborhood_US03()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var camera = await client.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);
        var downtown = await client.CreateEquipmentAsync(scenario.Camera("CAM-002", downtown: true), Cancellation);
        var motorcycle = await client.CreateEquipmentAsync(scenario.Motorcycle("MOTO-001"), Cancellation);

        Assert.Equal([camera.Id], (await SearchAsync(client, "?search=cam-001")).Items.Select(item => item.Id));
        Assert.Equal([camera.Id], (await SearchAsync(client, "?search=FLORES")).Items.Select(item => item.Id));
        Assert.Equal([motorcycle.Id], (await SearchAsync(client, "?search=moto%20sul")).Items.Select(item => item.Id));
        Assert.Equal(new[] { downtown.Id, motorcycle.Id }.Order(),
            (await SearchAsync(client, "?search=centro")).Items.Select(item => item.Id).Order());
        Assert.Equal(2, (await SearchAsync(client, "?search=ov-200")).TotalCount);
    }

    [Fact]
    public async Task Search_WithoutMatch_ReturnsEmptyItems_US03()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Manager);
        using var admin = Client(UserRoles.Administrator);
        await admin.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);

        var result = await SearchAsync(client, "?search=inexistente");

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    // Filtros

    [Fact]
    public async Task Filters_CombineByAndAcrossAndByOrWithin_US03()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var north = await CreateNorthNeighborhoodAsync(client);
        var target = await client.CreateEquipmentAsync(scenario.Camera("CAM-001", EquipmentStatus.Malfunction), Cancellation);
        var operational = await client.CreateEquipmentAsync(scenario.Camera("CAM-002"), Cancellation);
        var otherRegionRequest = scenario.Camera("CAM-003", EquipmentStatus.Malfunction);
        otherRegionRequest.Location = InventoryScenario.Location(north.Id, "Rua das Acácias", "S/N", -22.22, -49.62);
        var otherRegion = await client.CreateEquipmentAsync(otherRegionRequest, Cancellation);
        var radarRequest = scenario.Radar("RAD-001", "red-light", null);
        radarRequest.Status = EquipmentStatus.Malfunction;
        var otherType = await client.CreateEquipmentAsync(radarRequest, Cancellation);

        var combined = await SearchAsync(
            client, $"?typeId={scenario.CameraTypeId}&status=Malfunction&regionId={scenario.RegionId}");
        Assert.Equal([target.Id], combined.Items.Select(item => item.Id));

        var eitherStatus = await SearchAsync(
            client, $"?typeId={scenario.CameraTypeId}&status=Malfunction&status=Operational&regionId={scenario.RegionId}");
        Assert.Equal(new[] { target.Id, operational.Id }.Order(), eitherStatus.Items.Select(item => item.Id).Order());

        var eitherRegion = await SearchAsync(
            client, $"?status=Malfunction&regionId={scenario.RegionId}&regionId={north.RegionId}&typeId={scenario.CameraTypeId}");
        Assert.Equal(new[] { target.Id, otherRegion.Id }.Order(), eitherRegion.Items.Select(item => item.Id).Order());

        var byNeighborhood = await SearchAsync(client, $"?neighborhoodId={north.Id}");
        Assert.Equal([otherRegion.Id], byNeighborhood.Items.Select(item => item.Id));

        var byOrganization = await SearchAsync(client, $"?organizationId={scenario.OrganizationId}&status=Malfunction");
        Assert.Equal(3, byOrganization.TotalCount);
        Assert.DoesNotContain(byOrganization.Items, item => item.Id == operational.Id);
        Assert.Contains(byOrganization.Items, item => item.Id == otherType.Id);
    }

    [Fact]
    public async Task Filters_ByCategory_SeparateFixedAndMobile_RF010()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var camera = await client.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);
        var motorcycle = await client.CreateEquipmentAsync(scenario.Motorcycle("MOTO-001"), Cancellation);

        Assert.Equal([camera.Id], (await SearchAsync(client, "?category=Fixed")).Items.Select(item => item.Id));
        Assert.Equal([motorcycle.Id], (await SearchAsync(client, "?category=Mobile")).Items.Select(item => item.Id));
        Assert.Equal(2, (await SearchAsync(client, "?category=Fixed&category=Mobile")).TotalCount);
    }

    // Paginação e ordenação

    [Fact]
    public async Task Paging_ReturnsTheTotalAndTheRequestedPage_InTheRequestedOrder_Plan15_8()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Manager);
        using var admin = Client(UserRoles.Administrator);
        foreach (var serial in new[] { "CAM-003", "CAM-001", "CAM-005", "CAM-002", "CAM-004" })
        {
            await admin.CreateEquipmentAsync(scenario.Camera(serial), Cancellation);
        }

        var second = await SearchAsync(client, "?sort=serialNumber&page=2&pageSize=2");
        var descending = await SearchAsync(client, "?sort=serialNumber&direction=desc&pageSize=2");
        var last = await SearchAsync(client, "?sort=serialNumber&page=3&pageSize=2");

        Assert.Equal((5, 2, 2), (second.TotalCount, second.Page, second.PageSize));
        Assert.Equal(["CAM-003", "CAM-004"], second.Items.Select(item => item.SerialNumber));
        Assert.Equal(["CAM-005", "CAM-004"], descending.Items.Select(item => item.SerialNumber));
        Assert.Equal(["CAM-005"], last.Items.Select(item => item.SerialNumber));
    }

    [Theory]
    [InlineData("?sort=brand", "sort")]
    [InlineData("?pageSize=101", "pageSize")]
    [InlineData("?page=0", "page")]
    [InlineData("?status=Quebrado", "status")]
    public async Task Search_InvalidParameter_IsBadRequestOnThatParameter(string query, string field)
    {
        using var client = Client(UserRoles.Administrator);

        var response = await client.GetAsync($"{Equipment}{query}", Cancellation);

        var errors = await ProblemAssert.IsValidationAsync(response, Cancellation);
        Assert.Equal([field], errors.Keys);
    }

    [Fact]
    public async Task Search_ByOperator_IsForbidden_D58()
    {
        using var client = Client(UserRoles.Operator);

        await ProblemAssert.IsForbiddenAsync(await client.GetAsync(Equipment, Cancellation), Cancellation);
    }

    // Necessidade de manutenção: consulta × Domínio (Art. V.3; RN-10; D-54)

    [Fact]
    public async Task MaintenanceNeed_QueryMatchesDomain_AtTheBoundariesOfTheDefaultWindow_RN10()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        var today = await Fixture.TodayAsync(Cancellation);
        using var client = Client(UserRoles.Administrator);

        var expected = await CreateNeedCasesAsync(client, scenario, today, window: 7);

        await AssertNeedsAsync(client, expected);
    }

    [Fact]
    public async Task MaintenanceNeed_QueryUsesTheStoredWindowParameter_LikeTheDomain_RN10_RN34()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        var today = await Fixture.TodayAsync(Cancellation);

        // O cache dos parâmetros é singleton: a janela alterada fica numa instância própria da API (plan 15.10).
        await using var isolated = Fixture.CreateIsolatedFactory();
        await using (var scope = isolated.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<BusinessParameterProvisioningService>()
                .ProvisionAsync(new Dictionary<string, int> { [WindowKey] = 3 }, Cancellation);
        }

        using var client = isolated.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Fixture.Tokens.For(UserRoles.Administrator));

        var expected = await CreateNeedCasesAsync(client, scenario, today, window: 3);

        await AssertNeedsAsync(client, expected);
    }

    /// <summary>
    /// Um equipamento por fronteira da RN-10, com a necessidade esperada: nenhuma; agendada hoje (próxima); no último
    /// dia da janela (próxima) e no dia seguinte (programada); periódica vencida ontem; imediata; e as precedências
    /// imediata sobre vencida e vencida sobre próxima.
    /// </summary>
    private static async Task<Dictionary<Guid, MaintenanceNeed>> CreateNeedCasesAsync(
        HttpClient client, InventoryScenario scenario, DateOnly today, int window)
    {
        async Task<Guid> Create(string serial, RegisterMaintenanceRequest? initial, EquipmentStatus status = EquipmentStatus.Operational) =>
            (await client.CreateEquipmentAsync(scenario.Camera(serial, status, initial), Cancellation)).Id;

        async Task Register(Guid id, RegisterMaintenanceRequest request)
        {
            var response = await client.PostJsonAsync($"{Equipment}/{id}/maintenances", request, Cancellation);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        var none = await Create("NEED-NONE", null);
        var dueToday = await Create("NEED-TODAY", InventoryScenario.Scheduled(today));
        var lastDay = await Create("NEED-LAST-DAY", InventoryScenario.Scheduled(today.AddDays(window)));
        var dayAfter = await Create("NEED-DAY-AFTER", InventoryScenario.Scheduled(today.AddDays(window + 1)));
        var overdue = await Create("NEED-OVERDUE", InventoryScenario.Periodic(today.AddDays(-1)));
        var immediate = await Create("NEED-IMMEDIATE", null, EquipmentStatus.Malfunction);
        var immediateAndOverdue = await Create("NEED-IMM-OVERDUE", InventoryScenario.Periodic(today.AddDays(-5)), EquipmentStatus.Malfunction);
        var overdueAndUpcoming = await Create("NEED-OVERDUE-UPC", InventoryScenario.Periodic(today.AddDays(-1)));
        await Register(overdueAndUpcoming, InventoryScenario.Scheduled(today.AddDays(1)));
        var mobileLastDay = (await client.CreateEquipmentAsync(
            scenario.Motorcycle("NEED-MOBILE", InventoryScenario.Periodic(today.AddDays(window))), Cancellation)).Id;

        return new Dictionary<Guid, MaintenanceNeed>
        {
            [none] = MaintenanceNeed.None,
            [dueToday] = MaintenanceNeed.Upcoming,
            [lastDay] = MaintenanceNeed.Upcoming,
            [dayAfter] = MaintenanceNeed.Scheduled,
            [overdue] = MaintenanceNeed.Overdue,
            [immediate] = MaintenanceNeed.Immediate,
            [immediateAndOverdue] = MaintenanceNeed.Immediate,
            [overdueAndUpcoming] = MaintenanceNeed.Overdue,
            [mobileLastDay] = MaintenanceNeed.Upcoming,
        };
    }

    /// <summary>
    /// A necessidade de cada equipamento é a esperada na pesquisa (consulta traduzida) e nos detalhes (Domínio), e o
    /// filtro de cada valor traz exatamente os equipamentos com ele.
    /// </summary>
    private static async Task AssertNeedsAsync(HttpClient client, Dictionary<Guid, MaintenanceNeed> expected)
    {
        var all = await SearchAsync(client, "?pageSize=100");
        Assert.Equal(expected.Count, all.TotalCount);

        foreach (var summary in all.Items)
        {
            var details = await client.GetEquipmentAsync(summary.Id, Cancellation);
            Assert.True(
                expected[summary.Id] == summary.MaintenanceNeed && summary.MaintenanceNeed == details.MaintenanceNeed,
                $"{summary.SerialNumber}: esperado {expected[summary.Id]}, pesquisa {summary.MaintenanceNeed}, "
                + $"detalhes {details.MaintenanceNeed}.");
        }

        foreach (var need in Enum.GetValues<MaintenanceNeed>())
        {
            var filtered = await SearchAsync(client, $"?maintenanceNeed={need}&pageSize=100");
            Assert.Equal(
                expected.Where(entry => entry.Value == need).Select(entry => entry.Key).Order(),
                filtered.Items.Select(item => item.Id).Order());
        }
    }

    private HttpClient Client(string role) => Fixture.CreateUserClient(Fixture.Tokens.For(role));

    private static async Task<NeighborhoodData> CreateNorthNeighborhoodAsync(HttpClient client)
    {
        var region = await (await client.PostJsonAsync(
            "api/inventory/regions", new RegionRequest { Name = "Zona Norte" }, Cancellation)).ReadAsync<RegionData>(Cancellation);
        var response = await client.PostJsonAsync(
            "api/inventory/neighborhoods",
            new NeighborhoodRequest
            {
                Name = "Jardim Órion",
                RegionId = region.Id,
                Center = new GeoPointInput { Latitude = -22.22, Longitude = -49.62 },
            },
            Cancellation);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<NeighborhoodData>(Cancellation);
    }

    private static async Task<PagedResult<EquipmentSummary>> SearchAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"{Equipment}{query}", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<PagedResult<EquipmentSummary>>(Cancellation);
    }
}
