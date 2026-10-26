namespace Argos.Contracts.Predictive;

// Espelhos dos enums do Domínio da Cidade Preditiva (D-61; plan 17.11): os mesmos nomes de itens, que trafegam como
// texto. Paridade garantida por teste unitário. O DutyShift já existe em Argos.Contracts.Operations (Fase 4).

/// <summary>Nível do índice de risco (RN-30).</summary>
public enum RiskLevel
{
    Low,
    Moderate,
    High,
    VeryHigh,
}

/// <summary>Confiança de uma previsão, pela quantidade de dados (RN-28).</summary>
public enum ConfidenceLevel
{
    Low,
    Medium,
    High,
}

/// <summary>Horizonte do mapa de regiões (D-91). Só do contrato.</summary>
public enum ForecastHorizon
{
    Next24Hours,
    Next7Days,
}

/// <summary>Faixa horária de 4 h no fuso do município (D-88). A 22h–02h pertence ao dia em que começa.</summary>
public enum TimeBand
{
    From02To06,
    From06To10,
    From10To14,
    From14To18,
    From18To22,
    From22To02,
}

/// <summary>
/// O que a explicação descreve (D-96). Só o índice de risco é entregue; o reforço de equipes (US-21) foi cortado em
/// 26/10/2026 (D-100) e o valor não existe. Só do contrato.
/// </summary>
public enum PredictionKind
{
    RiskIndex,
}

/// <summary>Tipo de ocorrência (A-32).</summary>
public enum OccurrenceKind
{
    TrafficAccident,
    TheftOrRobbery,
    Vandalism,
    Congestion,
    SevereTrafficViolation,
}
