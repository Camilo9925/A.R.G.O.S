using System.Net;
using Argos.Contracts.Access;
using Argos.Contracts.Inventory;
using Argos.IntegrationTests.Infrastructure;

namespace Argos.IntegrationTests.Inventory;

/// <summary>
/// Endpoints de referência e cadastros de apoio (T066; RF-004, RF-019, RF-020, RN-22; D-58; plan 15.14, 15.16): caminho
/// feliz de cada rota (Art. XII.3) e os erros com o campo exato ou o motivo no <c>detail</c> (Art. XII.4). Listas para
/// qualquer usuário autenticado; escrita e CEP com <c>inventory.manage</c>.
/// </summary>
public sealed class SupportRegistersEndpointsTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    private const string Inventory = "api/inventory";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // Município

    [Fact]
    public async Task Municipality_ReturnsNameStateTimeZoneBoundaryAndCenter_ToAnyAuthenticatedUser_D58()
    {
        await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Operator);

        var response = await client.GetAsync($"{Inventory}/municipality", Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var municipality = await response.ReadAsync<MunicipalityData>(Cancellation);
        Assert.Equal("Prometeia", municipality.Name);
        Assert.Equal("SP", municipality.State);
        Assert.Equal("America/Sao_Paulo", municipality.TimeZoneId);
        Assert.Equal(5, Assert.Single(municipality.Boundary).Count);
        Assert.Equal(new GeoPointData(-22.25, -49.65), municipality.Center);
    }

    [Fact]
    public async Task Municipality_BeforeTheLoad_IsNotFound()
    {
        using var client = Client(UserRoles.Administrator);

        var response = await client.GetAsync($"{Inventory}/municipality", Cancellation);

        await ProblemAssert.IsNotFoundAsync(response, "O município ainda não foi carregado.", Cancellation);
    }

    // CEP

    [Theory]
    [InlineData("16985-001")]
    [InlineData("16985001")]
    public async Task ZipCode_Known_SuggestsStreetNeighborhoodRegionCityAndState_RF004(string zipCode)
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);

        var response = await client.GetAsync($"{Inventory}/zip-codes/{zipCode}", Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var suggestion = await response.ReadAsync<ZipCodeSuggestion>(Cancellation);
        Assert.Equal("16985-001", suggestion.ZipCode);
        Assert.Equal("Rua das Flores", suggestion.Street);
        Assert.Equal(scenario.HesperiaId, suggestion.NeighborhoodId);
        Assert.Equal(InventorySeedData.Hesperia, suggestion.NeighborhoodName);
        Assert.Equal(scenario.RegionId, suggestion.RegionId);
        Assert.Equal("Zona Única", suggestion.RegionName);
        Assert.Equal(("Prometeia", "SP"), (suggestion.City, suggestion.State));
        Assert.Equal((-22.241, -49.641), (suggestion.Latitude, suggestion.Longitude));
    }

    [Fact]
    public async Task ZipCode_Unknown_IsNotFoundWithGuidance_D52()
    {
        await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);

        var response = await client.GetAsync($"{Inventory}/zip-codes/16985-999", Cancellation);

        await ProblemAssert.IsNotFoundAsync(
            response, "CEP não encontrado em Prometeia. Preencha o endereço manualmente.", Cancellation);
    }

    [Fact]
    public async Task ZipCode_InvalidFormat_IsBadRequestOnZipCode_RN02()
    {
        using var client = Client(UserRoles.Administrator);

        var response = await client.GetAsync($"{Inventory}/zip-codes/1698A001", Cancellation);

        var errors = await ProblemAssert.IsValidationAsync(response, Cancellation);
        Assert.Equal(["zipCode"], errors.Keys);
        Assert.Equal(["Informe o CEP com 8 dígitos, no formato 00000-000."], errors["zipCode"]);
    }

    [Fact]
    public async Task ZipCode_ByManager_IsForbidden_D58()
    {
        using var client = Client(UserRoles.Manager);

        var response = await client.GetAsync($"{Inventory}/zip-codes/16985-001", Cancellation);

        await ProblemAssert.IsForbiddenAsync(response, Cancellation);
    }

    // Regiões

    [Fact]
    public async Task Regions_ListAndGet_ToAnyAuthenticatedUser_WithBoundary_D58()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Operator);

        var list = await (await client.GetAsync($"{Inventory}/regions", Cancellation)).ReadAsync<RegionData[]>(Cancellation);
        var region = Assert.Single(list);
        Assert.Equal((scenario.RegionId, "Zona Única", true), (region.Id, region.Name, region.IsActive));
        Assert.NotNull(region.Boundary);

        var response = await client.GetAsync($"{Inventory}/regions/{scenario.RegionId}", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(region.Id, (await response.ReadAsync<RegionData>(Cancellation)).Id);
    }

    [Fact]
    public async Task Region_CreateUpdateAndInactivate_RF020()
    {
        await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);

        var created = await client.PostJsonAsync($"{Inventory}/regions", new RegionRequest { Name = " Zona Leste " }, Cancellation);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var region = await created.ReadAsync<RegionData>(Cancellation);
        Assert.Equal(("Zona Leste", true), (region.Name, region.IsActive));
        Assert.Null(region.Boundary);
        Assert.Equal(region.Id, (await (await client.GetAsync(created.Headers.Location, Cancellation))
            .ReadAsync<RegionData>(Cancellation)).Id);

        var updated = await client.PutJsonAsync(
            $"{Inventory}/regions/{region.Id}", new RegionRequest { Name = "Zona Leste Nova" }, Cancellation);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Zona Leste Nova", (await updated.ReadAsync<RegionData>(Cancellation)).Name);

        var inactivated = await client.PostAsync($"{Inventory}/regions/{region.Id}/inactivation", null, Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, inactivated.StatusCode);

        var active = await (await client.GetAsync($"{Inventory}/regions", Cancellation)).ReadAsync<RegionData[]>(Cancellation);
        Assert.DoesNotContain(active, item => item.Id == region.Id);
        var all = await (await client.GetAsync($"{Inventory}/regions?includeInactive=true", Cancellation))
            .ReadAsync<RegionData[]>(Cancellation);
        Assert.False(all.Single(item => item.Id == region.Id).IsActive);
    }

    [Fact]
    public async Task Region_DuplicateNameIgnoringCaseAndAccents_IsConflict_Plan15_2_1()
    {
        await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);

        var response = await client.PostJsonAsync($"{Inventory}/regions", new RegionRequest { Name = "ZONA UNICA" }, Cancellation);

        await ProblemAssert.IsConflictAsync(response, "Já existe uma região com esse nome.", Cancellation);
    }

    [Fact]
    public async Task Region_InvalidName_IsBadRequestOnName()
    {
        using var client = Client(UserRoles.Administrator);

        var response = await client.PostJsonAsync($"{Inventory}/regions", new RegionRequest { Name = "Z" }, Cancellation);

        var errors = await ProblemAssert.IsValidationAsync(response, Cancellation);
        Assert.Equal(["name"], errors.Keys);
        Assert.Equal(["O nome deve ter entre 2 e 60 caracteres."], errors["name"]);
    }

    [Fact]
    public async Task Region_WithActiveNeighborhood_CannotBeInactivated_RN22()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);

        var response = await client.PostAsync($"{Inventory}/regions/{scenario.RegionId}/inactivation", null, Cancellation);

        await ProblemAssert.IsBusinessRuleAsync(response, "A região tem bairros ativos e não pode ser inativada.", Cancellation);
    }

    [Fact]
    public async Task Region_Unknown_IsNotFound()
    {
        using var client = Client(UserRoles.Administrator);
        var id = Guid.NewGuid();

        await ProblemAssert.IsNotFoundAsync(
            await client.GetAsync($"{Inventory}/regions/{id}", Cancellation), "Região não encontrada.", Cancellation);
        await ProblemAssert.IsNotFoundAsync(
            await client.PutJsonAsync($"{Inventory}/regions/{id}", new RegionRequest { Name = "Zona Norte" }, Cancellation),
            "Região não encontrada.",
            Cancellation);
        await ProblemAssert.IsNotFoundAsync(
            await client.PostAsync($"{Inventory}/regions/{id}/inactivation", null, Cancellation),
            "Região não encontrada.",
            Cancellation);
    }

    // Bairros

    [Fact]
    public async Task Neighborhoods_ListFilteredByRegionAndGet_ToAnyAuthenticatedUser_D58()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Operator);

        var list = await (await client.GetAsync($"{Inventory}/neighborhoods?regionId={scenario.RegionId}", Cancellation))
            .ReadAsync<NeighborhoodData[]>(Cancellation);
        Assert.Equal(
            new[] { InventorySeedData.Downtown, InventorySeedData.Hesperia }.Order(), list.Select(item => item.Name).Order());
        Assert.All(list, item => Assert.Equal("Zona Única", item.RegionName));

        var otherRegion = await client.GetAsync($"{Inventory}/neighborhoods?regionId={Guid.NewGuid()}", Cancellation);
        Assert.Empty(await otherRegion.ReadAsync<NeighborhoodData[]>(Cancellation));

        var response = await client.GetAsync($"{Inventory}/neighborhoods/{scenario.HesperiaId}", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var hesperia = await response.ReadAsync<NeighborhoodData>(Cancellation);
        Assert.Equal((InventorySeedData.Hesperia, new GeoPointData(-22.24, -49.64)), (hesperia.Name, hesperia.Center));
    }

    [Fact]
    public async Task Neighborhood_CreateUpdateAndInactivate_RF020()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);

        var created = await client.PostJsonAsync(
            $"{Inventory}/neighborhoods", Neighborhood("Jardim Órion", scenario.RegionId, -22.22, -49.62), Cancellation);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var neighborhood = await created.ReadAsync<NeighborhoodData>(Cancellation);
        Assert.Equal(("Jardim Órion", scenario.RegionId, new GeoPointData(-22.22, -49.62)),
            (neighborhood.Name, neighborhood.RegionId, neighborhood.Center));
        Assert.Equal(neighborhood.Id, (await (await client.GetAsync(created.Headers.Location, Cancellation))
            .ReadAsync<NeighborhoodData>(Cancellation)).Id);

        var updated = await client.PutJsonAsync(
            $"{Inventory}/neighborhoods/{neighborhood.Id}",
            Neighborhood("Jardim Órion Novo", scenario.RegionId, -22.23, -49.63),
            Cancellation);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var after = await updated.ReadAsync<NeighborhoodData>(Cancellation);
        Assert.Equal(("Jardim Órion Novo", new GeoPointData(-22.23, -49.63)), (after.Name, after.Center));

        var inactivated = await client.PostAsync($"{Inventory}/neighborhoods/{neighborhood.Id}/inactivation", null, Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, inactivated.StatusCode);
        var all = await (await client.GetAsync($"{Inventory}/neighborhoods?includeInactive=true", Cancellation))
            .ReadAsync<NeighborhoodData[]>(Cancellation);
        Assert.False(all.Single(item => item.Id == neighborhood.Id).IsActive);
    }

    [Fact]
    public async Task Neighborhood_CenterOutsideTheMunicipality_IsBadRequestOnCenter_RN02()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);

        var response = await client.PostJsonAsync(
            $"{Inventory}/neighborhoods", Neighborhood("Jardim Órion", scenario.RegionId, -22.5, -49.62), Cancellation);

        var errors = await ProblemAssert.IsValidationAsync(response, Cancellation);
        Assert.Equal(["center"], errors.Keys);
        Assert.Equal(["O ponto central deve ficar dentro do município."], errors["center"]);
    }

    [Fact]
    public async Task Neighborhood_WithoutRegion_IsBadRequestOnRegionId_RN22()
    {
        await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);

        var response = await client.PostJsonAsync(
            $"{Inventory}/neighborhoods", Neighborhood("Jardim Órion", null, -22.22, -49.62), Cancellation);

        var errors = await ProblemAssert.IsValidationAsync(response, Cancellation);
        Assert.Equal(["regionId"], errors.Keys);
        Assert.Equal(["Escolha a região do bairro."], errors["regionId"]);
    }

    [Fact]
    public async Task Neighborhood_WithInactiveRegion_IsBadRequestOnRegionId_RN22()
    {
        await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var region = await (await client.PostJsonAsync(
            $"{Inventory}/regions", new RegionRequest { Name = "Zona Oeste" }, Cancellation)).ReadAsync<RegionData>(Cancellation);
        await client.PostAsync($"{Inventory}/regions/{region.Id}/inactivation", null, Cancellation);

        var response = await client.PostJsonAsync(
            $"{Inventory}/neighborhoods", Neighborhood("Jardim Órion", region.Id, -22.22, -49.62), Cancellation);

        var errors = await ProblemAssert.IsValidationAsync(response, Cancellation);
        Assert.Equal(["regionId"], errors.Keys);
        Assert.Equal(["A região escolhida está inativa."], errors["regionId"]);
    }

    [Fact]
    public async Task Neighborhood_UnknownRegionInTheBody_IsNotFound_Plan15_12()
    {
        await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);

        var response = await client.PostJsonAsync(
            $"{Inventory}/neighborhoods", Neighborhood("Jardim Órion", Guid.NewGuid(), -22.22, -49.62), Cancellation);

        await ProblemAssert.IsNotFoundAsync(response, "Região não encontrada.", Cancellation);
    }

    [Fact]
    public async Task Neighborhood_DuplicateNameIgnoringCaseAndAccents_IsConflict()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);

        var response = await client.PostJsonAsync(
            $"{Inventory}/neighborhoods", Neighborhood("vila hesperia", scenario.RegionId, -22.22, -49.62), Cancellation);

        await ProblemAssert.IsConflictAsync(response, "Já existe um bairro com esse nome.", Cancellation);
    }

    [Fact]
    public async Task Neighborhood_WithActiveBase_CannotBeInactivated_Plan15_2_1()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);

        var response = await client.PostAsync($"{Inventory}/neighborhoods/{scenario.DowntownId}/inactivation", null, Cancellation);

        await ProblemAssert.IsBusinessRuleAsync(
            response, "O bairro tem equipamentos, bases ou agentes ativos e não pode ser inativado.", Cancellation);
    }

    [Fact]
    public async Task Neighborhood_WithActiveEquipment_CannotBeInactivated_Plan15_2_1()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var camera = await client.PostJsonAsync("api/inventory/equipment", scenario.Camera("CAM-001"), Cancellation);
        Assert.Equal(HttpStatusCode.Created, camera.StatusCode);

        var response = await client.PostAsync($"{Inventory}/neighborhoods/{scenario.HesperiaId}/inactivation", null, Cancellation);

        await ProblemAssert.IsBusinessRuleAsync(
            response, "O bairro tem equipamentos, bases ou agentes ativos e não pode ser inativado.", Cancellation);
    }

    [Fact]
    public async Task Neighborhood_Unknown_IsNotFound()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var id = Guid.NewGuid();

        await ProblemAssert.IsNotFoundAsync(
            await client.GetAsync($"{Inventory}/neighborhoods/{id}", Cancellation), "Bairro não encontrado.", Cancellation);
        await ProblemAssert.IsNotFoundAsync(
            await client.PutJsonAsync(
                $"{Inventory}/neighborhoods/{id}", Neighborhood("Jardim Órion", scenario.RegionId, -22.22, -49.62), Cancellation),
            "Bairro não encontrado.",
            Cancellation);
        await ProblemAssert.IsNotFoundAsync(
            await client.PostAsync($"{Inventory}/neighborhoods/{id}/inactivation", null, Cancellation),
            "Bairro não encontrado.",
            Cancellation);
    }

    // Órgãos

    [Fact]
    public async Task Organizations_ListAndGet_ToAnyAuthenticatedUser_D58()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Operator);

        var list = await (await client.GetAsync($"{Inventory}/organizations", Cancellation))
            .ReadAsync<OrganizationData[]>(Cancellation);
        var organization = Assert.Single(list);
        Assert.Equal((scenario.OrganizationId, "Departamento Municipal de Trânsito", InventorySeedData.Organization),
            (organization.Id, organization.Name, organization.Acronym));

        var response = await client.GetAsync($"{Inventory}/organizations/{scenario.OrganizationId}", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(organization, await response.ReadAsync<OrganizationData>(Cancellation));
    }

    [Fact]
    public async Task Organization_CreateUpdateAndInactivate_RF019()
    {
        using var client = Client(UserRoles.Administrator);

        var created = await client.PostJsonAsync(
            $"{Inventory}/organizations", Organization("Agência de Trânsito", "ATR"), Cancellation);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var organization = await created.ReadAsync<OrganizationData>(Cancellation);
        Assert.Equal(("Agência de Trânsito", "ATR", true), (organization.Name, organization.Acronym, organization.IsActive));
        Assert.Equal(organization, await (await client.GetAsync(created.Headers.Location, Cancellation))
            .ReadAsync<OrganizationData>(Cancellation));

        var updated = await client.PutJsonAsync(
            $"{Inventory}/organizations/{organization.Id}", Organization("Agência Municipal de Trânsito", "AMT"), Cancellation);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var after = await updated.ReadAsync<OrganizationData>(Cancellation);
        Assert.Equal(("Agência Municipal de Trânsito", "AMT"), (after.Name, after.Acronym));

        var inactivated = await client.PostAsync($"{Inventory}/organizations/{organization.Id}/inactivation", null, Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, inactivated.StatusCode);
        var again = await client.PostAsync($"{Inventory}/organizations/{organization.Id}/inactivation", null, Cancellation);
        await ProblemAssert.IsBusinessRuleAsync(again, "O órgão já está inativo.", Cancellation);
        var all = await (await client.GetAsync($"{Inventory}/organizations?includeInactive=true", Cancellation))
            .ReadAsync<OrganizationData[]>(Cancellation);
        Assert.False(all.Single(item => item.Id == organization.Id).IsActive);
    }

    [Fact]
    public async Task Organization_DuplicateNameOrAcronym_IsConflict()
    {
        await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);

        var sameName = await client.PostJsonAsync(
            $"{Inventory}/organizations", Organization("DEPARTAMENTO MUNICIPAL DE TRANSITO", "OUTRA"), Cancellation);
        await ProblemAssert.IsConflictAsync(sameName, "Já existe um órgão com esse nome.", Cancellation);

        var sameAcronym = await client.PostJsonAsync(
            $"{Inventory}/organizations", Organization("Outro Departamento", "dmt"), Cancellation);
        await ProblemAssert.IsConflictAsync(sameAcronym, "Já existe um órgão com essa sigla.", Cancellation);
    }

    [Fact]
    public async Task Organization_InvalidAcronym_IsBadRequestOnAcronym()
    {
        using var client = Client(UserRoles.Administrator);

        var response = await client.PostJsonAsync(
            $"{Inventory}/organizations", Organization("Agência de Trânsito", "A"), Cancellation);

        var errors = await ProblemAssert.IsValidationAsync(response, Cancellation);
        Assert.Equal(["acronym"], errors.Keys);
        Assert.Equal(["A sigla deve ter entre 2 e 15 caracteres."], errors["acronym"]);
    }

    [Fact]
    public async Task Organization_Unknown_IsNotFound()
    {
        using var client = Client(UserRoles.Administrator);
        var id = Guid.NewGuid();

        await ProblemAssert.IsNotFoundAsync(
            await client.GetAsync($"{Inventory}/organizations/{id}", Cancellation), "Órgão não encontrado.", Cancellation);
        await ProblemAssert.IsNotFoundAsync(
            await client.PutJsonAsync($"{Inventory}/organizations/{id}", Organization("Agência", "AG"), Cancellation),
            "Órgão não encontrado.",
            Cancellation);
        await ProblemAssert.IsNotFoundAsync(
            await client.PostAsync($"{Inventory}/organizations/{id}/inactivation", null, Cancellation),
            "Órgão não encontrado.",
            Cancellation);
    }

    // Bases operacionais

    [Fact]
    public async Task Bases_ListFilteredByOrganizationAndGet_ToAnyAuthenticatedUser_D58()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Operator);

        var list = await (await client.GetAsync($"{Inventory}/bases?organizationId={scenario.OrganizationId}", Cancellation))
            .ReadAsync<OperationalBaseData[]>(Cancellation);
        var operationalBase = Assert.Single(list);
        Assert.Equal((scenario.BaseId, InventorySeedData.Base, InventorySeedData.Organization),
            (operationalBase.Id, operationalBase.Name, operationalBase.OrganizationAcronym));
        Assert.Equal((InventorySeedData.Downtown, "Prometeia", "SP", "16985-002"),
            (operationalBase.Location.NeighborhoodName, operationalBase.Location.City, operationalBase.Location.State,
                operationalBase.Location.ZipCode));

        var otherOrganization = await client.GetAsync($"{Inventory}/bases?organizationId={Guid.NewGuid()}", Cancellation);
        Assert.Empty(await otherOrganization.ReadAsync<OperationalBaseData[]>(Cancellation));

        var response = await client.GetAsync($"{Inventory}/bases/{scenario.BaseId}", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(scenario.BaseId, (await response.ReadAsync<OperationalBaseData>(Cancellation)).Id);
    }

    [Fact]
    public async Task Base_CreateUpdateAndInactivate_RF019()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);

        var created = await client.PostJsonAsync(
            $"{Inventory}/bases", Base("Base Hespéria", scenario.OrganizationId, scenario.HesperiaLocation()), Cancellation);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var operationalBase = await created.ReadAsync<OperationalBaseData>(Cancellation);
        Assert.Equal(("Base Hespéria", scenario.HesperiaId, "número 181"),
            (operationalBase.Name, operationalBase.Location.NeighborhoodId, operationalBase.Location.Reference));
        Assert.Equal(operationalBase.Id, (await (await client.GetAsync(created.Headers.Location, Cancellation))
            .ReadAsync<OperationalBaseData>(Cancellation)).Id);

        var updated = await client.PutJsonAsync(
            $"{Inventory}/bases/{operationalBase.Id}",
            Base("Base Centro Sul", scenario.OrganizationId, scenario.DowntownLocation()),
            Cancellation);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var after = await updated.ReadAsync<OperationalBaseData>(Cancellation);
        Assert.Equal(("Base Centro Sul", scenario.DowntownId, "Avenida Brasil"),
            (after.Name, after.Location.NeighborhoodId, after.Location.Street));

        var inactivated = await client.PostAsync($"{Inventory}/bases/{operationalBase.Id}/inactivation", null, Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, inactivated.StatusCode);
        var all = await (await client.GetAsync($"{Inventory}/bases?includeInactive=true", Cancellation))
            .ReadAsync<OperationalBaseData[]>(Cancellation);
        Assert.False(all.Single(item => item.Id == operationalBase.Id).IsActive);
    }

    [Fact]
    public async Task Base_LocationOutsideTheMunicipality_IsBadRequestOnLocationCoordinates_RN02()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var outside = InventoryScenario.Location(scenario.HesperiaId, "Rua das Flores", "S/N", -22.5, -49.641);

        var response = await client.PostJsonAsync(
            $"{Inventory}/bases", Base("Base Hespéria", scenario.OrganizationId, outside), Cancellation);

        var errors = await ProblemAssert.IsValidationAsync(response, Cancellation);
        Assert.Equal(["location.latitude", "location.longitude"], errors.Keys.Order());
        Assert.Equal(["O ponto está fora do município."], errors["location.latitude"]);
    }

    [Fact]
    public async Task Base_DuplicateNameInTheSameOrganization_IsConflict_Plan15_2_3()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);

        var response = await client.PostJsonAsync(
            $"{Inventory}/bases", Base("BASE CENTRAL", scenario.OrganizationId, scenario.HesperiaLocation()), Cancellation);

        await ProblemAssert.IsConflictAsync(response, "Já existe uma base com esse nome neste órgão.", Cancellation);
    }

    [Fact]
    public async Task Base_WithActiveEquipment_CannotBeInactivated_Plan15_2_3()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var motorcycle = await client.PostJsonAsync("api/inventory/equipment", scenario.Motorcycle("MOTO-001"), Cancellation);
        Assert.Equal(HttpStatusCode.Created, motorcycle.StatusCode);

        var response = await client.PostAsync($"{Inventory}/bases/{scenario.BaseId}/inactivation", null, Cancellation);

        await ProblemAssert.IsBusinessRuleAsync(
            response, "A base tem equipamentos ou agentes ativos e não pode ser inativada.", Cancellation);
    }

    [Fact]
    public async Task Base_UnknownOrganizationInTheBody_IsNotFound_Plan15_12()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);

        var response = await client.PostJsonAsync(
            $"{Inventory}/bases", Base("Base Hespéria", Guid.NewGuid(), scenario.HesperiaLocation()), Cancellation);

        await ProblemAssert.IsNotFoundAsync(response, "Órgão não encontrado.", Cancellation);
    }

    [Fact]
    public async Task Base_Unknown_IsNotFound()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var id = Guid.NewGuid();

        await ProblemAssert.IsNotFoundAsync(
            await client.GetAsync($"{Inventory}/bases/{id}", Cancellation), "Base operacional não encontrada.", Cancellation);
        await ProblemAssert.IsNotFoundAsync(
            await client.PutJsonAsync(
                $"{Inventory}/bases/{id}", Base("Base Hespéria", scenario.OrganizationId, scenario.HesperiaLocation()), Cancellation),
            "Base operacional não encontrada.",
            Cancellation);
        await ProblemAssert.IsNotFoundAsync(
            await client.PostAsync($"{Inventory}/bases/{id}/inactivation", null, Cancellation),
            "Base operacional não encontrada.",
            Cancellation);
    }

    // Tipos de equipamento

    [Fact]
    public async Task EquipmentTypes_ListTheTenCatalogTypesWithDefinitions_AndGet_ToAnyAuthenticatedUser_D58()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Operator);

        var list = await (await client.GetAsync($"{Inventory}/equipment-types", Cancellation))
            .ReadAsync<EquipmentTypeData[]>(Cancellation);
        Assert.Equal(10, list.Length);

        var response = await client.GetAsync($"{Inventory}/equipment-types/{scenario.RadarTypeId}", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var radar = await response.ReadAsync<EquipmentTypeData>(Cancellation);
        Assert.Equal(("traffic-radar", EquipmentCategory.Fixed), (radar.Key, radar.Category));
        Assert.Equal(["modality", "maxSpeedKmh", "monitoredLanes", "roadDirection"], radar.Attributes.Select(item => item.Key));
        var maxSpeed = radar.Attributes.Single(item => item.Key == "maxSpeedKmh");
        Assert.False(maxSpeed.IsRequired);
        Assert.Equal(("modality", "speed"), (maxSpeed.RequiredWhen?.AttributeKey, maxSpeed.RequiredWhen?.OptionKey));
    }

    [Fact]
    public async Task EquipmentType_Unknown_IsNotFound()
    {
        using var client = Client(UserRoles.Operator);

        var response = await client.GetAsync($"{Inventory}/equipment-types/{Guid.NewGuid()}", Cancellation);

        await ProblemAssert.IsNotFoundAsync(response, "Tipo de equipamento não encontrado.", Cancellation);
    }

    // Permissões

    [Theory]
    [InlineData("regions")]
    [InlineData("neighborhoods")]
    [InlineData("organizations")]
    [InlineData("bases")]
    public async Task Writes_ByManager_AreForbidden_D58(string register)
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Manager);
        var existingId = register switch
        {
            "regions" => scenario.RegionId,
            "neighborhoods" => scenario.HesperiaId,
            "organizations" => scenario.OrganizationId,
            _ => scenario.BaseId,
        };
        object body = register switch
        {
            "regions" => new RegionRequest { Name = "Zona Leste" },
            "neighborhoods" => Neighborhood("Jardim Órion", scenario.RegionId, -22.22, -49.62),
            "organizations" => Organization("Agência de Trânsito", "ATR"),
            _ => Base("Base Hespéria", scenario.OrganizationId, scenario.HesperiaLocation()),
        };

        await ProblemAssert.IsForbiddenAsync(
            await client.PostJsonAsync($"{Inventory}/{register}", body, Cancellation), Cancellation);
        await ProblemAssert.IsForbiddenAsync(
            await client.PutJsonAsync($"{Inventory}/{register}/{existingId}", body, Cancellation), Cancellation);
        await ProblemAssert.IsForbiddenAsync(
            await client.PostAsync($"{Inventory}/{register}/{existingId}/inactivation", null, Cancellation), Cancellation);
    }

    [Fact]
    public async Task Lists_WithoutToken_AreUnauthenticated()
    {
        using var client = Fixture.Factory.CreateClient();

        var response = await client.GetAsync($"{Inventory}/regions", Cancellation);

        await ProblemAssert.IsUnauthenticatedAsync(response, Cancellation);
    }

    private HttpClient Client(string role) => Fixture.CreateUserClient(Fixture.Tokens.For(role));

    private static NeighborhoodRequest Neighborhood(string name, Guid? regionId, double latitude, double longitude) =>
        new() { Name = name, RegionId = regionId, Center = new GeoPointInput { Latitude = latitude, Longitude = longitude } };

    private static OrganizationRequest Organization(string name, string acronym) => new() { Name = name, Acronym = acronym };

    private static OperationalBaseRequest Base(string name, Guid organizationId, LocationInput location) =>
        new() { Name = name, OrganizationId = organizationId, Location = location };
}
