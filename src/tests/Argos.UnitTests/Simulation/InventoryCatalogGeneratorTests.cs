using System.Text.Json;
using Argos.Domain.Inventory.Equipments;
using Argos.Domain.Inventory.Geography;
using Argos.Domain.Inventory.Support;
using Argos.Domain.Inventory.Types;
using Argos.Simulation.Generators;
using Argos.Simulation.Geography;
using Argos.Simulation.Models;

namespace Argos.UnitTests.Simulation;

/// <summary>
/// Catálogo inicial gerado (RF-026, Art. IX.1, IX.3, IX.4; D-56, D-62; plan 15.11.2, 15.11.3): determinismo,
/// composição, distribuição geográfica e conferência contra as regras do Domínio, para que a carga não falhe.
/// </summary>
public sealed class InventoryCatalogGeneratorTests
{
    private const int Seed = 42;

    private static readonly PrometeiaGeography Geography = PrometeiaGeography.Load();
    private static readonly SimulatedInventoryCatalog Catalog = InventoryCatalogGenerator.Generate(Seed, Geography);

    private static readonly string[] CrewTypes = ["traffic-agent-motorcycle", "patrol-vehicle"];

    [Fact]
    public void SameSeed_GeneratesSameCatalog_ArtIX3()
    {
        Assert.Equal(Serialize(Catalog), Serialize(InventoryCatalogGenerator.Generate(Seed, Geography)));
    }

    [Fact]
    public void DifferentSeeds_GenerateDifferentCatalogs_ArtIX3()
    {
        Assert.NotEqual(Serialize(Catalog), Serialize(InventoryCatalogGenerator.Generate(Seed + 1, Geography)));
    }

    [Fact]
    public void Organizations_AreTheFiveOfPlan15112()
    {
        Assert.Equal(["ÁGORA", "GCM", "PM", "ROTAPRO", "SMM"], Catalog.Organizations.Select(o => o.Acronym).Order());
    }

    [Fact]
    public void Bases_AreTheNineOfPlan15112_InsideMunicipality()
    {
        Assert.Equal(9, Catalog.Bases.Count);
        Assert.Equal(9, Catalog.Bases.Select(b => (b.OrganizationAcronym, b.Name)).Distinct().Count());
        Assert.All(Catalog.Bases, b => Assert.True(Inside(b.Location)));
        Assert.Equal("Centro", RegionOf(Catalog.Bases.Single(b => b.Name == InventoryCatalogGenerator.CentralDeMobilidade).Location));
        Assert.Equal("Zona Norte", RegionOf(Catalog.Bases.Single(b => b.Name == InventoryCatalogGenerator.InspetoriaNorte).Location));
    }

    [Fact]
    public void Composition_200FixedAnd100Mobile_ByType_Plan15113()
    {
        Assert.Equal(200, Catalog.Equipment.Count(e => e.Location is not null));
        Assert.Equal(100, Catalog.Equipment.Count(e => e.BaseName is not null));
        Assert.Equal(
            new Dictionary<string, int>
            {
                ["traffic-radar"] = 36,
                ["surveillance-camera"] = 90,
                ["truck-scale"] = 6,
                ["automated-gate"] = 24,
                ["parking-kiosk"] = 44,
                ["parking-enforcement-vehicle"] = 4,
                ["passenger-transport-vehicle"] = 36,
                ["electric-scooter"] = 30,
                ["traffic-agent-motorcycle"] = 12,
                ["patrol-vehicle"] = 18,
            },
            Catalog.Equipment.GroupBy(e => e.TypeKey).ToDictionary(g => g.Key, g => g.Count()));
    }

