using Argos.Domain.Operations.Occurrences;
using Argos.Domain.Predictive;

namespace Argos.UnitTests.Domain.Predictive;

/// <summary>
/// Índice de risco (US-20; RN-28 a RN-30; D-89; plan 17.5, 17.13): célula máxima 100, região com menos semanas que o
/// mínimo fora do índice e da normalização (RN-29), tendência limitada, confiança nos limites e fatores ordenados.
/// </summary>
public sealed class RiskModelTests
{
    private static readonly DateOnly DataEnd = new(2026, 10, 26);

    private static readonly Guid Centro = Guid.NewGuid();

    private static readonly Guid Oeste = Guid.NewGuid();

    private static RiskRegionInput Region(Guid id, string name, int weeks, int total = 100) =>
        new(id, name, DataEnd.AddDays(-7 * weeks), total);

    /// <summary>Ocorrências de uma célula, <paramref name="perWeek"/> por semana, nas semanas informadas.</summary>
    private static IEnumerable<RiskSample> Cell(
        Guid region, DayOfWeek day, TimeBand band, OccurrenceKind kind, int perWeek, IEnumerable<int> weeks) =>
        weeks.SelectMany(week => Enumerable.Range(0, perWeek).Select(_ =>
            new RiskSample(region, kind, DataEnd.AddDays((-7 * week) - 1), day, band)));

    private static RiskResult Calculate(
        IEnumerable<RiskSample> samples, IReadOnlyList<RiskRegionInput> regions, RiskLevels? levels = null) =>
        RiskModel.Calculate(samples.ToArray(), DataEnd, regions, levels ?? RiskLevels.Default, 4);

    [Fact]
    public void Calculate_MostIntenseCellOfTheCity_IsOneHundredAndTheOthersAreRelative_RN30()
    {
        var samples = Cell(Centro, DayOfWeek.Friday, TimeBand.From18To22, OccurrenceKind.TrafficAccident, 3, Enumerable.Range(0, 12))
            .Concat(Cell(Centro, DayOfWeek.Monday, TimeBand.From06To10, OccurrenceKind.Congestion, 1, Enumerable.Range(0, 12)));

        var region = Assert.Single(Calculate(samples, [Region(Centro, "Centro", 12)]).Regions);

        Assert.Equal(42, region.Cells.Count);
        Assert.Equal(100, region.Cell(DayOfWeek.Friday, TimeBand.From18To22).Index);
        Assert.Equal(11, region.Cell(DayOfWeek.Monday, TimeBand.From06To10).Index);
        Assert.Equal(RiskLevel.VeryHigh, region.Cell(DayOfWeek.Friday, TimeBand.From18To22).Level);
        Assert.Equal(RiskLevel.Low, region.Cell(DayOfWeek.Monday, TimeBand.From06To10).Level);
        Assert.Equal(0, region.Cell(DayOfWeek.Sunday, TimeBand.From02To06).Index);
    }

    [Fact]
    public void Calculate_RegionWithThreeWeeks_HasInsufficientDataAndStaysOutOfTheNormalization_RN29()
    {
        var samples = Cell(Centro, DayOfWeek.Friday, TimeBand.From18To22, OccurrenceKind.TrafficAccident, 3, Enumerable.Range(0, 12))
            .Concat(Cell(Oeste, DayOfWeek.Friday, TimeBand.From18To22, OccurrenceKind.TheftOrRobbery, 30, Enumerable.Range(0, 3)));

        var result = Calculate(samples, [Region(Centro, "Centro", 12), Region(Oeste, "Zona Oeste", 3)]);

        var west = result.Regions.Single(region => region.RegionId == Oeste);
        Assert.True(west.InsufficientData);
        Assert.Equal(3, west.HistoryWeeks);
        Assert.Empty(west.Cells);
        Assert.Equal(100, result.Regions.Single(region => region.RegionId == Centro).Cell(DayOfWeek.Friday, TimeBand.From18To22).Index);
    }

