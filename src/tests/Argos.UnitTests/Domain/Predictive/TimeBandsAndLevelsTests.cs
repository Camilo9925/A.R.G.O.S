using Argos.Domain.Operations.Occurrences;
using Argos.Domain.Predictive;

namespace Argos.UnitTests.Domain.Predictive;

/// <summary>
/// Faixas horárias e níveis de risco (D-88, D-91, D-97; RN-30; plan 17.5, 17.13): a faixa 22h–02h pertence ao dia em que
/// começa, o horizonte de 24 h atravessa a meia-noite e as fronteiras 24/25, 49/50 e 74/75 valem também com outras faixas.
/// </summary>
public sealed class TimeBandsAndLevelsTests
{
    // 2026-10-26 é uma segunda-feira.
    private static readonly DateTime Monday = new(2026, 10, 26);

    [Theory]
    [InlineData(22, 0, DayOfWeek.Monday, TimeBand.From22To02)]
    [InlineData(23, 59, DayOfWeek.Monday, TimeBand.From22To02)]
    [InlineData(0, 30, DayOfWeek.Sunday, TimeBand.From22To02)]
    [InlineData(1, 59, DayOfWeek.Sunday, TimeBand.From22To02)]
    [InlineData(2, 0, DayOfWeek.Monday, TimeBand.From02To06)]
    [InlineData(5, 59, DayOfWeek.Monday, TimeBand.From02To06)]
    [InlineData(6, 0, DayOfWeek.Monday, TimeBand.From06To10)]
    [InlineData(10, 0, DayOfWeek.Monday, TimeBand.From10To14)]
    [InlineData(14, 0, DayOfWeek.Monday, TimeBand.From14To18)]
    [InlineData(18, 0, DayOfWeek.Monday, TimeBand.From18To22)]
    [InlineData(21, 59, DayOfWeek.Monday, TimeBand.From18To22)]
    public void CellOf_NightBandBelongsToTheDayItStarts_D88(int hour, int minute, DayOfWeek day, TimeBand band)
    {
        var cell = TimeBands.CellOf(Monday.AddHours(hour).AddMinutes(minute));

        Assert.Equal((day, band), cell);
    }

    [Fact]
    public void Label_ShowsTheSixBandsFromTwoAm_D88()
    {
        Assert.Equal(
            ["02h–06h", "06h–10h", "10h–14h", "14h–18h", "18h–22h", "22h–02h"],
            TimeBands.All.Select(TimeBands.Label));
    }

    [Fact]
    public void CellsBetween_Next24HoursCrossingMidnight_ListsTheSevenBandsInOrder_D91()
    {
        var friday = Monday.AddDays(4);

        var cells = TimeBands.CellsBetween(friday.AddHours(20), friday.AddHours(44));

        Assert.Equal(
            [
                (DayOfWeek.Friday, TimeBand.From18To22),
                (DayOfWeek.Friday, TimeBand.From22To02),
                (DayOfWeek.Saturday, TimeBand.From02To06),
                (DayOfWeek.Saturday, TimeBand.From06To10),
                (DayOfWeek.Saturday, TimeBand.From10To14),
                (DayOfWeek.Saturday, TimeBand.From14To18),
                (DayOfWeek.Saturday, TimeBand.From18To22),
            ],
            cells.Select(cell => (cell.Day, cell.Band)));
        Assert.Equal(friday.AddHours(18), cells[0].StartLocal);
        Assert.Equal(friday.AddHours(22), cells[1].StartLocal);
    }

    [Fact]
    public void CellsBetween_Next7Days_CoversAllFortyTwoCells_D91()
    {
        var cells = TimeBands.CellsBetween(Monday.AddHours(12), Monday.AddHours(12).AddDays(7));

        Assert.Equal(42, cells.Select(cell => (cell.Day, cell.Band)).Distinct().Count());
    }

    [Theory]
    [InlineData(0, RiskLevel.Low)]
    [InlineData(24, RiskLevel.Low)]
    [InlineData(25, RiskLevel.Moderate)]
    [InlineData(49, RiskLevel.Moderate)]
    [InlineData(50, RiskLevel.High)]
    [InlineData(74, RiskLevel.High)]
    [InlineData(75, RiskLevel.VeryHigh)]
    [InlineData(100, RiskLevel.VeryHigh)]
    public void LevelOf_DefaultRanges_HonorsTheBoundaries_RN30(int index, RiskLevel expected)
    {
        Assert.Equal(expected, RiskLevels.Default.LevelOf(index));
    }

    [Fact]
    public void LevelOf_OtherRangesFromTheParameters_MoveTheBoundaries_RN30()
    {
        var levels = new RiskLevels(10, 20, 30);

        Assert.Equal(
            [RiskLevel.Low, RiskLevel.Moderate, RiskLevel.Moderate, RiskLevel.High, RiskLevel.High, RiskLevel.VeryHigh],
            new[] { 9, 10, 19, 20, 29, 30 }.Select(levels.LevelOf));
        Assert.Equal(
            [(RiskLevel.Low, 0, 9), (RiskLevel.Moderate, 10, 19), (RiskLevel.High, 20, 29), (RiskLevel.VeryHigh, 30, 100)],
            levels.Ranges().Select(range => (range.Level, range.From, range.To)));
    }

    [Theory]
    [InlineData(0, 50, 75)]
    [InlineData(50, 50, 75)]
    [InlineData(60, 50, 75)]
    [InlineData(25, 50, 101)]
    public void RiskLevels_RangesOutOfOrder_AreRefused_RN30(int moderate, int high, int veryHigh)
    {
        var error = Assert.Throws<InvalidOperationException>(() => new RiskLevels(moderate, high, veryHigh));

        Assert.Equal(RiskLevels.OutOfOrderMessage, error.Message);
    }

    [Fact]
    public void Labels_AndRecommendations_AreTheTextsOfThePlan_RN28()
    {
        Assert.Equal(["Baixo", "Moderado", "Alto", "Muito alto"], Enum.GetValues<RiskLevel>().Select(RiskLevels.Label));
        Assert.Equal(["Baixa", "Média", "Alta"], Enum.GetValues<ConfidenceLevel>().Select(RiskLevels.Label));
        Assert.Equal("Reforçar o patrulhamento preventivo na região nesse período.", RiskLevels.Recommendation(RiskLevel.High));
    }

    [Fact]
    public void OccurrenceKindCatalog_HasTheSeverityWeights_A32()
    {
        Assert.Equal(
            [3, 3, 2, 1, 1],
            Enum.GetValues<OccurrenceKind>().Select(OccurrenceKindCatalog.Weight));
    }
}
