using System.Globalization;
using Argos.Contracts.Predictive;
using Argos.Domain.Operations.Occurrences;
using Argos.Domain.Predictive;

namespace Argos.Application.Predictive;

/// <summary>
/// Monta a explicação única de toda previsão (US-25; RF-049; RN-28, RN-29; CS-06; D-96; plan 17.5, 17.6): o que é, o
/// horizonte, os dados considerados (sempre "simulados", Art. IX.2), os fatores, a confiança e a recomendação. Os
/// textos são os da spec, em pt-BR, montados aqui para a Web só exibir (D-59). Sem taxa de acerto: RN-32 não foi
/// entregue (corte de contingência (b) adiantado em 26/10/2026).
/// </summary>
public static class PredictionExplanationBuilder
{
    public const string PatternHorizonLabel = "Padrão semanal";

    public const string Next24HoursLabel = "Próximas 24 horas";

    public const string Next7DaysLabel = "Próximos 7 dias";

    private const string InsufficientRecommendation =
        "Aguardar mais histórico antes de usar a previsão para essa região.";

    private static readonly NumberFormatInfo PtBr = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
        NumberGroupSizes = [3],
    };

    public static string HorizonLabel(ForecastHorizon horizon) => horizon switch
    {
        ForecastHorizon.Next24Hours => Next24HoursLabel,
        ForecastHorizon.Next7Days => Next7DaysLabel,
        _ => throw new ArgumentOutOfRangeException(nameof(horizon), horizon, "Horizonte desconhecido."),
    };

    /// <summary>Explicação de uma célula da grade (padrão semanal).</summary>
    public static PredictionExplanation ForCell(RegionRisk region, RiskCell cell, RiskResult result) =>
        new()
        {
            Kind = PredictionKind.RiskIndex,
            Subject = $"Índice de risco de ocorrências {In(region.RegionName)}, "
                + $"{TimeBands.DayName(cell.Day).ToLowerInvariant()}, {TimeBands.Label(cell.Band)}.",
            HorizonLabel = PatternHorizonLabel,
            DataConsidered = Data(region, result.DataEnd),
            Factors = cell.Factors.Select(factor => Factor(factor, region, cell)).ToArray(),
            Confidence = Map<Contracts.Predictive.ConfidenceLevel>(cell.Confidence),
            Recommendation = RiskLevels.Recommendation(cell.Level),
        };

    /// <summary>Explicação da região na grade: o padrão semanal e o maior risco da semana.</summary>
    public static PredictionExplanation ForRegionPattern(RegionRisk region, RiskResult result)
    {
        if (region.InsufficientData)
        {
            return Insufficient(region, PatternHorizonLabel, null, null, result, "padrão semanal");
        }

        var peak = region.Cells.OrderByDescending(cell => cell.Index).First();

        return new PredictionExplanation
        {
            Kind = PredictionKind.RiskIndex,
            Subject = $"Índice de risco de ocorrências {In(region.RegionName)}, padrão semanal.",
            HorizonLabel = PatternHorizonLabel,
            DataConsidered = Data(region, result.DataEnd),
            Factors = peak.Factors
                .Where(factor => factor.Kind is RiskFactorKind.Region or RiskFactorKind.RecentTrend)
                .Select(factor => Factor(factor, region, peak))
                .ToArray(),
            Confidence = Map<Contracts.Predictive.ConfidenceLevel>(peak.Confidence),
            Recommendation = $"Maior risco da semana: {TimeBands.DayName(peak.Day).ToLowerInvariant()}, "
                + $"{TimeBands.Label(peak.Band)} (índice {peak.Index}). {RiskLevels.Recommendation(peak.Level)}",
        };
    }

    /// <summary>Explicação de uma região no mapa: a da célula do pico do horizonte (D-91).</summary>
    public static PredictionExplanation ForForecastRegion(
        RegionRisk region,
        RiskCell peak,
        ForecastHorizon horizon,
        DateTimeOffset start,
        DateTimeOffset end,
        RiskResult result) =>
        new()
        {
            Kind = PredictionKind.RiskIndex,
            Subject = $"Índice de risco de ocorrências {In(region.RegionName)}, {HorizonLabel(horizon).ToLowerInvariant()}: "
                + $"pico em {TimeBands.DayName(peak.Day).ToLowerInvariant()}, {TimeBands.Label(peak.Band)}.",
            HorizonLabel = HorizonLabel(horizon),
            HorizonStart = start,
            HorizonEnd = end,
            DataConsidered = Data(region, result.DataEnd),
            Factors = peak.Factors.Select(factor => Factor(factor, region, peak)).ToArray(),
            Confidence = Map<Contracts.Predictive.ConfidenceLevel>(peak.Confidence),
            Recommendation = RiskLevels.Recommendation(peak.Level),
        };

    /// <summary>RN-29: região com histórico abaixo do mínimo, sem índice, fatores nem confiança.</summary>
    public static PredictionExplanation Insufficient(
        RegionRisk region,
        string horizonLabel,
        DateTimeOffset? start,
        DateTimeOffset? end,
        RiskResult result,
        string horizonInSubject) =>
        new()
        {
            Kind = PredictionKind.RiskIndex,
            Subject = $"Índice de risco de ocorrências {In(region.RegionName)}, {horizonInSubject}.",
            HorizonLabel = horizonLabel,
            HorizonStart = start,
            HorizonEnd = end,
            DataConsidered = Data(region, result.DataEnd),
            Recommendation = InsufficientRecommendation,
            InsufficientData = true,
            InsufficientDataMessage = InsufficientMessage(region.RegionName, region.HistoryWeeks, result.MinimumHistoryWeeks),
        };

    /// <summary>"Dados insuficientes: a Zona Oeste tem 3 semanas de histórico, e a previsão exige pelo menos 4." (RN-29).</summary>
    public static string InsufficientMessage(string regionName, int weeks, int minimumWeeks) =>
        $"Dados insuficientes: {The(regionName)} {regionName} tem {Plural(weeks, "semana", "semanas")} de histórico, "
        + $"e a previsão exige pelo menos {minimumWeeks}.";

    private static PredictionDataConsidered Data(RegionRisk region, DateOnly dataEnd)
    {
        if (region.FirstOccurrenceDate is not { } first)
        {
            return new PredictionDataConsidered
            {
                Description = $"Nenhuma ocorrência simulada {Of(region.RegionName)}.",
                IsSimulated = true,
            };
        }

        var last = dataEnd.AddDays(-1);
        var window = region.InsufficientData ? string.Empty : $"; {region.WindowWeeks} na janela de treino";

        return new PredictionDataConsidered
        {
            Description = $"{Plural(region.TotalOccurrences, "ocorrência simulada", "ocorrências simuladas", grouped: true)} "
                + $"{Of(region.RegionName)}, de {Date(first)} a {Date(last)} "
                + $"({Plural(region.HistoryWeeks, "semana", "semanas")}{window}).",
            PeriodStart = first,
            PeriodEnd = last,
            Weeks = region.HistoryWeeks,
            OccurrenceCount = region.TotalOccurrences,
            IsSimulated = true,
        };
    }

    private static PredictionFactor Factor(RiskFactor factor, RegionRisk region, RiskCell cell)
    {
        switch (factor.Kind)
        {
            case RiskFactorKind.TimeBand:
                return new PredictionFactor
                {
                    Label = "Faixa horária",
                    Description = $"{TimeBands.Label(cell.Band)}: {Multiple(factor.Multiplier)}× a média das faixas da região.",
                    Multiplier = Math.Round(factor.Multiplier, 2),
                };
            case RiskFactorKind.DayOfWeek:
                return new PredictionFactor
                {
                    Label = "Dia da semana",
                    Description = $"{TimeBands.DayName(cell.Day)}: {Multiple(factor.Multiplier)}× a média dos dias da região.",
                    Multiplier = Math.Round(factor.Multiplier, 2),
                };
            case RiskFactorKind.Region:
                return new PredictionFactor
                {
                    Label = "Região",
                    Description = $"{region.RegionName}: {Multiple(factor.Multiplier)}× a média das regiões.",
                    Multiplier = Math.Round(factor.Multiplier, 2),
                };
            case RiskFactorKind.RecentTrend:
                return new PredictionFactor
                {
                    Label = "Tendência recente",
                    Description = $"Últimas {PredictiveModelSettings.TrendWeeks} semanas: {Percent(factor.Multiplier - 1)} em relação à janela.",
                    Multiplier = Math.Round(factor.Multiplier, 2),
                };
            default:
                return new PredictionFactor
                {
                    Label = "Tipos que mais pesaram",
                    Description = string.Join(
                            ", ",
                            factor.Shares.Select(share =>
                                $"{OccurrenceKindCatalog.Label(share.Kind)} ({Math.Round(share.Share * 100):0}%)"))
                        + ".",
                    Share = factor.Shares.Count == 0 ? null : Math.Round(factor.Shares[0].Share, 2),
                };
        }
    }

    private static string Percent(double fraction)
    {
        var percent = (int)Math.Round(fraction * 100, MidpointRounding.AwayFromZero);
        return percent > 0 ? $"+{percent}%" : percent < 0 ? $"-{-percent}%" : "0%";
    }

    private static string Multiple(double value) => value.ToString("0.0", PtBr);

    private static string Date(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string Plural(int count, string singular, string plural, bool grouped = false) =>
        $"{(grouped ? count.ToString("N0", PtBr) : count.ToString(CultureInfo.InvariantCulture))} {(count == 1 ? singular : plural)}";

    // Os nomes das regiões são "Centro" (masculino) e "Zona …" (feminino).
    private static bool IsFeminine(string regionName) => regionName.StartsWith("Zona ", StringComparison.Ordinal);

    private static string The(string regionName) => IsFeminine(regionName) ? "a" : "o";

    private static string In(string regionName) => $"{(IsFeminine(regionName) ? "na" : "no")} {regionName}";

    private static string Of(string regionName) => $"{(IsFeminine(regionName) ? "da" : "do")} {regionName}";

    private static TContract Map<TContract>(Enum value)
        where TContract : struct, Enum =>
        Enum.Parse<TContract>(value.ToString());
}