    [Fact]
    public void Composition_OrganizationsAndSubtypes_Plan15113()
    {
        Assert.Equal(60, Count("surveillance-camera", "GCM"));
        Assert.Equal(30, Count("surveillance-camera", "SMM"));
        Assert.Equal(12, Count("automated-gate", "SMM"));
        Assert.Equal(12, Count("automated-gate", "ROTAPRO"));
        Assert.Equal(24, Count("electric-scooter", "ÁGORA"));
        Assert.Equal(6, Count("electric-scooter", "SMM"));
        Assert.Equal(10, Count("patrol-vehicle", "GCM"));
        Assert.Equal(8, Count("patrol-vehicle", "PM"));
        Assert.Equal(
            new Dictionary<string, int> { ["taxi"] = 18, ["motorcycle-taxi"] = 10, ["school"] = 8 },
            Catalog.Equipment.Where(e => e.TypeKey == "passenger-transport-vehicle")
                .GroupBy(e => (string)e.Attributes["modality"])
                .ToDictionary(g => g.Key, g => g.Count()));
    }

    [Fact]
    public void MobileEquipment_UseBasesOfTheirOrganization_Plan15113()
    {
        var bases = Catalog.Bases.ToDictionary(b => b.Name);

        Assert.All(Catalog.Equipment.Where(e => e.BaseName is not null), e =>
            Assert.Equal(e.OrganizationAcronym, bases[e.BaseName!].OrganizationAcronym));
        Assert.All(Catalog.Equipment.Where(e => e.TypeKey == "traffic-agent-motorcycle"), e =>
            Assert.Equal(InventoryCatalogGenerator.CentralDeMobilidade, e.BaseName));
    }

    [Fact]
    public void Statuses_FollowTheShares_Plan15113()
    {
        var counts = Catalog.Equipment.GroupBy(e => e.FinalStatus).ToDictionary(g => g.Key, g => g.Count());

        Assert.InRange(counts[SimulatedEquipmentStatus.Operational], 240, 264);
        Assert.InRange(counts[SimulatedEquipmentStatus.Malfunction], 12, 18);
        Assert.InRange(counts[SimulatedEquipmentStatus.UnderMaintenance], 6, 12);
        Assert.InRange(counts[SimulatedEquipmentStatus.NotInstalled], 9, 15);
        Assert.InRange(counts[SimulatedEquipmentStatus.Deactivated], 6, 12);
        Assert.InRange(counts[SimulatedEquipmentStatus.Stolen], 1, 6);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(2026)]
    public void CrewTypes_AreNeverDeactivatedOrStolen_D62(int seed)
    {
        var catalog = InventoryCatalogGenerator.Generate(seed, Geography);

        Assert.DoesNotContain(catalog.Equipment, e => CrewTypes.Contains(e.TypeKey)
            && e.FinalStatus is SimulatedEquipmentStatus.Deactivated or SimulatedEquipmentStatus.Stolen);
    }

    [Fact]
    public void OperationalPeriodics_AboutFivePercentOverdueAndEightUpcoming_RN10()
    {
        var periodics = Catalog.Equipment
            .Where(e => e is { FinalStatus: SimulatedEquipmentStatus.Operational, PeriodicMaintenance: not null })
            .Select(e => e.PeriodicMaintenance!.DueInDays)
            .ToArray();

        var overdue = periodics.Count(days => days < 0) / (double)periodics.Length;
        var upcoming = periodics.Count(days => days is >= 0 and <= 7) / (double)periodics.Length;
        Assert.InRange(overdue, 0.04, 0.06);
        Assert.InRange(upcoming, 0.07, 0.09);
    }

    [Fact]
    public void AllFixedPoints_InsideMunicipality_RN02_ArtIX4()
    {
        Assert.All(Catalog.Equipment.Where(e => e.Location is not null), e => Assert.True(Inside(e.Location!)));
    }

    [Fact]
    public void Locations_UseZipCodeStreetAndNeighborhoodOfTheSameRecord_ArtIX4()
    {
        var streets = Geography.Streets.ToDictionary(street => street.ZipCode);

        Assert.All(LocationsOf(Catalog), location =>
        {
            var street = streets[location.ZipCode];
            Assert.Equal(street.Name, location.Street);
            Assert.Equal(street.Neighborhood, location.Neighborhood);
        });
    }

