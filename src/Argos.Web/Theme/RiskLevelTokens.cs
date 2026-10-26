using Argos.Contracts.Predictive;
using MudBlazor;

namespace Argos.Web.Theme;

/// <summary>
/// Aparência do nível do índice de risco (RN-30; US-20; Art. X.2, XI.2): a cor sempre com ícone e texto, na grade, no
/// mapa e na legenda. As faixas de cada nível vêm da API; aqui só se escolhe como mostrá-las. Verde, amarelo, laranja e
/// vermelho da paleta de situações; "Dados insuficientes" em cinza, com contorno tracejado no mapa.
/// </summary>
public static class RiskLevelTokens
{
    public static StatusToken Low { get; } =
        new("Baixo", "#2EBD59", Icons.Material.Filled.CheckCircle);

    public static StatusToken Moderate { get; } =
        new("Moderado", "#F5C518", Icons.Material.Filled.Info);

    public static StatusToken High { get; } =
        new("Alto", ArgosTheme.Amber, Icons.Material.Filled.Warning);

    public static StatusToken VeryHigh { get; } =
        new("Muito alto", "#E5484D", Icons.Material.Filled.Error);

    /// <summary>Região com histórico menor que o mínimo (RN-29): sem número e sem nível.</summary>
    public static StatusToken InsufficientData { get; } =
        new("Dados insuficientes", "#8A94A6", Icons.Material.Outlined.HelpOutline, IsOutlined: true);

    /// <summary>Todos, do menor para o maior risco, para a legenda.</summary>
    public static IReadOnlyList<(RiskLevel Level, StatusToken Token)> All { get; } =
    [
        (RiskLevel.Low, Low),
        (RiskLevel.Moderate, Moderate),
        (RiskLevel.High, High),
        (RiskLevel.VeryHigh, VeryHigh),
    ];

    public static StatusToken For(RiskLevel level) => level switch
    {
        RiskLevel.Low => Low,
        RiskLevel.Moderate => Moderate,
        RiskLevel.High => High,
        RiskLevel.VeryHigh => VeryHigh,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Nível de risco desconhecido."),
    };
}

/// <summary>Aparência da confiança de uma previsão (RN-28): Baixa, Média ou Alta, com ícone e texto.</summary>
public static class ConfidenceTokens
{
    public static StatusToken Low { get; } =
        new("Baixa", "#8A94A6", Icons.Material.Filled.ArrowDownward);

    public static StatusToken Medium { get; } =
        new("Média", "#F5C518", Icons.Material.Filled.Remove);

    public static StatusToken High { get; } =
        new("Alta", "#2EBD59", Icons.Material.Filled.ArrowUpward);

    public static StatusToken For(ConfidenceLevel confidence) => confidence switch
    {
        ConfidenceLevel.Low => Low,
        ConfidenceLevel.Medium => Medium,
        ConfidenceLevel.High => High,
        _ => throw new ArgumentOutOfRangeException(nameof(confidence), confidence, "Confiança desconhecida."),
    };
}

/// <summary>Rótulos fixos da Preditiva que a API não manda: o nome do dia da semana da grade e o selo de dados simulados.</summary>
public static class PredictiveLabels
{
    public static StatusToken Simulated { get; } =
        new("Dados simulados", ArgosTheme.Ice, Icons.Material.Outlined.Science);

    public static string DayName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Segunda-feira",
        DayOfWeek.Tuesday => "Terça-feira",
        DayOfWeek.Wednesday => "Quarta-feira",
        DayOfWeek.Thursday => "Quinta-feira",
        DayOfWeek.Friday => "Sexta-feira",
        DayOfWeek.Saturday => "Sábado",
        DayOfWeek.Sunday => "Domingo",
        _ => throw new ArgumentOutOfRangeException(nameof(day), day, "Dia da semana desconhecido."),
    };

    /// <summary>Segunda a domingo, a ordem das linhas da grade.</summary>
    public static IReadOnlyList<DayOfWeek> WeekDays { get; } =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday,
    ];
}
