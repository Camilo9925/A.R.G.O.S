namespace Argos.Domain.Predictive;

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

/// <summary>Faixa de índices de um nível, para a legenda.</summary>
public sealed record RiskLevelRange(RiskLevel Level, int From, int To);

/// <summary>
/// Níveis do índice (RN-30; D-97; plan 17.5) a partir dos parâmetros de negócio: Baixo abaixo de <c>moderateFrom</c>,
/// Moderado a partir dele, Alto a partir de <c>highFrom</c> e Muito alto a partir de <c>veryHighFrom</c>. Padrão 25, 50
/// e 75.
/// </summary>
public sealed class RiskLevels
{
    public const string OutOfOrderMessage = "As faixas dos níveis de risco devem ser crescentes, entre 1 e 100.";

    private readonly int _moderateFrom;
    private readonly int _highFrom;
    private readonly int _veryHighFrom;

    /// <exception cref="InvalidOperationException">Faixas fora de ordem (só por alteração manual do banco; Art. III.7).</exception>
    public RiskLevels(int moderateFrom, int highFrom, int veryHighFrom)
    {
        if (moderateFrom < 1 || moderateFrom >= highFrom || highFrom >= veryHighFrom || veryHighFrom > 100)
        {
            throw new InvalidOperationException(OutOfOrderMessage);
        }

        (_moderateFrom, _highFrom, _veryHighFrom) = (moderateFrom, highFrom, veryHighFrom);
    }

    public static RiskLevels Default { get; } = new(25, 50, 75);

    public RiskLevel LevelOf(int index)
    {
        if (index < 0 || index > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "O índice de risco vai de 0 a 100.");
        }

        if (index >= _veryHighFrom)
        {
            return RiskLevel.VeryHigh;
        }

        if (index >= _highFrom)
        {
            return RiskLevel.High;
        }

        return index >= _moderateFrom ? RiskLevel.Moderate : RiskLevel.Low;
    }

    /// <summary>As quatro faixas, de 0 a 100, para a legenda (D-59).</summary>
    public IReadOnlyList<RiskLevelRange> Ranges() =>
    [
        new(RiskLevel.Low, 0, _moderateFrom - 1),
        new(RiskLevel.Moderate, _moderateFrom, _highFrom - 1),
        new(RiskLevel.High, _highFrom, _veryHighFrom - 1),
        new(RiskLevel.VeryHigh, _veryHighFrom, 100),
    ];

    public static string Label(RiskLevel level) => level switch
    {
        RiskLevel.Low => "Baixo",
        RiskLevel.Moderate => "Moderado",
        RiskLevel.High => "Alto",
        RiskLevel.VeryHigh => "Muito alto",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Nível de risco desconhecido."),
    };

    public static string Label(ConfidenceLevel confidence) => confidence switch
    {
        ConfidenceLevel.Low => "Baixa",
        ConfidenceLevel.Medium => "Média",
        ConfidenceLevel.High => "Alta",
        _ => throw new ArgumentOutOfRangeException(nameof(confidence), confidence, "Confiança desconhecida."),
    };

    /// <summary>Recomendação pelo nível (RN-28; plan 17.5), sempre como sugestão (RN-33).</summary>
    public static string Recommendation(RiskLevel level) => level switch
    {
        RiskLevel.Low => "Manter o patrulhamento de rotina.",
        RiskLevel.Moderate => "Manter atenção e o patrulhamento de rotina na região nesse período.",
        RiskLevel.High => "Reforçar o patrulhamento preventivo na região nesse período.",
        RiskLevel.VeryHigh => "Priorizar a região nesse período e avaliar reforço de equipes.",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Nível de risco desconhecido."),
    };
}
