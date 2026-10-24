using Argos.Application.Common.Exceptions;
using Argos.Contracts.Inventory;
using Argos.Domain.Common;
using Argos.Domain.Inventory;
using Argos.Domain.Inventory.Geography;
using Argos.Domain.Inventory.Support;
using Argos.UnitTests.Domain.Inventory;
using DomainAgentRole = Argos.Domain.Inventory.Agents.AgentRole;

namespace Argos.UnitTests.Application.Inventory;

/// <summary>
/// Cadastros de apoio (RF-004, RF-019, RF-020, RN-02, RN-22; D-49, D-52, D-53; plan 15.12): unicidade por chave
/// normalizada, registro cadastral só quando algo muda, nomes no lugar dos ids de referência e prefixo
/// <c>location.</c> nos erros.
/// </summary>
public sealed class SupportRegisterServicesTests
{
    private readonly InventoryServices _services = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateRegion_SavesOnceAndLogsCreationWithAuthor_D49()
    {
        var region = await _services.RegionService().CreateAsync(new RegionRequest { Name = "  Zona Norte " }, Ct);

        Assert.Equal("Zona Norte", region.Name);
        Assert.Equal(1, _services.UnitOfWork.SaveCalls);
        var change = Assert.Single(_services.Inventory.Changes);
        Assert.Equal((InventoryEntityType.Region, region.Id, InventoryChangeKind.Created), (change.EntityType, change.EntityId, change.Kind));
        Assert.Equal("admin", change.Author);
        Assert.Equal(InventoryTestData.Now, change.OccurredAt);
    }

