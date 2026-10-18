using Argos.Application.Common.Exceptions;
using Argos.Application.Seeding;
using Argos.Application.Seeding.Inventory;
using Argos.Domain.Common.Parameters;
using Argos.Domain.Inventory;
using Argos.Domain.Inventory.Equipments;
using Argos.Domain.Inventory.Types;
using Argos.UnitTests.Application.Fakes;

namespace Argos.UnitTests.Application.Seeding;

/// <summary>
/// Carga do Inventário (RN-34; Art. IX.2, IX.3, XII.7; D-55, D-56; plan 15.10, 15.11.4): as versões gravam uma vez só,
/// pelas operações do Domínio, com o autor <c>simulação</c> e os instantes relativos à âncora; os parâmetros de
/// negócio só são criados quando faltam.
/// </summary>
public sealed class InventorySeedingTests
{
    private const int Seed = 20261018;

    private static readonly DateTimeOffset Anchor = new(2026, 10, 18, 15, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ReferenceRegisteredAt = Anchor.AddDays(-200);

    private readonly FakeInventory _inventory = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Reference_FirstRun_CreatesEverythingInOneSaveWithLedgerRecord_D56()
    {
        var result = await ReferenceSeeder().SeedAsync(Reference(), Seed, ReferenceRegisteredAt, Ct);

        Assert.False(result.AlreadyApplied);
        Assert.Equal(1, _inventory.UnitOfWork.SaveCalls);
        Assert.Equal(Seed, _inventory.Ledger[InventorySeed.ReferenceVersion]);
        Assert.Single(_inventory.Municipalities);
        Assert.Equal(2, _inventory.Regions.Count);
        Assert.Equal(2, _inventory.Neighborhoods.Count);
        Assert.Single(_inventory.ZipCodes);
        Assert.Equal(2, _inventory.Organizations.Count);
        Assert.Equal(2, _inventory.Bases.Count);
        Assert.Equal(10, _inventory.Types.Count);
        Assert.Equal(
            new Dictionary<string, int>
            {
                ["regions"] = 2, ["neighborhoods"] = 2, ["zipCodes"] = 1, ["organizations"] = 2, ["bases"] = 2, ["equipmentTypes"] = 10,
            },
            result.Counts);
    }

    [Fact]
    public async Task Reference_RegistrationLog_HasSimulationAuthorAndGivenInstant_ArtIX2()
    {
        await ReferenceSeeder().SeedAsync(Reference(), Seed, ReferenceRegisteredAt, Ct);

        Assert.Equal(2 + 2 + 2 + 2 + 10, _inventory.Changes.Count);
        Assert.All(_inventory.Changes, change =>
        {
            Assert.Equal(InventoryChangeKind.Created, change.Kind);
            Assert.Equal("simulação", change.Author);
            Assert.Equal(ReferenceRegisteredAt, change.OccurredAt);
        });
    }

    [Fact]
    public async Task Reference_SecondRunWithSameSeed_WritesNothing_ArtXII7()
    {
        await ReferenceSeeder().SeedAsync(Reference(), Seed, ReferenceRegisteredAt, Ct);

        var second = await ReferenceSeeder().SeedAsync(Reference(), Seed, ReferenceRegisteredAt, Ct);

        Assert.True(second.AlreadyApplied);
        Assert.Equal(1, _inventory.UnitOfWork.SaveCalls);
        Assert.Equal(2, _inventory.Regions.Count);
    }

    [Fact]
    public async Task Reference_AlreadyAppliedWithOtherSeed_StopsWithClearMessage_ArtIX3()
    {
        await ReferenceSeeder().SeedAsync(Reference(), Seed, ReferenceRegisteredAt, Ct);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ReferenceSeeder().SeedAsync(Reference(), Seed + 1, ReferenceRegisteredAt, Ct));

        Assert.Contains("down -v", exception.Message);
        Assert.Equal(1, _inventory.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Catalog_BeforeReference_Fails()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => CatalogSeeder().SeedAsync(Catalog(), Seed, Anchor, Ct));

        Assert.Empty(_inventory.Equipment);
        Assert.Equal(0, _inventory.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Catalog_FirstRun_CreatesEquipmentInOneSaveWithLedgerRecord_D56()
    {
        await ReferenceSeeder().SeedAsync(Reference(), Seed, ReferenceRegisteredAt, Ct);

        var result = await CatalogSeeder().SeedAsync(Catalog(), Seed, Anchor, Ct);

        Assert.Equal(new Dictionary<string, int> { ["equipment"] = 4 }, result.Counts);
        Assert.Equal(4, _inventory.Equipment.Count);
        Assert.Equal(Seed, _inventory.Ledger[InventorySeed.CatalogVersion]);
        Assert.Equal(2, _inventory.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Catalog_SecondRunWithSameSeed_WritesNothing_ArtXII7()
    {
        await ReferenceSeeder().SeedAsync(Reference(), Seed, ReferenceRegisteredAt, Ct);
        await CatalogSeeder().SeedAsync(Catalog(), Seed, Anchor, Ct);

        var second = await CatalogSeeder().SeedAsync(Catalog(), Seed, Anchor, Ct);

        Assert.True(second.AlreadyApplied);
        Assert.Equal(4, _inventory.Equipment.Count);
        Assert.Equal(2, _inventory.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Catalog_EquipmentInMalfunction_HasImmediateOpen_RN07()
    {
        await SeedAllAsync();

        var malfunction = Equipment("RD-0002");

        Assert.Equal(EquipmentStatus.Malfunction, malfunction.Status);
        Assert.True(malfunction.HasOpenImmediateMaintenance);
        Assert.Equal(MaintenanceOrigin.AutomaticOnMalfunction, Assert.Single(malfunction.Maintenances).Origin);
    }

    [Fact]
    public async Task Catalog_BusinessRecords_HaveSimulationAuthorAndInstantsRelativeToAnchor_D56()
    {
        await SeedAllAsync();

        var radar = Equipment("RD-0001");
        var registration = Assert.Single(radar.StatusChanges);
        Assert.Equal(("simulação", Anchor.AddDays(-180)), (registration.Author, registration.OccurredAt));

        var periodic = Assert.Single(radar.Maintenances);
        Assert.Equal(new DateOnly(2026, 10, 18).AddDays(-3), periodic.DueDate);
        Assert.Equal("simulação", periodic.OpenedBy);
        Assert.Equal(MaintenanceNeed.Overdue, radar.CalculateMaintenanceNeed(new DateOnly(2026, 10, 18), 7));
    }

    [Fact]
    public async Task Catalog_StatusChangeToStolen_CancelsPeriodicMaintenance_RN11()
    {
        await SeedAllAsync();

        var camera = Equipment("CM-0001");
        var change = camera.StatusChanges.Last();

        Assert.Equal(EquipmentStatus.Stolen, camera.Status);
        Assert.Equal((Anchor.AddDays(-2), "simulação", "Furto registrado em boletim."), (change.OccurredAt, change.Author, change.Justification));
        Assert.Equal(MaintenanceState.Cancelled, Assert.Single(camera.Maintenances).State);
    }

    [Fact]
    public async Task Catalog_MobileEquipment_UsesTheBaseOfItsOrganization_RF007()
    {
        await SeedAllAsync();

        var patrol = Equipment("PT-0001");

        Assert.Equal(_inventory.Bases.Single(operationalBase => operationalBase.Name == "Sede").Id, patrol.BaseId);
        Assert.Null(patrol.Location);
    }

    [Fact]
    public async Task Parameters_FirstRun_CreatesMissingWithConfiguredValue_RN34()
    {
        var created = await ParameterService().ProvisionAsync(
            new Dictionary<string, int> { ["maintenance.upcoming-window-days"] = 10 }, Ct);

        Assert.Equal(["maintenance.upcoming-window-days"], created);
        Assert.Equal(10, _inventory.Parameters[BusinessParameter.MaintenanceUpcomingWindowDays]);
        Assert.Equal(1, _inventory.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Parameters_WithoutConfiguration_UsesSpecDefault_RN34()
    {
        await ParameterService().ProvisionAsync(null, Ct);

        Assert.Equal(7, _inventory.Parameters[BusinessParameter.MaintenanceUpcomingWindowDays]);
    }

    [Fact]
    public async Task Parameters_AlreadyStored_AreNeverOverwritten_ArtIII9()
    {
        _inventory.Parameters[BusinessParameter.MaintenanceUpcomingWindowDays] = 14;

        var created = await ParameterService().ProvisionAsync(
            new Dictionary<string, int> { ["maintenance.upcoming-window-days"] = 10 }, Ct);

        Assert.Empty(created);
        Assert.Equal(14, _inventory.Parameters[BusinessParameter.MaintenanceUpcomingWindowDays]);
        Assert.Equal(0, _inventory.UnitOfWork.SaveCalls);
    }

    [Theory]
    [InlineData("maintenance.upcoming-window-days", 61)]
    [InlineData("maintenance.unknown", 7)]
    public async Task Parameters_InvalidConfiguration_IsRefusedWithTheKey_RN34(string key, int value)
    {
        var exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => ParameterService().ProvisionAsync(new Dictionary<string, int> { [key] = value }, Ct));

        Assert.Equal([$"BusinessParameters:{key}"], exception.Errors.Keys);
        Assert.Empty(_inventory.Parameters);
    }

    private async Task SeedAllAsync()
    {
        await ReferenceSeeder().SeedAsync(Reference(), Seed, ReferenceRegisteredAt, Ct);
        await CatalogSeeder().SeedAsync(Catalog(), Seed, Anchor, Ct);
    }

    private Equipment Equipment(string serialNumber) =>
        _inventory.Equipment.Single(equipment => equipment.SerialNumber == serialNumber);

    private InventoryReferenceSeeder ReferenceSeeder() =>
        new(_inventory, _inventory, _inventory, _inventory, _inventory, _inventory, _inventory, _inventory, _inventory, _inventory.UnitOfWork);

    private EquipmentCatalogSeeder CatalogSeeder() =>
        new(_inventory, _inventory, _inventory, _inventory, _inventory, _inventory, _inventory, _inventory.UnitOfWork);

    private BusinessParameterProvisioningService ParameterService() => new(_inventory, _inventory.UnitOfWork);

    /// <summary>Município quadrado com duas zonas lado a lado, um bairro em cada, dois órgãos e uma base de cada.</summary>
    private static InventoryReferenceSeed Reference() => new(
        new MunicipalitySeed("Prometeia", "SP", "America/Sao_Paulo", Square(-22.30, -49.80, -22.10, -49.50), new SeedPoint(-22.20, -49.65)),
        [
            new RegionSeed("Centro", Square(-22.30, -49.80, -22.10, -49.65)),
            new RegionSeed("Zona Leste", Square(-22.30, -49.65, -22.10, -49.50)),
        ],
        [
            new NeighborhoodSeed("Jardim das Musas", "Centro", new SeedPoint(-22.20, -49.70)),
            new NeighborhoodSeed("Vila Aurora", "Zona Leste", new SeedPoint(-22.20, -49.60)),
        ],
        [new ZipCodeSeed("16985-001", "Rua das Acácias", "Jardim das Musas", new SeedPoint(-22.21, -49.71))],
        [
            new OrganizationSeed("SMM", "Secretaria Municipal de Mobilidade"),
            new OrganizationSeed("GCM", "Guarda Civil Municipal"),
        ],
        [
            new OperationalBaseSeed("Central de Mobilidade", "SMM", Location("Jardim das Musas", -22.21, -49.71)),
            new OperationalBaseSeed("Sede", "GCM", Location("Vila Aurora", -22.19, -49.59)),
        ]);

    private static EquipmentCatalogSeed Catalog() => new(
    [
        Radar("RD-0001", EquipmentStatus.Operational, new PeriodicMaintenanceSeed(-3, 365, "Aferição anual"), null),
        Radar("RD-0002", EquipmentStatus.Malfunction, null, null),
        new EquipmentSeed(
            EquipmentTypeCatalog.SurveillanceCamera,
            "CM-0001",
            "Tecnovia",
            "TC-4K",
            "GCM",
            Location("Vila Aurora", -22.18, -49.58),
            null,
            new Dictionary<string, object?>
            {
                ["resolution"] = "uhd-4k", ["nightVision"] = true, ["colorMode"] = "color", ["panTiltZoom"] = false,
            },
            TimeSpan.FromDays(170),
            EquipmentStatus.Operational,
            new PeriodicMaintenanceSeed(40, 180, "Revisão semestral"),
            new StatusChangeSeed(EquipmentStatus.Stolen, TimeSpan.FromDays(2), "Furto registrado em boletim.")),
        new EquipmentSeed(
            EquipmentTypeCatalog.PatrolVehicle,
            "PT-0001",
            "Orbis",
            "Patrulha 4x4",
            "GCM",
            null,
            "Sede",
            new Dictionary<string, object?> { ["licensePlate"] = "GCM1A23", ["prefix"] = "GCM-01", ["vehicleKind"] = "patrol-car" },
            TimeSpan.FromDays(160),
            EquipmentStatus.Operational,
            null,
            null),
    ]);

    private static EquipmentSeed Radar(
        string serialNumber, EquipmentStatus status, PeriodicMaintenanceSeed? periodic, StatusChangeSeed? change) => new(
        EquipmentTypeCatalog.TrafficRadar,
        serialNumber,
        "Vigilis",
        "VX-300",
        "SMM",
        Location("Jardim das Musas", -22.22, -49.72),
        null,
        new Dictionary<string, object?> { ["modality"] = "speed", ["maxSpeedKmh"] = 60L, ["monitoredLanes"] = 2L },
        TimeSpan.FromDays(180),
        status,
        periodic,
        change);

    private static LocationSeed Location(string neighborhood, double latitude, double longitude) =>
        new("16985-001", "Rua das Acácias", neighborhood, "número 181", latitude, longitude);

    private static IReadOnlyList<SeedPoint> Square(double south, double west, double north, double east) =>
    [
        new(north, west),
        new(north, east),
        new(south, east),
        new(south, west),
        new(north, west),
    ];
}
