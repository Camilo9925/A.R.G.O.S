namespace Argos.Domain.Inventory.Geography;

/// <summary>
/// Um CEP da tabela local, somente leitura (RF-004; D-52; plan 15.2.4): um logradouro dentro de um bairro, com o
/// ponto de referência que centraliza o mapa. Serve só à sugestão; salvar não exige CEP existente (RN-02).
/// </summary>
public sealed class ZipCodeEntry
{
    // Usado pelo EF Core ao materializar.
    private ZipCodeEntry()
    {
    }

    private ZipCodeEntry(ZipCode zipCode, string street, Guid neighborhoodId, GeoPoint referencePoint)
    {
        ZipCode = zipCode;
        Street = street;
        NeighborhoodId = neighborhoodId;
        ReferencePoint = referencePoint;
    }

    public ZipCode ZipCode { get; private set; } = null!;

    public string Street { get; private set; } = string.Empty;

    public Guid NeighborhoodId { get; private set; }

    public GeoPoint ReferencePoint { get; private set; } = null!;

    public static ZipCodeEntry Create(
        ZipCode zipCode, string street, Neighborhood neighborhood, GeoPoint referencePoint, Municipality municipality)
    {
        ArgumentNullException.ThrowIfNull(zipCode);
        ArgumentNullException.ThrowIfNull(neighborhood);

        var trimmed = (street ?? string.Empty).Trim();
        if (trimmed.Length is < Location.StreetMinLength or > Location.StreetMaxLength)
        {
            throw new InventoryRuleException(
                $"A rua do CEP {zipCode.Formatted} deve ter entre {Location.StreetMinLength} e {Location.StreetMaxLength} caracteres.");
        }

        if (!municipality.Contains(referencePoint))
        {
            throw new InventoryRuleException($"O ponto de referência do CEP {zipCode.Formatted} está fora do município.");
        }

        return new ZipCodeEntry(zipCode, trimmed, neighborhood.Id, referencePoint);
    }
}
