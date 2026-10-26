using Argos.Domain.Operations.Occurrences;

namespace Argos.Domain.Predictive;

/// <summary>Uma ocorrência já convertida para o horário local: a data, o dia da semana e a faixa (plan 17.5).</summary>
public sealed record RiskSample(Guid RegionId, OccurrenceKind Kind, DateOnly LocalDate, DayOfWeek Day, TimeBand Band);

/// <summary>Região com o resumo do histórico dela: a data da primeira ocorrência e o total (plan 17.9).</summary>
public sealed record RiskRegionInput(Guid Id, string Name, DateOnly? FirstOccurrenceDate, int TotalOccurrences);

public enum RiskFactorKind
{
    TimeBand,
    DayOfWeek,
    Region,
    RecentTrend,
    OccurrenceKinds,
}

/// <summary>Participação de um tipo de ocorrência nos pontos de gravidade da célula (0 a 1).</summary>
public sealed record KindShare(OccurrenceKind Kind, double Share);

/// <summary>
/// Um fator da explicação (RN-28): o múltiplo em relação à média (1 = igual à média), ou, para os tipos que mais
/// pesaram, as participações.
/// </summary>
public sealed record RiskFactor(RiskFactorKind Kind, double Multiplier, IReadOnlyList<KindShare> Shares);

public sealed record RiskCell(
    DayOfWeek Day,
    TimeBand Band,
    int Index,
    RiskLevel Level,
    ConfidenceLevel Confidence,
    int OccurrenceCount,
    IReadOnlyList<RiskFactor> Factors);

/// <summary>
/// Resultado de uma região. Com <see cref="InsufficientData"/> não há células nem tendência (RN-29; Art. VI.3).
/// </summary>
public sealed record RegionRisk(
    Guid RegionId,
    string RegionName,
    int HistoryWeeks,
    bool InsufficientData,
    int WindowWeeks,
    DateOnly? FirstOccurrenceDate,
    int TotalOccurrences,
    double Trend,
    IReadOnlyList<RiskCell> Cells)
{
    public RiskCell Cell(DayOfWeek day, TimeBand band) =>
        Cells.Single(cell => cell.Day == day && cell.Band == band);
}

public sealed record RiskResult(DateOnly DataEnd, int MinimumHistoryWeeks, IReadOnlyList<RegionRisk> Regions);

/// <summary>
/// Índice de risco por região, dia da semana e faixa horária (US-20; RF-043; RN-28 a RN-30; D-89 a D-91; plan 17.5):
/// função pura sobre as ocorrências, sem modelo treinado (Art. VI.4). Média semanal ponderada pela gravidade de cada
/// célula, ajustada pela tendência recente da região e normalizada pela célula mais intensa entre as regiões com
/// histórico suficiente (100 = a mais intensa da cidade; A-37).
/// </summary>
public static class RiskModel
{
    private const int Cells = 42;

    /// <summary>
    /// Semanas de histórico (RN-29): dias locais inteiros entre a meia-noite que inicia o dia da primeira ocorrência e o
    /// fim dos dados, ÷ 7, arredondado para baixo (plan 17.4). Sem ocorrências, 0.
    /// </summary>
    public static int HistoryWeeks(DateOnly? firstOccurrenceDate, DateOnly dataEnd) =>
        firstOccurrenceDate is { } first ? Math.Max(0, dataEnd.DayNumber - first.DayNumber) / 7 : 0;

    /// <param name="samples">Ocorrências; as fora da janela de treino de cada região são ignoradas.</param>
    /// <param name="dataEnd">A meia-noite local (data) em que terminam os dados; as semanas contam para trás daqui.</param>
    /// <param name="minimumHistoryWeeks">Parâmetro <c>predictive.minimum-history-weeks</c> (RN-29).</param>
    public static RiskResult Calculate(
        IReadOnlyCollection<RiskSample> samples,
        DateOnly dataEnd,
        IReadOnlyList<RiskRegionInput> regions,
        RiskLevels levels,
        int minimumHistoryWeeks)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentNullException.ThrowIfNull(levels);

        var byRegion = samples.GroupBy(sample => sample.RegionId).ToDictionary(group => group.Key, group => group.ToArray());
        var drafts = regions
            .Select(region => Draft(
                region,
                byRegion.GetValueOrDefault(region.Id, []),
                dataEnd,
                HistoryWeeks(region.FirstOccurrenceDate, dataEnd),
                minimumHistoryWeeks))
            .ToArray();

        var sufficient = drafts.Where(draft => !draft.Insufficient).ToArray();
        var maximum = sufficient.Length == 0 ? 0 : sufficient.Max(draft => draft.MaximumAdjusted);
        var averageOfMeans = sufficient.Length == 0 ? 0 : sufficient.Average(draft => draft.MeanIntensity);

