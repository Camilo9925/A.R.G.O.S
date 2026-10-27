using MudBlazor;

namespace Argos.Web.Theme;

/// <summary>
/// Classificação de uma região na Central (RN-24; US-17): Normal, Atenção ou Crítica, da menos para a mais grave.
/// <see cref="NoData"/> vem antes de todas: sem equipamento no recorte não há como dizer que a região está Normal.
/// </summary>
public enum RegionClassification
{
    NoData,
    Normal,
    Attention,
    Critical,
}

/// <summary>
/// Aparência da classificação da região (RN-24; US-17; Art. X.2, XI.2): a cor sempre com ícone e texto, no quadro, na
/// legenda e no mapa. A classificação é calculada na Web a partir do retrato (plan 16.12); aqui só se escolhe como mostrá-la.
/// </summary>
public static class RegionClassificationTokens
{
    public static StatusToken Normal { get; } =
        new("Normal", "#2EBD59", Icons.Material.Filled.CheckCircle);

    public static StatusToken Attention { get; } =
        new("Atenção", ArgosTheme.Amber, Icons.Material.Filled.Warning);

    public static StatusToken Critical { get; } =
        new("Crítica", "#E5484D", Icons.Material.Filled.Error);

    public static StatusToken NoData { get; } =
        new("Sem dados no recorte", "#8A94A6", Icons.Material.Filled.HelpOutline);

    /// <summary>Todas, da mais grave para a menos grave, para a legenda.</summary>
    public static IReadOnlyList<(RegionClassification Classification, StatusToken Token)> All { get; } =
    [
        (RegionClassification.Critical, Critical),
        (RegionClassification.Attention, Attention),
        (RegionClassification.Normal, Normal),
        (RegionClassification.NoData, NoData),
    ];

    public static StatusToken For(RegionClassification classification) => classification switch
    {
        RegionClassification.NoData => NoData,
        RegionClassification.Normal => Normal,
        RegionClassification.Attention => Attention,
        RegionClassification.Critical => Critical,
        _ => throw new ArgumentOutOfRangeException(nameof(classification), classification, "Classificação desconhecida."),
    };
}
