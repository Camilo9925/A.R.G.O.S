namespace Argos.Contracts.Predictive;

// Contratos do índice de risco e da explicação (US-20, US-25; RF-043, RF-049, RF-051; D-96, D-98; plan 17.6, 17.11).
// Tudo vem pronto da API (D-59): índice, nível, faixas da legenda, confiança, fatores, textos e recomendação. A taxa de
// acerto (RN-32, CS-09) não foi entregue (corte de contingência (b) adiantado em 26/10/2026), então não há campo para ela.

/// <summary>Faixa de índices de um nível, para a legenda da grade e do mapa.</summary>
public sealed record RiskLevelRange
{
    public RiskLevel Level { get; init; }

    public int From { get; init; }

    public int To { get; init; }
}

/// <summary>Faixa horária com o rótulo pronto, como "18h–22h".</summary>
public sealed record TimeBandData
{
    public TimeBand Band { get; init; }

    public string Label { get; init; } = string.Empty;
}

/// <summary>Os dados em que a previsão se apoia (RN-28): sempre ocorrências simuladas na entrega (Art. IX.2).</summary>
public sealed record PredictionDataConsidered
{
    public string Description { get; init; } = string.Empty;

    public DateOnly? PeriodStart { get; init; }

    public DateOnly? PeriodEnd { get; init; }

    public int Weeks { get; init; }

    public int OccurrenceCount { get; init; }

    public bool IsSimulated { get; init; }
}

/// <summary>Um fator da explicação: o texto pronto e, quando houver, o múltiplo ou a participação que o sustenta.</summary>
public sealed record PredictionFactor
{
    public string Label { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public double? Multiplier { get; init; }

    public double? Share { get; init; }
}

/// <summary>
/// A explicação única de toda previsão (US-25; RN-28, RN-29; CS-06; D-96): o que é, o horizonte, os dados considerados,
/// os fatores, a confiança e a recomendação. Com <see cref="InsufficientData"/> não há fatores nem confiança, mas o resto
/// é preenchido e a mensagem da RN-29 vem pronta.
/// </summary>
public sealed record PredictionExplanation
{
    public PredictionKind Kind { get; init; }

    public string Subject { get; init; } = string.Empty;

    public string HorizonLabel { get; init; } = string.Empty;

    public DateTimeOffset? HorizonStart { get; init; }

    public DateTimeOffset? HorizonEnd { get; init; }

    public PredictionDataConsidered DataConsidered { get; init; } = new();

    public IReadOnlyList<PredictionFactor> Factors { get; init; } = [];

    public ConfidenceLevel? Confidence { get; init; }

    public string Recommendation { get; init; } = string.Empty;

    public bool InsufficientData { get; init; }

    public string? InsufficientDataMessage { get; init; }
}

/// <summary>Uma célula da grade: dia da semana × faixa horária, com o índice e a explicação (RF-043).</summary>
public sealed record RiskGridCell
{
    public DayOfWeek DayOfWeek { get; init; }

    public TimeBand Band { get; init; }

    public int Index { get; init; }

    public RiskLevel Level { get; init; }

    public ConfidenceLevel Confidence { get; init; }

    public PredictionExplanation Explanation { get; init; } = new();
}

/// <summary>
/// Grade de uma região (US-20; <c>GET /api/predictive/risk/regions/{regionId}/grid</c>): 42 células, ou nenhuma com
/// dados insuficientes (RN-29).
/// </summary>
public sealed record RiskGrid
{
    public Guid RegionId { get; init; }

    public string RegionName { get; init; } = string.Empty;

    public DateTimeOffset GeneratedAt { get; init; }

    public IReadOnlyList<RiskLevelRange> Levels { get; init; } = [];

    public IReadOnlyList<TimeBandData> Bands { get; init; } = [];

    public bool InsufficientData { get; init; }

    public PredictionExplanation Explanation { get; init; } = new();

    public IReadOnlyList<RiskGridCell> Cells { get; init; } = [];
}

/// <summary>Uma região no mapa de um horizonte: o maior índice entre as células do horizonte (D-91; Art. V.3).</summary>
public sealed record RiskForecastRegion
{
    public Guid RegionId { get; init; }

    public string RegionName { get; init; } = string.Empty;

    public int? Index { get; init; }

    public RiskLevel? Level { get; init; }

    public bool InsufficientData { get; init; }

    public DayOfWeek? PeakDayOfWeek { get; init; }

    public TimeBand? PeakBand { get; init; }

    public DateTimeOffset? PeakStart { get; init; }

    public PredictionExplanation Explanation { get; init; } = new();
}

/// <summary>Previsão das regiões para um horizonte (US-20; <c>GET /api/predictive/risk/forecast</c>).</summary>
public sealed record RiskForecast
{
    public ForecastHorizon Horizon { get; init; }

    public DateTimeOffset Start { get; init; }

    public DateTimeOffset End { get; init; }

    public DateTimeOffset GeneratedAt { get; init; }

    public IReadOnlyList<RiskLevelRange> Levels { get; init; } = [];

    public IReadOnlyList<RiskForecastRegion> Regions { get; init; } = [];
}
