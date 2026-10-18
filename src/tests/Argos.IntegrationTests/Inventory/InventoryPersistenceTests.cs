using Argos.Application.Common.Abstractions;
using Argos.Application.Common.Exceptions;
using Argos.Application.Inventory;
using Argos.Application.Inventory.Equipments;
using Argos.Application.Inventory.Geography;
using Argos.Application.Inventory.Support;
using Argos.Application.Inventory.Types;
using Argos.Application.Seeding;
using Argos.Application.Seeding.Inventory;
using Argos.Contracts.Common;
using Argos.Contracts.Inventory;
using Argos.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using DomainEquipment = Argos.Domain.Inventory.Equipments.Equipment;
using DomainEquipmentStatus = Argos.Domain.Inventory.Equipments.EquipmentStatus;
using DomainLocation = Argos.Domain.Inventory.Geography.Location;
using EquipmentTypeCatalog = Argos.Domain.Inventory.Types.EquipmentTypeCatalog;

namespace Argos.IntegrationTests.Inventory;

/// <summary>
/// Persistência do Inventário contra o PostgreSQL real (T062, T063): as pendências de implementação da seção 12 do plano
/// (localização como tipo complexo sem <c>DELETE</c>, comparador do <c>jsonb</c>, necessidade de manutenção traduzida em
/// subconsultas e limpeza das tabelas novas) e a tradução dos conflitos do banco em 409 (D-60, RN-01). Os casos de uso
/// são chamados direto pela DI da API; os endpoints são testados no T074 e no T075.
/// </summary>
public sealed class InventoryPersistenceTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    private const string Saint = InventorySeedData.Saint;
    private const string Downtown = InventorySeedData.Downtown;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task InventoryServices_AreResolvedFromTheContainer()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        Type[] registered =
        [
            typeof(MunicipalityService), typeof(RegionService), typeof(NeighborhoodService), typeof(OrganizationService),
            typeof(OperationalBaseService), typeof(EquipmentTypeService), typeof(EquipmentQueryService),
            typeof(EquipmentService), typeof(EquipmentStatusService), typeof(MaintenanceService),
            typeof(BusinessParameterProvisioningService), typeof(InventoryReferenceSeeder), typeof(EquipmentCatalogSeeder),
        ];

        Assert.All(registered, type => Assert.NotNull(services.GetRequiredService(type)));
    }

    [Fact]
    public async Task ChangingLocation_UpdatesColumnsInPlace_WithoutDelete_Plan12()
    {
        var scenario = await SeedReferenceAsync();
        var created = await CreateCameraAsync(scenario, "CAM-001", scenario.SaintId);

        var updated = await UseAsync<EquipmentService, EquipmentDetails>(service => service.UpdateAsync(
            created.Id,
            new UpdateEquipmentRequest
            {
                SerialNumber = created.SerialNumber,
                Brand = created.Brand,
                Model = created.Model,
                OrganizationId = scenario.OrganizationId,
                Location = Location(scenario.DowntownId, "Avenida Brasil", "oposto ao número 50", -22.265, -49.665),
                Attributes = CameraAttributes("full-hd-1080p"),
                Version = created.Version,
            },
            Cancellation));

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(Downtown, updated.NeighborhoodName);
        Assert.Equal("Avenida Brasil", updated.Location!.Street);
        Assert.Equal(-22.265, updated.Location.Latitude);
        Assert.NotEqual(created.Version, updated.Version);

        var history = await UseAsync<EquipmentQueryService, PagedResult<StatusChangeData>>(
            service => service.ListStatusChangesAsync(created.Id, new PageQuery(), Cancellation));
        Assert.Equal(1, history.TotalCount);
    }

    [Fact]
    public async Task OnlyNewMaintenance_DoesNotRewriteEquipmentRow_AttributesComparedByContent_Plan12()
    {
        var scenario = await SeedReferenceAsync();
        var created = await CreateCameraAsync(scenario, "CAM-002", scenario.SaintId);

        await UseAsync<MaintenanceService, MaintenanceData>(service => service.RegisterAsync(
            created.Id, new RegisterMaintenanceRequest { Kind = MaintenanceKind.Immediate }, Cancellation));
        var afterMaintenance = await DetailsAsync(created.Id);

        Assert.Equal(created.Version, afterMaintenance.Version);
        Assert.Equal(MaintenanceNeed.Immediate, afterMaintenance.MaintenanceNeed);

        var updated = await UseAsync<EquipmentService, EquipmentDetails>(service => service.UpdateAsync(
            created.Id,
            new UpdateEquipmentRequest
            {
                SerialNumber = created.SerialNumber,
                Brand = created.Brand,
                Model = created.Model,
                OrganizationId = scenario.OrganizationId,
                Location = SaintLocation(scenario),
                Attributes = CameraAttributes("qhd-2k"),
                Version = afterMaintenance.Version,
            },
            Cancellation));

        Assert.NotEqual(afterMaintenance.Version, updated.Version);
        Assert.Equal("qhd-2k", updated.Attributes.Single(attribute => attribute.Key == "resolution").Value);
        Assert.Equal("2K", updated.Attributes.Single(attribute => attribute.Key == "resolution").DisplayValue);
    }

    [Fact]
    public async Task SearchMaintenanceNeed_MatchesDomainCalculation_D54()
    {
        var scenario = await SeedReferenceAsync();
        var today = await UseAsync<MunicipalCalendar, DateOnly>(calendar => calendar.TodayAsync(Cancellation));

        var none = await CreateCameraAsync(scenario, "NEED-NONE", scenario.SaintId);
        var scheduled = await CreateCameraAsync(scenario, "NEED-SCHEDULED", scenario.SaintId, Scheduled(today.AddDays(30)));
        var upcomingEdge = await CreateCameraAsync(scenario, "NEED-UPCOMING", scenario.SaintId, Scheduled(today.AddDays(7)));
        var overdue = await CreateCameraAsync(scenario, "NEED-OVERDUE", scenario.SaintId, Periodic(today.AddDays(-2)));
        await UseAsync<MaintenanceService, MaintenanceData>(service => service.RegisterAsync(
            overdue.Id, Scheduled(today.AddDays(3)), Cancellation));
        var immediate = await CreateCameraAsync(
            scenario, "NEED-IMMEDIATE", scenario.SaintId, status: EquipmentStatus.Malfunction);
        var mobileToday = await CreateMotorcycleAsync(scenario, "NEED-MOBILE", Periodic(today));

        var expected = new Dictionary<Guid, MaintenanceNeed>
        {
            [none.Id] = MaintenanceNeed.None,
            [scheduled.Id] = MaintenanceNeed.Scheduled,
            [upcomingEdge.Id] = MaintenanceNeed.Upcoming,
            [overdue.Id] = MaintenanceNeed.Overdue,
            [immediate.Id] = MaintenanceNeed.Immediate,
            [mobileToday.Id] = MaintenanceNeed.Upcoming,
        };

        var all = await SearchAsync(new EquipmentSearchQuery());
        Assert.Equal(expected.Count, all.TotalCount);

        foreach (var summary in all.Items)
        {
            var details = await DetailsAsync(summary.Id);
            Assert.Equal(details.MaintenanceNeed, summary.MaintenanceNeed);
            Assert.Equal(expected[summary.Id], summary.MaintenanceNeed);
        }

        var urgent = await SearchAsync(new EquipmentSearchQuery
        {
            MaintenanceNeed = [MaintenanceNeed.Overdue, MaintenanceNeed.Immediate],
        });
        Assert.Equal(new[] { overdue.Id, immediate.Id }.Order(), urgent.Items.Select(item => item.Id).Order());
    }

    [Fact]
    public async Task Search_IgnoresAccentsAndCase_EscapesWildcards_AndCombinesFilters_US03()
    {
        var scenario = await SeedReferenceAsync();
        var fixedInSaint = await CreateCameraAsync(scenario, "SRCH-001", scenario.SaintId);
        var fixedDowntown = await CreateCameraAsync(scenario, "SRCH-002", scenario.DowntownId);
        var mobile = await CreateMotorcycleAsync(scenario, "SRCH-003");

        var byNeighborhood = await SearchAsync(new EquipmentSearchQuery { Search = "SAO jorge" });
        Assert.Equal([fixedInSaint.Id], byNeighborhood.Items.Select(item => item.Id));

        var bySerial = await SearchAsync(new EquipmentSearchQuery { Search = "srch-00" });
        Assert.Equal(3, bySerial.TotalCount);

        var downtown = await SearchAsync(new EquipmentSearchQuery { NeighborhoodId = [scenario.DowntownId] });
        Assert.Equal(new[] { fixedDowntown.Id, mobile.Id }.Order(), downtown.Items.Select(item => item.Id).Order());
        Assert.Equal(scenario.RegionId, downtown.Items.Single(item => item.Id == mobile.Id).RegionId);

        var combined = await SearchAsync(new EquipmentSearchQuery
        {
            NeighborhoodId = [scenario.DowntownId],
            Category = [EquipmentCategory.Fixed],
        });
        Assert.Equal([fixedDowntown.Id], combined.Items.Select(item => item.Id));

        var wildcard = await SearchAsync(new EquipmentSearchQuery { Search = "%" });
        Assert.Empty(wildcard.Items);

        var firstPage = await SearchAsync(new EquipmentSearchQuery { Sort = EquipmentSortFields.SerialNumber, PageSize = 2 });
        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(["SRCH-001", "SRCH-002"], firstPage.Items.Select(item => item.SerialNumber));

        var byStatus = await SearchAsync(new EquipmentSearchQuery { Sort = EquipmentSortFields.Status });
        Assert.Equal(3, byStatus.Items.Count);
    }

    [Fact]
    public async Task CompletingPeriodicMaintenance_PersistsTheWholeAggregate_RN09()
    {
        var scenario = await SeedReferenceAsync();
        var today = await UseAsync<MunicipalCalendar, DateOnly>(calendar => calendar.TodayAsync(Cancellation));
        var created = await CreateCameraAsync(
            scenario, "CAM-003", scenario.SaintId, Periodic(today.AddDays(-1)), EquipmentStatus.UnderMaintenance);
        var periodic = created.OpenMaintenances.Single();

        var completion = await UseAsync<MaintenanceService, MaintenanceCompletionData>(service => service.CompleteAsync(
            created.Id,
            periodic.Id,
            new CompleteMaintenanceRequest { Result = MaintenanceResult.Success, CompletedAt = DateTimeOffset.UtcNow },
            Cancellation));

        Assert.Equal(EquipmentStatus.Operational, completion.Status);
        Assert.Equal(today.AddDays(30), completion.NextPeriodic!.DueDate);

        var maintenances = await UseAsync<EquipmentQueryService, IReadOnlyList<MaintenanceData>>(
            service => service.ListMaintenancesAsync(created.Id, state: null, Cancellation));
        Assert.Equal(
            [(MaintenanceState.Open, completion.NextPeriodic.Id), (MaintenanceState.Completed, periodic.Id)],
            maintenances.Select(maintenance => (maintenance.State, maintenance.Id)).OrderBy(pair => pair.State));

        var history = await UseAsync<EquipmentQueryService, PagedResult<StatusChangeData>>(
            service => service.ListStatusChangesAsync(created.Id, new PageQuery(), Cancellation));
        Assert.Equal(2, history.TotalCount);
        Assert.Equal(StatusChangeOrigin.MaintenanceCompletion, history.Items[0].Origin);
        Assert.Equal(periodic.Id, history.Items[0].MaintenanceId);
    }

    [Fact]
    public async Task DuplicateBrandAndSerial_CaughtByUniqueIndex_ReturnsConflictWithExistingId_RN01()
    {
        var scenario = await SeedReferenceAsync();
        var existing = await CreateCameraAsync(scenario, "ABC-123", scenario.SaintId);

        // Grava direto pelas portas, sem a verificação da Aplicação, como a segunda de duas gravações simultâneas.
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var type = await services.GetRequiredService<IEquipmentTypeRepository>()
            .FindByKeyAsync(EquipmentTypeCatalog.SurveillanceCamera, Cancellation);
        var organization = await services.GetRequiredService<IOrganizationRepository>()
            .FindByIdAsync(scenario.OrganizationId, Cancellation);
        var municipality = await services.GetRequiredService<IMunicipalityRepository>().GetAsync(Cancellation);
        var neighborhood = await services.GetRequiredService<INeighborhoodRepository>()
            .FindByIdAsync(scenario.SaintId, Cancellation);
        var duplicate = DomainEquipment.Register(
            type!,
            "abc-123",
            existing.Brand.ToLowerInvariant(),
            "Outro modelo",
            organization,
            DomainLocation.Create(municipality!, neighborhood!, "16985-001", "Rua das Flores", "número 10", -22.241, -49.641),
            null,
            CameraAttributes("hd-720p").ToDictionary(entry => entry.Key, entry => (object?)entry.Value),
            DomainEquipmentStatus.Operational,
            DateTimeOffset.UtcNow,
            "teste");
        services.GetRequiredService<IEquipmentRepository>().Add(duplicate);

        var conflict = await Assert.ThrowsAsync<ConflictException>(
            () => services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Cancellation));

        Assert.Equal(EquipmentService.DuplicateBrandAndSerialMessage, conflict.Message);
        Assert.Equal(existing.Id, conflict.Extensions[EquipmentService.ExistingEquipmentIdExtension]);
    }

    [Fact]
    public async Task ConcurrentStatusChanges_SecondSaveReturnsConflict_D60()
    {
        var scenario = await SeedReferenceAsync();
        var created = await CreateCameraAsync(scenario, "CAM-004", scenario.SaintId);

        await using var first = Fixture.Factory.Services.CreateAsyncScope();
        await using var second = Fixture.Factory.Services.CreateAsyncScope();
        var firstCopy = await first.ServiceProvider.GetRequiredService<IEquipmentRepository>()
            .FindByIdAsync(created.Id, Cancellation);
        var secondCopy = await second.ServiceProvider.GetRequiredService<IEquipmentRepository>()
            .FindByIdAsync(created.Id, Cancellation);

        secondCopy!.ChangeStatus(DomainEquipmentStatus.Malfunction, null, canReactivate: false, DateTimeOffset.UtcNow, "segundo");
        await second.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Cancellation);

        firstCopy!.ChangeStatus(DomainEquipmentStatus.UnderMaintenance, null, canReactivate: false, DateTimeOffset.UtcNow, "primeiro");
        await Assert.ThrowsAsync<ConflictException>(
            () => first.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Cancellation));

        Assert.Equal(EquipmentStatus.Malfunction, (await DetailsAsync(created.Id)).Status);
    }

    [Fact]
    public async Task DatabaseCleaner_EmptiesTheInventoryTables_Plan8_2()
    {
        var scenario = await SeedReferenceAsync();
        await CreateCameraAsync(scenario, "CAM-005", scenario.SaintId);

        await Fixture.ResetDatabaseAsync();

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IInventoryQueries>();
        Assert.Null(await queries.GetMunicipalityAsync(Cancellation));
        Assert.Empty(await queries.ListEquipmentTypesAsync(includeInactive: true, Cancellation));
        Assert.Empty(await queries.ListOrganizationsAsync(includeInactive: true, Cancellation));
        Assert.Equal(
            0, (await queries.SearchEquipmentAsync(new EquipmentSearchQuery(), DateOnly.MinValue, 7, Cancellation)).TotalCount);
        Assert.Null(await scope.ServiceProvider.GetRequiredService<ISeedLedger>()
            .FindAppliedSeedAsync(InventorySeed.ReferenceVersion, Cancellation));
    }

    /// <summary>Referência pequena de <see cref="InventorySeedData"/>, pela carga <c>inventory-reference-v1</c>.</summary>
    private async Task<Scenario> SeedReferenceAsync()
    {
        await UseAsync<InventoryReferenceSeeder, SeedResult>(seeder => seeder.SeedAsync(
            InventorySeedData.Reference(), seed: 42, registeredAt: DateTimeOffset.UtcNow.AddDays(-1), Cancellation));

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IInventoryQueries>();
        var neighborhoods = await queries.ListNeighborhoodsAsync(regionId: null, includeInactive: false, Cancellation);
        var types = await queries.ListEquipmentTypesAsync(includeInactive: false, Cancellation);
        var saint = neighborhoods.Single(neighborhood => neighborhood.Name == Saint);

        return new Scenario(
            saint.RegionId,
            saint.Id,
            neighborhoods.Single(neighborhood => neighborhood.Name == Downtown).Id,
            (await queries.ListOrganizationsAsync(includeInactive: false, Cancellation)).Single().Id,
            (await queries.ListOperationalBasesAsync(organizationId: null, includeInactive: false, Cancellation)).Single().Id,
            types.Single(type => type.Key == EquipmentTypeCatalog.SurveillanceCamera).Id,
            types.Single(type => type.Key == EquipmentTypeCatalog.TrafficAgentMotorcycle).Id);
    }

    private Task<EquipmentDetails> CreateCameraAsync(
        Scenario scenario,
        string serialNumber,
        Guid neighborhoodId,
        RegisterMaintenanceRequest? initialMaintenance = null,
        EquipmentStatus status = EquipmentStatus.Operational) =>
        UseAsync<EquipmentService, EquipmentDetails>(service => service.CreateAsync(
            new CreateEquipmentRequest
            {
                TypeId = scenario.CameraTypeId,
                SerialNumber = serialNumber,
                Brand = "Ômega Vision",
                Model = "OV-200",
                OrganizationId = scenario.OrganizationId,
                Location = neighborhoodId == scenario.SaintId
                    ? SaintLocation(scenario)
                    : Location(neighborhoodId, "Avenida Brasil", "número 200", -22.262, -49.662),
                Attributes = CameraAttributes("full-hd-1080p"),
                Status = status,
                InitialMaintenance = initialMaintenance,
            },
            Cancellation));

    private Task<EquipmentDetails> CreateMotorcycleAsync(
        Scenario scenario, string serialNumber, RegisterMaintenanceRequest? initialMaintenance = null) =>
        UseAsync<EquipmentService, EquipmentDetails>(service => service.CreateAsync(
            new CreateEquipmentRequest
            {
                TypeId = scenario.MotorcycleTypeId,
                SerialNumber = serialNumber,
                Brand = "Moto Sul",
                Model = "MS 300",
                OrganizationId = scenario.OrganizationId,
                BaseId = scenario.BaseId,
                Attributes = new Dictionary<string, string?> { ["licensePlate"] = "ABC1D23", ["prefix"] = $"M-{serialNumber}" },
                Status = EquipmentStatus.Operational,
                InitialMaintenance = initialMaintenance,
            },
            Cancellation));

    private static LocationInput SaintLocation(Scenario scenario) =>
        Location(scenario.SaintId, "Rua das Flores", "número 181", -22.241, -49.641);

    private static LocationInput Location(Guid neighborhoodId, string street, string reference, double latitude, double longitude) =>
        new()
        {
            ZipCode = "16985-001",
            Street = street,
            NeighborhoodId = neighborhoodId,
            Reference = reference,
            Latitude = latitude,
            Longitude = longitude,
        };

    private static Dictionary<string, string?> CameraAttributes(string resolution) =>
        new() { ["resolution"] = resolution, ["colorMode"] = "color", ["nightVision"] = "true", ["panTiltZoom"] = "false" };

    private static RegisterMaintenanceRequest Scheduled(DateOnly dueDate) =>
        new() { Kind = MaintenanceKind.Scheduled, DueDate = dueDate };

    private static RegisterMaintenanceRequest Periodic(DateOnly dueDate) =>
        new() { Kind = MaintenanceKind.Periodic, DueDate = dueDate, PeriodicityDays = 30 };

    private Task<EquipmentDetails> DetailsAsync(Guid id) =>
        UseAsync<EquipmentQueryService, EquipmentDetails>(service => service.GetDetailsAsync(id, Cancellation));

    private Task<PagedResult<EquipmentSummary>> SearchAsync(EquipmentSearchQuery query) =>
        UseAsync<EquipmentQueryService, PagedResult<EquipmentSummary>>(service => service.SearchAsync(query, Cancellation));

    /// <summary>Executa um caso de uso num escopo próprio, como uma requisição.</summary>
    private async Task<TResult> UseAsync<TService, TResult>(Func<TService, Task<TResult>> action)
        where TService : notnull
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    private sealed record Scenario(
        Guid RegionId,
        Guid SaintId,
        Guid DowntownId,
        Guid OrganizationId,
        Guid BaseId,
        Guid CameraTypeId,
        Guid MotorcycleTypeId);
}