    [Theory]
    [InlineData(20, 2)]
    [InlineData(21, 3)]
    [InlineData(22, 3)]
    [InlineData(27, 3)]
    [InlineData(28, 4)]
    public void HistoryWeeks_CountsWholeDaysFromTheMidnightOfTheFirstOccurrenceDay_RN29(int daysBeforeEnd, int weeks)
    {
        Assert.Equal(weeks, RiskModel.HistoryWeeks(DataEnd.AddDays(-daysBeforeEnd), DataEnd));
    }

    [Fact]
    public void HistoryWeeks_WithoutOccurrences_IsZero_RN29()
    {
        Assert.Equal(0, RiskModel.HistoryWeeks(null, DataEnd));
    }

    [Fact]
    public void Calculate_RegionWithoutOccurrences_HasInsufficientData_RN29()
    {
        var result = RiskModel.Calculate([], DataEnd, [new RiskRegionInput(Centro, "Centro", null, 0)], RiskLevels.Default, 4);

        var region = Assert.Single(result.Regions);
        Assert.True(region.InsufficientData);
        Assert.Equal(0, region.HistoryWeeks);
    }

    [Fact]
    public void Calculate_RecentWeeksMuchAboveTheWindow_LimitTheTrendToOnePointTwo_RN28()
    {
        var samples = Cell(Centro, DayOfWeek.Friday, TimeBand.From18To22, OccurrenceKind.TheftOrRobbery, 30, [0, 1])
            .Concat(Cell(Centro, DayOfWeek.Friday, TimeBand.From18To22, OccurrenceKind.TheftOrRobbery, 1, Enumerable.Range(2, 6)));

        var region = Assert.Single(Calculate(samples, [Region(Centro, "Centro", 12)]).Regions);

        Assert.Equal(1.2, region.Trend);
    }

    [Fact]
    public void Calculate_RecentWeeksMuchBelowTheWindow_LimitTheTrendToZeroPointEight_RN28()
    {
        var samples = Cell(Centro, DayOfWeek.Friday, TimeBand.From18To22, OccurrenceKind.TheftOrRobbery, 30, Enumerable.Range(2, 6));

        var region = Assert.Single(Calculate(samples, [Region(Centro, "Centro", 12)]).Regions);

        Assert.Equal(0.8, region.Trend);
    }

    [Fact]
    public void Calculate_RegionWithoutAnyOccurrenceInTheWindow_HasNeutralTrendAndIndexZero_RN28()
    {
        var region = Assert.Single(Calculate([], [Region(Centro, "Centro", 12)]).Regions);

        Assert.Equal(1, region.Trend);
        Assert.All(region.Cells, cell => Assert.Equal(0, cell.Index));
    }

    [Theory]
    [InlineData(12, 12, ConfidenceLevel.High)]
    [InlineData(12, 11, ConfidenceLevel.Medium)]
    [InlineData(8, 6, ConfidenceLevel.Medium)]
    [InlineData(8, 5, ConfidenceLevel.Low)]
    [InlineData(7, 12, ConfidenceLevel.Medium)]
    [InlineData(6, 6, ConfidenceLevel.Medium)]
    [InlineData(6, 5, ConfidenceLevel.Low)]
    [InlineData(5, 40, ConfidenceLevel.Low)]
    [InlineData(4, 40, ConfidenceLevel.Low)]
    public void Calculate_ConfidenceFollowsTheWeeksAndTheOccurrencesOfTheCell_RN28(int weeks, int occurrences, ConfidenceLevel expected)
    {
        var samples = Enumerable.Range(0, occurrences).Select(position =>
            new RiskSample(Centro, OccurrenceKind.Congestion, DataEnd.AddDays((-7 * (position % Math.Min(weeks, 8))) - 1), DayOfWeek.Friday, TimeBand.From18To22));

        var region = Assert.Single(Calculate(samples, [Region(Centro, "Centro", weeks)]).Regions);

        Assert.Equal(expected, region.Cell(DayOfWeek.Friday, TimeBand.From18To22).Confidence);
        Assert.Equal(occurrences, region.Cell(DayOfWeek.Friday, TimeBand.From18To22).OccurrenceCount);
    }