        return new RiskResult(
            dataEnd,
            minimumHistoryWeeks,
            drafts.Select(draft => draft.Insufficient ? draft.ToInsufficient() : draft.ToResult(maximum, averageOfMeans, levels)).ToArray());
    }

    private static RegionDraft Draft(
        RiskRegionInput region,
        RiskSample[] regionSamples,
        DateOnly dataEnd,
        int historyWeeks,
        int minimumHistoryWeeks)
    {
        if (historyWeeks < minimumHistoryWeeks)
        {
            return new RegionDraft(region, historyWeeks, true, 0, 1, new double[7, 6], new int[7, 6], new double[7, 6, 5]);
        }

        var windowWeeks = Math.Min(PredictiveModelSettings.TrainingWeeks, historyWeeks);
        var windowStart = dataEnd.AddDays(-7 * windowWeeks);
        var recentStart = dataEnd.AddDays(-7 * PredictiveModelSettings.TrendWeeks);
        var weights = new double[7, 6];
        var counts = new int[7, 6];
        var kindWeights = new double[7, 6, 5];
        double total = 0;
        double recent = 0;

        foreach (var sample in regionSamples.Where(sample => sample.LocalDate >= windowStart && sample.LocalDate < dataEnd))
        {
            var (day, band) = (DayIndex(sample.Day), (int)sample.Band);
            var weight = OccurrenceKindCatalog.Weight(sample.Kind);
            weights[day, band] += weight;
            counts[day, band]++;
            kindWeights[day, band, (int)sample.Kind] += weight;
            total += weight;

            if (sample.LocalDate >= recentStart)
            {
                recent += weight;
            }
        }

        var windowAverage = total / windowWeeks;
        var trend = windowAverage == 0
            ? 1
            : Math.Clamp(
                recent / PredictiveModelSettings.TrendWeeks / windowAverage,
                PredictiveModelSettings.TrendMinimum,
                PredictiveModelSettings.TrendMaximum);

        return new RegionDraft(region, historyWeeks, false, windowWeeks, trend, weights, counts, kindWeights);
    }

    private static int DayIndex(DayOfWeek day) => (((int)day) + 6) % 7;

    private sealed class RegionDraft(
        RiskRegionInput region,
        int historyWeeks,
        bool insufficient,
        int windowWeeks,
        double trend,
        double[,] weights,
        int[,] counts,
        double[,,] kindWeights)
    {
        public bool Insufficient => insufficient;

        /// <summary>Intensidade por semana de cada célula, antes da tendência.</summary>
        private double Intensity(int day, int band) => weights[day, band] / windowWeeks;

        /// <summary>Média da intensidade das 42 células.</summary>
        public double MeanIntensity => insufficient ? 0 : Enumerable.Range(0, Cells)
            .Sum(position => Intensity(position / 6, position % 6)) / Cells;

        public double MaximumAdjusted => insufficient ? 0 : Enumerable.Range(0, Cells)
            .Max(position => Intensity(position / 6, position % 6) * trend);

        public RegionRisk ToInsufficient() =>
            new(region.Id, region.Name, historyWeeks, true, 0, region.FirstOccurrenceDate, region.TotalOccurrences, 1, []);

        public RegionRisk ToResult(double maximum, double averageOfMeans, RiskLevels levels)
        {
            var mean = MeanIntensity;
            var cells = new List<RiskCell>(Cells);

            foreach (var day in TimeBands.WeekDays)
            {
                foreach (var band in TimeBands.All)
                {
                    var (d, b) = (DayIndex(day), (int)band);
                    var index = maximum == 0
                        ? 0
                        : (int)Math.Round(100 * Intensity(d, b) * trend / maximum, MidpointRounding.AwayFromZero);
                    index = Math.Clamp(index, 0, 100);

                    cells.Add(new RiskCell(
                        day, band, index, levels.LevelOf(index), Confidence(counts[d, b]), counts[d, b], Factors(d, b, mean, averageOfMeans)));
                }
            }

            return new RegionRisk(
                region.Id, region.Name, historyWeeks, false, windowWeeks, region.FirstOccurrenceDate, region.TotalOccurrences, trend, cells);
        }

        private ConfidenceLevel Confidence(int occurrences)
        {
            if (windowWeeks >= PredictiveModelSettings.HighConfidenceWeeks && occurrences >= PredictiveModelSettings.HighConfidenceOccurrences)
            {
                return ConfidenceLevel.High;
            }

            return windowWeeks >= PredictiveModelSettings.MediumConfidenceWeeks && occurrences >= PredictiveModelSettings.MediumConfidenceOccurrences
                ? ConfidenceLevel.Medium
                : ConfidenceLevel.Low;
        }

        /// <summary>
        /// Fatores de RN-28, calculados da própria fórmula e ordenados pelo quanto se afastam de 1 (|ln m|); os tipos que
        /// mais pesaram vêm por último.
        /// </summary>
        private List<RiskFactor> Factors(int day, int band, double mean, double averageOfMeans)
        {
            var bandMean = Enumerable.Range(0, 7).Average(other => Intensity(other, band));
            var dayMean = Enumerable.Range(0, 6).Average(other => Intensity(day, other));

            var factors = new List<RiskFactor>
            {
                new(RiskFactorKind.TimeBand, Ratio(bandMean, mean), []),
                new(RiskFactorKind.DayOfWeek, Ratio(dayMean, mean), []),
                new(RiskFactorKind.Region, Ratio(mean, averageOfMeans), []),
                new(RiskFactorKind.RecentTrend, trend, []),
            };

            var ordered = factors.OrderByDescending(factor => Math.Abs(Math.Log(Math.Max(factor.Multiplier, 1e-9)))).ToList();
            var shares = TopKinds(day, band);

            if (shares.Count > 0)
            {
                ordered.Add(new RiskFactor(RiskFactorKind.OccurrenceKinds, 1, shares));
            }

            return ordered;
        }

        private List<KindShare> TopKinds(int day, int band)
        {
            var cellWeight = weights[day, band];

            return cellWeight == 0
                ? []
                : Enum.GetValues<OccurrenceKind>()
                    .Select(kind => new KindShare(kind, kindWeights[day, band, (int)kind] / cellWeight))
                    .Where(share => share.Share > 0)
                    .OrderByDescending(share => share.Share)
                    .Take(PredictiveModelSettings.TopKinds)
                    .ToList();
        }

        private static double Ratio(double value, double reference) => reference == 0 ? 1 : value / reference;
    }
}
