namespace Argos.Contracts.Inventory;

/// <summary>
/// O município da demonstração (D-50; plan 15.14): nome, UF, fuso, contorno e centro. A Web desenha o contorno como
/// orientação no seletor de ponto e não repete a regra da RN-02 (Art. III.4).
/// </summary>
public sealed record MunicipalityData
{
    public string Name { get; init; } = string.Empty;

    public string State { get; init; } = string.Empty;

    /// <summary>Identificador IANA do fuso, como <c>America/Sao_Paulo</c>.</summary>
    public string TimeZoneId { get; init; } = string.Empty;

    /// <summary>Anéis fechados do contorno; o primeiro é o contorno externo e os demais, buracos.</summary>
    public IReadOnlyList<IReadOnlyList<GeoPointData>> Boundary { get; init; } = [];

    public GeoPointData Center { get; init; } = new(0, 0);
}
