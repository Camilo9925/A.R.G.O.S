using System.Text.RegularExpressions;
using Argos.Simulation.Geography;

namespace Argos.UnitTests.Simulation;

/// <summary>
/// Coerência da geografia de Prometeia lida do recurso embutido (Art. IX.4; D-51; plan 15.11.1).
/// </summary>
public sealed partial class PrometeiaGeographyTests
{
    private static readonly PrometeiaGeography Geography = PrometeiaGeography.Load();

    [Fact]
    public void Municipality_IsPrometeiaSP_ArtIX4()
    {
        Assert.Equal("Prometeia", Geography.Municipality.Name);
        Assert.Equal("SP", Geography.Municipality.State);
        Assert.Equal("America/Sao_Paulo", Geography.Municipality.TimeZoneId);
        Assert.True(GeoMath.Contains(Geography.Municipality.Boundary, Geography.Municipality.Center));
    }

    [Fact]
    public void Rings_AreClosedOnLoad_Plan15111()
    {
        var rings = Geography.Regions.Select(region => region.Boundary).Prepend(Geography.Municipality.Boundary);

        Assert.All(rings, ring =>
        {
            Assert.True(ring.Count >= 4);
            Assert.Equal(ring[0], ring[^1]);
        });
    }

    [Fact]
    public void Regions_AreTheFiveZones_ArtIX4()
    {
        Assert.Equal(
            ["Centro", "Zona Leste", "Zona Norte", "Zona Oeste", "Zona Sul"],
            Geography.Regions.Select(region => region.Name).Order());
    }

    [Fact]
    public void NeighborhoodCenters_InsideMunicipalityAndInTheirZone_ArtIX4()
    {
        Assert.InRange(Geography.Neighborhoods.Count, 25, 35);
        Assert.All(Geography.Neighborhoods, neighborhood =>
        {
            var region = Geography.Regions.Single(candidate => candidate.Name == neighborhood.Region);
            Assert.True(GeoMath.Contains(Geography.Municipality.Boundary, neighborhood.Center), neighborhood.Name);
            Assert.True(GeoMath.Contains(region.Boundary, neighborhood.Center), neighborhood.Name);
        });
    }

    [Fact]
    public void Neighborhoods_FourInCentroAndFiveToSevenPerZone_Plan15111()
    {
        var counts = Geography.Neighborhoods.GroupBy(neighborhood => neighborhood.Region).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(4, counts["Centro"]);
        Assert.All(counts.Where(entry => entry.Key != "Centro"), entry => Assert.InRange(entry.Value, 5, 7));
    }

    [Fact]
    public void NeighborhoodNames_AreUnique_ArtIX4()
    {
        Assert.Equal(Geography.Neighborhoods.Count, Geography.Neighborhoods.Select(n => n.Name).Distinct().Count());
    }

    [Fact]
    public void Zones_DoNotOverlapAndCoverTheMunicipality_ArtIX4()
    {
        var boundary = Geography.Municipality.Boundary;
        var (south, north) = (boundary.Min(point => point.Latitude), boundary.Max(point => point.Latitude));
        var (west, east) = (boundary.Min(point => point.Longitude), boundary.Max(point => point.Longitude));
        const int Steps = 120;
        var inside = 0;

        for (var i = 1; i < Steps; i++)
        {
            for (var j = 1; j < Steps; j++)
            {
                var point = new GeoCoordinate(south + ((north - south) * i / Steps), west + ((east - west) * j / Steps));
                var zones = Geography.Regions.Count(region => GeoMath.Contains(region.Boundary, point));

                if (GeoMath.Contains(boundary, point))
                {
                    inside++;
                    Assert.Equal(1, zones);
                }
                else
                {
                    Assert.Equal(0, zones);
                }
            }
        }

        Assert.True(inside > 1000);
    }

    [Fact]
    public void ZipCodes_UniqueWithEightDigitsAndPrometeiaPrefix_ArtIX4()
    {
        var zipCodes = Geography.Streets.Select(street => street.ZipCode).ToArray();

        Assert.Equal(zipCodes.Length, zipCodes.Distinct().Count());
        Assert.Matches(FiveDigits(), Geography.ZipCodePrefix);
        Assert.All(zipCodes, zipCode =>
        {
            Assert.Matches(EightDigits(), zipCode);
            Assert.StartsWith(Geography.ZipCodePrefix, zipCode, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Streets_BelongToExistingNeighborhoodsAndHaveGeometry_ArtIX4()
    {
        var names = Geography.Neighborhoods.Select(neighborhood => neighborhood.Name).ToHashSet();

        Assert.NotEmpty(Geography.Streets);
        Assert.All(Geography.Streets, street =>
        {
            Assert.Contains(street.Neighborhood, names);
            Assert.NotEmpty(street.Geometry);
            Assert.All(street.Geometry, trecho => Assert.True(trecho.Count >= 2));
            Assert.Matches(StreetName(), street.Name);
        });
    }

    [Fact]
    public void EveryNeighborhood_HasAtLeastOneStreet_ArtIX4()
    {
        var withStreets = Geography.Streets.Select(street => street.Neighborhood).ToHashSet();

        Assert.All(Geography.Neighborhoods, neighborhood => Assert.Contains(neighborhood.Name, withStreets));
    }

    [Fact]
    public void Load_ReturnsTheSameInstance()
    {
        Assert.Same(Geography, PrometeiaGeography.Load());
    }

    [GeneratedRegex("^[0-9]{5}$")]
    private static partial Regex FiveDigits();

    [GeneratedRegex("^[0-9]{8}$")]
    private static partial Regex EightDigits();

    [GeneratedRegex("^(Avenida|Rua|Travessa) .+$")]
    private static partial Regex StreetName();
}
