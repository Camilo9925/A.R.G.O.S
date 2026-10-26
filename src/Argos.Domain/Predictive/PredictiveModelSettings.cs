namespace Argos.Domain.Predictive;

/// <summary>
/// Constantes da técnica do índice de risco (Art. VI.4; plan 17.2, 17.5), num lugar só: a explicação as repete. Os
/// limites que o Administrador ajusta (histórico mínimo e faixas dos níveis) são parâmetros de negócio (RN-34).
/// </summary>
public static class PredictiveModelSettings
{
    /// <summary>Semanas da janela de treino, no máximo.</summary>
    public const int TrainingWeeks = 8;

    /// <summary>Semanas da tendência recente.</summary>
    public const int TrendWeeks = 2;

    public const double TrendMinimum = 0.8;

    public const double TrendMaximum = 1.2;

    /// <summary>Confiança Alta: janela de pelo menos 8 semanas e pelo menos 12 ocorrências na célula.</summary>
    public const int HighConfidenceWeeks = 8;

    public const int HighConfidenceOccurrences = 12;

    /// <summary>Confiança Média: janela de pelo menos 6 semanas e pelo menos 6 ocorrências na célula.</summary>
    public const int MediumConfidenceWeeks = 6;

    public const int MediumConfidenceOccurrences = 6;

    /// <summary>Quantos tipos de ocorrência a explicação cita como os que mais pesaram.</summary>
    public const int TopKinds = 2;
}
