using MudBlazor;

namespace Argos.Web.Theme;

/// <summary>
/// Tema MudBlazor na paleta oficial do ARGOS (Art. XI.1). Escuro é o padrão da Central (Art. X.4).
/// </summary>
public static class ArgosTheme
{
    public const string NightBlue = "#0B1E3F";
    public const string ElectricCyan = "#00C2FF";
    public const string Amber = "#FF7A1A";
    public const string Ice = "#E8EEF5";

    public static MudTheme Theme { get; } = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = ElectricCyan,
            PrimaryContrastText = NightBlue,
            Secondary = Amber,
            SecondaryContrastText = NightBlue,
            Background = NightBlue,
            Surface = "#12294F",
            AppbarBackground = "#081731",
            AppbarText = Ice,
            DrawerBackground = "#081731",
            DrawerText = Ice,
            DrawerIcon = Ice,
            TextPrimary = Ice,
            TextSecondary = "rgba(232,238,245,0.72)",
            ActionDefault = Ice,
            LinesDefault = "rgba(232,238,245,0.16)",
            Divider = "rgba(232,238,245,0.16)",
        },
        PaletteLight = new PaletteLight
        {
            Primary = NightBlue,
            PrimaryContrastText = Ice,
            Secondary = Amber,
            SecondaryContrastText = NightBlue,
            Background = Ice,
            Surface = "#FFFFFF",
            AppbarBackground = NightBlue,
            AppbarText = Ice,
            DrawerBackground = "#FFFFFF",
            DrawerText = NightBlue,
            DrawerIcon = NightBlue,
            TextPrimary = NightBlue,
            TextSecondary = "rgba(11,30,63,0.72)",
        },
        Typography = new Typography
        {
            // Fontes do sistema: nenhuma fonte baixada de CDN em tempo de execução.
            Default = new DefaultTypography
            {
                FontFamily = ["system-ui", "-apple-system", "Segoe UI", "Roboto", "Helvetica Neue", "Arial", "sans-serif"],
            },
        },
    };
}