    [Fact]
    public void Calculate_Factors_AreOrderedByTheDistanceFromOneAndEndWithTheHeaviestKinds_RN28()
    {
        var samples = Cell(Centro, DayOfWeek.Friday, TimeBand.From18To22, OccurrenceKind.TrafficAccident, 2, Enumerable.Range(0, 8))
            .Concat(Cell(Centro, DayOfWeek.Friday, TimeBand.From18To22, OccurrenceKind.Congestion, 2, Enumerable.Range(0, 8)))
            .Concat(Cell(Centro, DayOfWeek.Friday, TimeBand.From18To22, OccurrenceKind.Vandalism, 1, Enumerable.Range(0, 8)));

        var region = Assert.Single(Calculate(samples, [Region(Centro, "Centro", 8)]).Regions);
        var factors = region.Cell(DayOfWeek.Friday, TimeBand.From18To22).Factors;

        // Toda a atividade está numa célula: 7 dias × 6 faixas, então o dia vale 7× e a faixa 6× a média.
        Assert.Equal([RiskFactorKind.DayOfWeek, RiskFactorKind.TimeBand], factors.Take(2).Select(factor => factor.Kind));
        Assert.Equal(7, factors.First(factor => factor.Kind == RiskFactorKind.DayOfWeek).Multiplier, 6);
        Assert.Equal(6, factors.First(factor => factor.Kind == RiskFactorKind.TimeBand).Multiplier, 6);
        Assert.Equal(RiskFactorKind.OccurrenceKinds, factors[^1].Kind);

        // Pesos por semana: acidente 2×3 = 6, congestionamento 2×1 = 2, vandalismo 1×2 = 2; total 10.
        var shares = factors[^1].Shares;
        Assert.Equal(2, shares.Count);
        Assert.Equal(OccurrenceKind.TrafficAccident, shares[0].Kind);
        Assert.Equal(0.6, shares[0].Share, 6);
        Assert.Equal(0.2, shares[1].Share, 6);
    }

    [Fact]
    public void Calculate_TheRegionFactor_ComparesWithTheAverageOfTheSufficientRegions_RN28()
    {
        var other = Guid.NewGuid();
        var samples = Cell(Centro, DayOfWeek.Friday, TimeBand.From18To22, OccurrenceKind.Congestion, 3, Enumerable.Range(0, 12))
            .Concat(Cell(other, DayOfWeek.Friday, TimeBand.From18To22, OccurrenceKind.Congestion, 1, Enumerable.Range(0, 12)));

        var result = Calculate(samples, [Region(Centro, "Centro", 12), Region(other, "Zona Sul", 12)]);

        double RegionFactor(Guid id) => result.Regions.Single(region => region.RegionId == id)
            .Cell(DayOfWeek.Friday, TimeBand.From18To22).Factors.Single(factor => factor.Kind == RiskFactorKind.Region).Multiplier;

        Assert.Equal(1.5, RegionFactor(Centro), 6);
        Assert.Equal(0.5, RegionFactor(other), 6);
    }

    [Fact]
    public void Calculate_WithOtherRanges_ChangesTheLevelsButNotTheIndex_RN30()
    {
        var samples = Cell(Centro, DayOfWeek.Friday, TimeBand.From18To22, OccurrenceKind.TrafficAccident, 3, Enumerable.Range(0, 12))
            .Concat(Cell(Centro, DayOfWeek.Monday, TimeBand.From06To10, OccurrenceKind.Congestion, 1, Enumerable.Range(0, 12)));

        var region = Assert.Single(Calculate(samples, [Region(Centro, "Centro", 12)], new RiskLevels(5, 10, 90)).Regions);

        Assert.Equal(11, region.Cell(DayOfWeek.Monday, TimeBand.From06To10).Index);
        Assert.Equal(RiskLevel.High, region.Cell(DayOfWeek.Monday, TimeBand.From06To10).Level);
    }
}
