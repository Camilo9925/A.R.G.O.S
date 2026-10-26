using Argos.Contracts.Operations;
using MudBlazor;

namespace Argos.Web.Theme;

/// <summary>
/// Aparência da gravidade do alerta (RF-035): cor sempre com ícone e texto (Art. X.2, XI.2). A ordem da lista e a
/// gravidade vêm prontas da API; aqui só se escolhe como mostrá-las.
/// </summary>
public static class AlertSeverityTokens
{
    public static StatusToken Critical { get; } =
        new("Crítico", "#E5484D", Icons.Material.Filled.Error);

    public static StatusToken High { get; } =
        new("Alto", ArgosTheme.Amber, Icons.Material.Filled.PriorityHigh);

    public static StatusToken Medium { get; } =
        new("Médio", "#F5C518", Icons.Material.Filled.Info);

    public static StatusToken Low { get; } =
        new("Baixo", "#8A94A6", Icons.Material.Filled.ArrowDownward);

    /// <summary>Todas, da mais grave para a menos grave, para a legenda e os indicadores.</summary>
    public static IReadOnlyList<(AlertSeverity Severity, StatusToken Token)> All { get; } =
    [
        (AlertSeverity.Critical, Critical),
        (AlertSeverity.High, High),
        (AlertSeverity.Medium, Medium),
        (AlertSeverity.Low, Low),
    ];

    public static StatusToken For(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => Critical,
        AlertSeverity.High => High,
        AlertSeverity.Medium => Medium,
        AlertSeverity.Low => Low,
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Gravidade desconhecida."),
    };
}