    [Theory]
    [InlineData("centro")]
    [InlineData("CENTRO")]
    [InlineData(" Céntro ")]
    public async Task CreateRegion_NameRepeatedIgnoringCaseAndAccents_IsConflict_D53(string name)
    {
        await Assert.ThrowsAsync<ConflictException>(() => _services.RegionService().CreateAsync(new RegionRequest { Name = name }, Ct));

        Assert.Equal(0, _services.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task CreateRegion_NameRepeatedOfInactiveRegion_IsConflict_D53()
    {
        _services.Region.Inactivate(hasActiveNeighborhoods: false, InventoryTestData.Now, "admin");

        await Assert.ThrowsAsync<ConflictException>(() => _services.RegionService().CreateAsync(new RegionRequest { Name = "Centro" }, Ct));
    }

    [Fact]
    public async Task UpdateRegion_WithoutChange_DoesNotSaveNorLog_D49()
    {
        var region = await _services.RegionService().UpdateAsync(_services.Region.Id, new RegionRequest { Name = "Centro" }, Ct);

        Assert.Equal("Centro", region.Name);
        Assert.Equal(0, _services.UnitOfWork.SaveCalls);
        Assert.Empty(_services.Inventory.Changes);
    }

    [Fact]
    public async Task UpdateRegion_Renamed_LogsBeforeAndAfter_D49()
    {
        await _services.RegionService().UpdateAsync(_services.Region.Id, new RegionRequest { Name = "Centro Histórico" }, Ct);

        var change = Assert.Single(_services.Inventory.Changes);
        Assert.Equal(InventoryChangeKind.Updated, change.Kind);
        Assert.Equal(new FieldChange("name", "Nome", "Centro", "Centro Histórico"), Assert.Single(change.Changes));
        Assert.Equal(1, _services.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task UpdateRegion_Unknown_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _services.RegionService().UpdateAsync(Guid.NewGuid(), new RegionRequest { Name = "Nova" }, Ct));
    }

    [Fact]
    public async Task InactivateRegion_WithActiveNeighborhood_IsRefused_RN22()
    {
        await Assert.ThrowsAsync<InventoryRuleException>(() => _services.RegionService().InactivateAsync(_services.Region.Id, Ct));

        Assert.True(_services.Region.IsActive);
        Assert.Equal(0, _services.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task InactivateRegion_WithoutActiveNeighborhood_LogsInactivation_RN23()
    {
        var region = await _services.RegionService().CreateAsync(new RegionRequest { Name = "Zona Leste" }, Ct);

        await _services.RegionService().InactivateAsync(region.Id, Ct);

        Assert.False(_services.Inventory.Regions.Single(candidate => candidate.Id == region.Id).IsActive);
        Assert.Equal(InventoryChangeKind.Inactivated, _services.Inventory.Changes[^1].Kind);
    }

    [Fact]
    public async Task UpdateNeighborhood_NewRegion_LogsRegionNamesInsteadOfIds_D49()
    {
        var north = Region.Create("Zona Norte");
        _services.Inventory.Regions.Add(north);

        await _services.NeighborhoodService().UpdateAsync(
            _services.Neighborhood.Id,
            new NeighborhoodRequest
            {
                Name = "Jardim das Musas",
                RegionId = north.Id,
                Center = new GeoPointInput { Latitude = -22.20, Longitude = -49.65 },
            }, Ct);

        var change = Assert.Single(Assert.Single(_services.Inventory.Changes).Changes);
        Assert.Equal(new FieldChange("regionId", "Região", "Centro", "Zona Norte"), change);
    }

    [Fact]
    public async Task CreateNeighborhood_CenterOutsideMunicipality_IsErrorOnCenter_RN02()
    {
        var exception = await Assert.ThrowsAsync<DomainValidationException>(() => _services.NeighborhoodService().CreateAsync(
            new NeighborhoodRequest
            {
                Name = "Vila Nova",
                RegionId = _services.Region.Id,
                Center = new GeoPointInput { Latitude = -23.50, Longitude = -46.60 },
            }, Ct));

        Assert.Equal(["center"], exception.Errors.Keys);
    }

    [Fact]
    public async Task CreateNeighborhood_InvalidCoordinate_IsErrorUnderCenter_D46()
    {
        var exception = await Assert.ThrowsAsync<DomainValidationException>(() => _services.NeighborhoodService().CreateAsync(
            new NeighborhoodRequest
            {
                Name = "Vila Nova",
                RegionId = _services.Region.Id,
                Center = new GeoPointInput { Latitude = 95, Longitude = -49.65 },
            }, Ct));

        Assert.Equal(["center.latitude"], exception.Errors.Keys);
    }

    [Fact]
    public async Task CreateNeighborhood_UnknownRegion_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _services.NeighborhoodService().CreateAsync(new NeighborhoodRequest
        {
            Name = "Vila Nova",
            RegionId = Guid.NewGuid(),
            Center = new GeoPointInput { Latitude = -22.20, Longitude = -49.65 },
        }, Ct));
    }

    [Fact]
    public async Task InactivateNeighborhood_WithActiveBase_IsRefused_RN22()
    {
        await Assert.ThrowsAsync<InventoryRuleException>(
            () => _services.NeighborhoodService().InactivateAsync(_services.Neighborhood.Id, Ct));
    }

    [Fact]
    public async Task CreateOrganization_AcronymRepeatedIgnoringCase_IsConflict_D53()
    {
        var exception = await Assert.ThrowsAsync<ConflictException>(() => _services.OrganizationService().CreateAsync(
            new OrganizationRequest { Name = "Superintendência Municipal de Mobilidade", Acronym = "smm" }, Ct));

        Assert.Equal("Já existe um órgão com essa sigla.", exception.Message);
    }

    [Fact]
    public async Task UpdateOrganization_SameValues_DoesNotSave_D49()
    {
        await _services.OrganizationService().UpdateAsync(
            _services.Organization.Id, new OrganizationRequest { Name = "Secretaria Municipal de Mobilidade", Acronym = "SMM" }, Ct);

        Assert.Equal(0, _services.UnitOfWork.SaveCalls);
        Assert.Empty(_services.Inventory.Changes);
    }

    [Fact]
    public async Task CreateBase_InvalidLocation_ErrorsUnderLocationPrefix_D46()
    {
        var exception = await Assert.ThrowsAsync<DomainValidationException>(() => _services.OperationalBaseService().CreateAsync(
            new OperationalBaseRequest
            {
                Name = "Terminal Urbano",
                OrganizationId = _services.Organization.Id,
                Location = Location(zipCode: "1698", latitude: -23.50, reference: " "),
            }, Ct));

        Assert.Equal(
            ["location.latitude", "location.longitude", "location.reference", "location.zipCode"],
            exception.Errors.Keys.Order());
    }

    [Fact]
    public async Task CreateBase_SameNameInSameOrganization_IsConflict_RN22()
    {
        await Assert.ThrowsAsync<ConflictException>(() => _services.OperationalBaseService().CreateAsync(new OperationalBaseRequest
        {
            Name = "central de mobilidade",
            OrganizationId = _services.Organization.Id,
            Location = Location(),
        }, Ct));
    }

    [Fact]
    public async Task CreateBase_SameNameInOtherOrganization_IsAccepted()
    {
        var guard = Organization.Create("Guarda Civil Municipal", "GCM");
        _services.Inventory.Organizations.Add(guard);

        var created = await _services.OperationalBaseService().CreateAsync(new OperationalBaseRequest
        {
            Name = "Central de Mobilidade",
            OrganizationId = guard.Id,
            Location = Location(),
        }, Ct);

        Assert.Equal("GCM", created.OrganizationAcronym);
        Assert.Equal("Jardim das Musas", created.Location.NeighborhoodName);
        Assert.Equal("Prometeia", created.Location.City);
    }

    [Fact]
    public async Task UpdateBase_NewOrganizationAndNeighborhood_LogsNames_D49()
    {
        var guard = Organization.Create("Guarda Civil Municipal", "GCM");
        var other = Neighborhood.Create("Vila Aurora", _services.Region, InventoryTestData.Point(-22.25, -49.70), _services.Municipality);
        _services.Inventory.Organizations.Add(guard);
        _services.Inventory.Neighborhoods.Add(other);

        await _services.OperationalBaseService().UpdateAsync(_services.Base.Id, new OperationalBaseRequest
        {
            Name = "Central de Mobilidade",
            OrganizationId = guard.Id,
            Location = Location(neighborhoodId: other.Id),
        }, Ct);

        var changes = Assert.Single(_services.Inventory.Changes).Changes;
        Assert.Contains(new FieldChange("organizationId", "Órgão", "Secretaria Municipal de Mobilidade", "Guarda Civil Municipal"), changes);
        Assert.Contains(new FieldChange("location.neighborhoodId", "Bairro", "Jardim das Musas", "Vila Aurora"), changes);
    }

    [Fact]
    public async Task InactivateBase_WithActiveEquipment_IsRefused()
    {
        _services.Inventory.Equipment.Add(EquipmentTestData.NewPatrol(_services.Base, _services.Organization));

        await Assert.ThrowsAsync<InventoryRuleException>(() => _services.OperationalBaseService().InactivateAsync(_services.Base.Id, Ct));
    }

    [Fact]
    public async Task InactivateBase_WithActiveAgentAndNoEquipment_IsRefusedWithTheDomainMessage_RF019()
    {
        _services.Inventory.Agents.Add(AgentTestData.NewAgent(
            "AT-0001", _services.Organization, _services.Base, DomainAgentRole.TrafficAgent, rank: null));

        var exception = await Assert.ThrowsAsync<InventoryRuleException>(
            () => _services.OperationalBaseService().InactivateAsync(_services.Base.Id, Ct));

        Assert.Equal("A base tem equipamentos ou agentes ativos e não pode ser inativada.", exception.Message);
        Assert.True(_services.Base.IsActive);
        Assert.Equal(0, _services.UnitOfWork.SaveCalls);
        Assert.Empty(_services.Inventory.Changes);
    }

    [Fact]
    public async Task InactivateBase_OnlyWithInactiveAgent_IsInactivated_RF019()
    {
        var agent = AgentTestData.NewAgent("AT-0002", _services.Organization, _services.Base, DomainAgentRole.TrafficAgent, rank: null);
        agent.Inactivate("Desligado do órgão.", InventoryTestData.Now, "admin");
        _services.Inventory.Agents.Add(agent);

        await _services.OperationalBaseService().InactivateAsync(_services.Base.Id, Ct);

        Assert.False(_services.Base.IsActive);
        Assert.Equal(1, _services.UnitOfWork.SaveCalls);
        var change = Assert.Single(_services.Inventory.Changes);
        Assert.Equal(
            (InventoryEntityType.OperationalBase, _services.Base.Id, InventoryChangeKind.Inactivated),
            (change.EntityType, change.EntityId, change.Kind));
    }

    [Theory]
    [InlineData("1698-5001")]
    [InlineData("ABCDE-123")]
    public async Task SuggestZipCode_InvalidFormat_IsErrorOnZipCode_RN02(string zipCode)
    {
        var exception = await Assert.ThrowsAsync<DomainValidationException>(() => _services.MunicipalityService().SuggestAsync(zipCode, Ct));

        Assert.Equal(["zipCode"], exception.Errors.Keys);
    }

    [Fact]
    public async Task SuggestZipCode_Unknown_IsNotFound_D52()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _services.MunicipalityService().SuggestAsync("16985-999", Ct));
    }

    [Fact]
    public async Task SuggestZipCode_Known_ReturnsStreetNeighborhoodRegionAndCity_RF004()
    {
        _services.Inventory.ZipCodes.Add(ZipCodeEntry.Create(
            ZipCode.Parse("16985-001"), "Rua das Acácias", _services.Neighborhood, InventoryTestData.Point(-22.21, -49.66), _services.Municipality));

        var suggestion = await _services.MunicipalityService().SuggestAsync("16985001", Ct);

        Assert.Equal(
            ("16985-001", "Rua das Acácias", "Jardim das Musas", "Centro", "Prometeia", "SP"),
            (suggestion.ZipCode, suggestion.Street, suggestion.NeighborhoodName, suggestion.RegionName, suggestion.City, suggestion.State));
    }

    private LocationInput Location(
        string zipCode = "16985-001",
        double latitude = -22.21,
        string reference = "número 181",
        Guid? neighborhoodId = null) => new()
        {
            ZipCode = zipCode,
            Street = "Rua das Acácias",
            NeighborhoodId = neighborhoodId ?? _services.Neighborhood.Id,
            Reference = reference,
            Latitude = latitude,
            Longitude = -49.66,
        };
}