    [Fact]
    public void Radars_OnMainStreetsOfAllZones_Plan15113()
    {
        var radars = Catalog.Equipment.Where(e => e.TypeKey == "traffic-radar").ToArray();

        Assert.All(radars, radar => Assert.NotEqual(StreetClass.Local, StreetOf(radar.Location!).Class));
        Assert.Equal(5, radars.Select(radar => RegionOf(radar.Location!)).Distinct().Count());
        Assert.InRange(radars.Count(radar => (string)radar.Attributes["modality"] == "speed"), 24, 27);
    }

    [Fact]
    public void Scales_OnMajorOrSecondaryStreetsNearTheBoundary_Plan15113()
    {
        var scales = Catalog.Equipment.Where(e => e.TypeKey == "truck-scale").ToArray();

        Assert.All(scales, scale =>
        {
            var point = new GeoCoordinate(scale.Location!.Latitude, scale.Location.Longitude);
            Assert.NotEqual(StreetClass.Local, StreetOf(scale.Location).Class);
            Assert.True(GeoMath.DistanceToRingMeters(Geography.Municipality.Boundary, point)
                <= InventoryCatalogGenerator.ScaleMaxDistanceToBoundaryMeters);
        });
    }

    [Fact]
    public void Cameras_AboutFortyPercentInCentro_Plan15113()
    {
        var cameras = Catalog.Equipment.Where(e => e.TypeKey == "surveillance-camera").ToArray();

        Assert.InRange(cameras.Count(camera => RegionOf(camera.Location!) == "Centro") / (double)cameras.Length, 0.35, 0.45);
    }

    [Fact]
    public void References_FollowRn03Format_RN03()
    {
        Assert.All(LocationsOf(Catalog), location =>
            Assert.Matches("^(número [0-9]+|oposto ao número [0-9]+|S/N)$", location.Reference));
    }

    [Fact]
    public void BrandAndSerial_AndPlates_AreUnique_RN01()
    {
        Assert.Equal(300, Catalog.Equipment.Select(e => (e.Brand, e.SerialNumber)).Distinct().Count());

        var plates = Catalog.Equipment.Where(e => e.Attributes.ContainsKey("licensePlate"))
            .Select(e => (string)e.Attributes["licensePlate"]).ToArray();
        Assert.Equal(plates.Length, plates.Distinct().Count());
        Assert.All(plates, plate => Assert.Matches("^[A-Z]{3}[0-9][A-Z][0-9]{2}$", plate));
    }

    [Fact]
    public void Catalog_FollowsTheDomainTypes_RN06_US02()
    {
        var types = EquipmentTypeCatalog.CreateAll().ToDictionary(type => type.Key);

        Assert.All(Catalog.Equipment, e =>
        {
            var type = types[e.TypeKey];
            Assert.Equal(e.Location is not null, type.Category == EquipmentCategory.Fixed);
            type.ValidateAttributes(e.Attributes.ToDictionary(entry => entry.Key, entry => (object?)entry.Value));
        });
    }

