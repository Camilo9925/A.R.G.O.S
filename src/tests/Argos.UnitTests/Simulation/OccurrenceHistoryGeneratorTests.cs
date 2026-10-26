using Argos.Simulation.Generators;
using Argos.Simulation.Geography;
using Argos.Simulation.Models;

namespace Argos.UnitTests.Simulation;

/// <summary>
/// Histórico simulado de ocorrências (RF-025, parcial; Art. IX.1 a IX.4; D-86, D-87, D-93; A-32, A-34; plan 17.3, 17.13):
/// determinístico pela semente e pelo dia da carga, com os pontos dentro da região, a Zona Oeste só nas últimas 3
/// semanas e os totais semanais perto da taxa de cada região.
/// </summary>
public sealed class OccurrenceHistoryGeneratorTests
{
    private static readonly DateOnly EndDate = new(2026, 10, 26);

    private static readonly PrometeiaGeography Geography = PrometeiaGeography.Load();

    private static readonly Lazy<IReadOnlyList<SimulatedOccurrence>> Seed42 =
        new(() => OccurrenceHistoryGenerator.Generate(42, Geography, EndDate));

    private static DateTimeOffset Midnight(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(Geography.Municipality.TimeZoneId);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    [Fact]
    public void Generate_SameSeedAndDay_GivesTheSameOccurrences_ArtIX3()
    {
        var again = OccurrenceHistoryGenerator.Generate(42, Geography, EndDate);

        Assert.Equal(Seed42.Value, again);
    }

    [Fact]
    public void Generate_OtherSeed_GivesOtherOccurrences_ArtIX3()
    {
        var other = OccurrenceHistoryGenerator.Generate(7, Geography, EndDate);

        Assert.NotEqual(Seed42.Value.Select(item => item.OccurredAt), other.Select(item => item.OccurredAt));
    }

    [Fact]
    public void Generate_TheTwelveWeeksEndAtTheLocalMidnightOfTheLoadDay_D87()
    {
        var first = Midnight(EndDate.AddDays(-84));
        var end = Midnight(EndDate);

        Assert.All(Seed42.Value, item => Assert.InRange(item.OccurredAt, first, end.AddTicks(-1)));
        Assert.True(Seed42.Value.Min(item => item.OccurredAt) < first.AddDays(1));
        Assert.True(Seed42.Value.Max(item => item.OccurredAt) >= end.AddHours(-3));
    }

    [Fact]
    public void Generate_EveryPointFallsInsideItsRegion_ArtIX4()
    {
        var regions = Geography.Regions.ToDictionary(region => region.Name);

        Assert.All(Seed42.Value, item =>
        {
            var point = new GeoCoordinate(item.Latitude, item.Longitude);
            Assert.True(GeoMath.Contains(regions[item.RegionName].Boundary, point), $"{item.Street} ({item.RegionName})");
            Assert.Equal(item.RegionName, Geography.Neighborhood(item.NeighborhoodName).Region);
        });
    }

    [Fact]
    public void Generate_WestZone_HasOccurrencesOnlyInTheLastThreeWeeks_A34()
    {
        var threeWeeksAgo = Midnight(EndDate.AddDays(-21));
        var west = Seed42.Value.Where(item => item.RegionName == "Zona Oeste").ToArray();

        Assert.NotEmpty(west);
        Assert.All(west, item => Assert.True(item.OccurredAt >= threeWeeksAgo));
        Assert.All(
            Seed42.Value.Where(item => item.RegionName != "Zona Oeste").Select(item => item.RegionName).Distinct(),
            region => Assert.Contains(Seed42.Value, item => item.RegionName == region && item.OccurredAt < threeWeeksAgo));
    }

    [Theory]
    [InlineData("Centro", 110)]
    [InlineData("Zona Norte", 60)]
    [InlineData("Zona Sul", 55)]
    public void Generate_WeeklyTotalByRegion_IsWithinTwentyPercentOfTheRate_A32(string region, double rate)
    {
        var perWeek = Seed42.Value.Count(item => item.RegionName == region) / 12.0;

        Assert.InRange(perWeek, rate * 0.8, rate * 1.2);
    }

    [Fact]
    public void Generate_EastZone_HasThirtyPercentMoreInTheLastThreeWeeks_A32()
    {
        var threeWeeksAgo = Midnight(EndDate.AddDays(-21));
        var east = Seed42.Value.Where(item => item.RegionName == "Zona Leste").ToArray();

        var recent = east.Count(item => item.OccurredAt >= threeWeeksAgo) / 3.0;
        var before = east.Count(item => item.OccurredAt < threeWeeksAgo) / 9.0;

        Assert.InRange(before, 70 * 0.8, 70 * 1.2);
        Assert.InRange(recent, 91 * 0.8, 91 * 1.2);
        Assert.True(recent > before);
    }

    [Fact]
    public void Generate_WestZoneTotalAndVolume_AreCloseToThePlan_A34()
    {
        var west = Seed42.Value.Count(item => item.RegionName == "Zona Oeste") / 3.0;

        Assert.InRange(west, 50 * 0.8, 50 * 1.2);
        Assert.InRange(Seed42.Value.Count, 3_400, 4_100);
    }

    [Fact]
    public void Generate_AverageTotalAcrossSeeds_IsTheVolumeOfThePlan_A32()
    {
        // 12 × (110 + 70 + 60 + 55) + 3 × 21 + 3 × 50 ≈ 3.753, menos o corte da faixa 22h–02h do último dia.
        var average = Enumerable.Range(1, 20)
            .Average(seed => OccurrenceHistoryGenerator.Generate(seed, Geography, EndDate).Count);

        Assert.InRange(average, 3_650, 3_850);
    }

    [Fact]
    public void Generate_FridayNightsHaveMoreTheftThanTuesdayMornings_A32()
    {
        static int Count(IEnumerable<SimulatedOccurrence> items, DayOfWeek day, int hourFrom, int hourTo) => items.Count(item =>
            item.Kind == SimulatedOccurrenceKind.TheftOrRobbery
            && item.OccurredAt.DayOfWeek == day
            && item.OccurredAt.Hour >= hourFrom
            && item.OccurredAt.Hour < hourTo);

        // O fuso do município é −03:00, então o deslocamento fica o mesmo em qualquer instante do histórico.
        var local = Seed42.Value.Select(item => item with { OccurredAt = item.OccurredAt.ToOffset(TimeSpan.FromHours(-3)) }).ToArray();

        Assert.True(Count(local, DayOfWeek.Friday, 18, 22) > Count(local, DayOfWeek.Tuesday, 6, 10) * 2);
    }
}
