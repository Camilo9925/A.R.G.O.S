using MudBlazor;

namespace Argos.Web.Theme;

/// <summary>
/// Aparência de uma situação: cor de referência sempre acompanhada de ícone e texto (Art. X.2, XI.2).
/// </summary>
/// <param name="Label">Texto exibido e usado na legenda.</param>
/// <param name="Color">Cor de referência.</param>
/// <param name="Icon">Apoio não-cromático.</param>
/// <param name="IsOutlined">Marcador vazado, com contorno tracejado.</param>
public sealed record StatusToken(string Label, string Color, string Icon, bool IsOutlined = false);

/// <summary>
/// Cores de situação semânticas e consistentes em todo o sistema (Art. XI.2). Ainda sem uso na Fase 0.
/// </summary>
public static class StatusTokens
{
    public static StatusToken Operational { get; } =
        new("Operacional", "#2EBD59", Icons.Material.Filled.CheckCircle);

    public static StatusToken Malfunction { get; } =
        new("Mau funcionamento", "#E5484D", Icons.Material.Filled.Warning);

    public static StatusToken UnderMaintenance { get; } =
        new("Em manutenção", "#F5C518", Icons.Material.Filled.Build);

    public static StatusToken Deactivated { get; } =
        new("Desativado", "#8A94A6", Icons.Material.Outlined.DoNotDisturbOn);

    public static StatusToken Stolen { get; } =
        new("Furtado", "#9B5DE5", Icons.Material.Filled.Block);

    public static StatusToken NotInstalled { get; } =
        new("Ainda não instalado", ArgosTheme.Ice, Icons.Material.Outlined.RadioButtonUnchecked, IsOutlined: true);
}