    /// <summary>
    /// Aplica o catálogo inteiro às operações do Domínio, como a carga vai fazer (plan 15.11.4): cadastro, manutenção
    /// periódica e mudança de situação. Nenhuma operação pode ser recusada, e as situações finais batem.
    /// </summary>
    [Fact]
    public void Catalog_PassesTheDomainOperations_RN02_RN05_RN07_RN08()
    {
        var anchor = new DateTimeOffset(2026, 10, 18, 15, 0, 0, TimeSpan.Zero);
        var municipality = Municipality.Create(
            Geography.Municipality.Name,
            Geography.Municipality.State,
            Geography.Municipality.TimeZoneId,
            GeoPolygon.FromRing(Geography.Municipality.Boundary.Select(p => GeoPoint.Create(p.Latitude, p.Longitude))),
            GeoPoint.Create(Geography.Municipality.Center.Latitude, Geography.Municipality.Center.Longitude));
        var regions = Geography.Regions.ToDictionary(region => region.Name, region => Region.Create(region.Name));
        var neighborhoods = Geography.Neighborhoods.ToDictionary(
            neighborhood => neighborhood.Name,
            neighborhood => Neighborhood.Create(
                neighborhood.Name,
                regions[neighborhood.Region],
                GeoPoint.Create(neighborhood.Center.Latitude, neighborhood.Center.Longitude),
                municipality));
        var organizations = Catalog.Organizations.ToDictionary(o => o.Acronym, o => Organization.Create(o.Name, o.Acronym));
        var bases = Catalog.Bases.ToDictionary(
            b => b.Name,
            b => OperationalBase.Create(b.Name, organizations[b.OrganizationAcronym], ToLocation(b.Location)));
        var types = EquipmentTypeCatalog.CreateAll().ToDictionary(type => type.Key);
        var today = municipality.DateOf(anchor);

        foreach (var simulated in Catalog.Equipment)
        {
            var registeredAt = anchor - simulated.RegisteredBeforeAnchor;
            var equipment = Equipment.Register(
                types[simulated.TypeKey],
                simulated.SerialNumber,
                simulated.Brand,
                simulated.Model,
                organizations[simulated.OrganizationAcronym],
                simulated.Location is null ? null : ToLocation(simulated.Location),
                simulated.BaseName is null ? null : bases[simulated.BaseName],
                simulated.Attributes.ToDictionary(entry => entry.Key, entry => (object?)entry.Value),
                Enum.Parse<EquipmentStatus>(simulated.InitialStatus.ToString()),
                registeredAt,
                "simulação");

            if (simulated.PeriodicMaintenance is { } periodic)
            {
                equipment.RegisterMaintenance(
                    MaintenanceKind.Periodic,
                    today.AddDays(periodic.DueInDays),
                    periodic.PeriodicityDays,
                    periodic.Description,
                    municipality.DateOf(registeredAt),
                    registeredAt,
                    "simulação");
            }

            if (simulated.StatusChange is { } change)
            {
                equipment.ChangeStatus(
                    Enum.Parse<EquipmentStatus>(change.Target.ToString()),
                    change.Justification,
                    canReactivate: true,
                    anchor - change.BeforeAnchor,
                    "simulação");
            }

            Assert.Equal(simulated.FinalStatus.ToString(), equipment.Status.ToString());
            Assert.Equal(
                simulated.FinalStatus == SimulatedEquipmentStatus.Malfunction,
                equipment.HasOpenImmediateMaintenance);
        }

        Location ToLocation(SimulatedLocation location) => Location.Create(
            municipality,
            neighborhoods[location.Neighborhood],
            location.ZipCode,
            location.Street,
            location.Reference,
            location.Latitude,
            location.Longitude);
    }

    private static int Count(string typeKey, string organization) =>
        Catalog.Equipment.Count(e => e.TypeKey == typeKey && e.OrganizationAcronym == organization);

    private static IEnumerable<SimulatedLocation> LocationsOf(SimulatedInventoryCatalog catalog) =>
        catalog.Equipment.Select(e => e.Location).OfType<SimulatedLocation>().Concat(catalog.Bases.Select(b => b.Location));

    private static bool Inside(SimulatedLocation location) =>
        GeoMath.Contains(Geography.Municipality.Boundary, new GeoCoordinate(location.Latitude, location.Longitude));

    private static PrometeiaStreet StreetOf(SimulatedLocation location) =>
        Geography.Streets.Single(street => street.ZipCode == location.ZipCode);

    private static string RegionOf(SimulatedLocation location) => Geography.Neighborhood(location.Neighborhood).Region;

    private static string Serialize(SimulatedInventoryCatalog catalog) => JsonSerializer.Serialize(catalog);
}
