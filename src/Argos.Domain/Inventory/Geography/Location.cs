using Argos.Domain.Common;

namespace Argos.Domain.Inventory.Geography;

/// <summary>
/// Localização de um equipamento fixo ou de uma base (RF-003, RF-004, RN-02, RN-03; D-50; plan 15.2.4). É montada
/// pela fábrica <see cref="Create"/>, que recebe o município e o bairro já carregados e verifica tudo sem acessar
/// dados. Os erros saem com os nomes de campo do objeto (<c>zipCode</c>, <c>reference</c>…); a Aplicação os põe sob o
/// prefixo do contrato com <c>DomainFields.Under("location", …)</c> (D-46).
/// </summary>
public sealed record Location
{
    public const int StreetMinLength = 3;
    public const int StreetMaxLength = 120;
    public const int ReferenceMinLength = 1;
    public const int ReferenceMaxLength = 120;

    // Usado pelo EF Core ao materializar: o tipo complexo não liga o GeoPoint a um parâmetro do construtor.
    private Location()
    {
    }

    private Location(
        ZipCode zipCode, string street, Guid neighborhoodId, string city, string state, string reference, GeoPoint point)
    {
        ZipCode = zipCode;
        Street = street;
        NeighborhoodId = neighborhoodId;
        City = city;
        State = state;
        Reference = reference;
        Point = point;
    }

    public ZipCode ZipCode { get; } = null!;

    public string Street { get; } = string.Empty;

    public Guid NeighborhoodId { get; }

    /// <summary>Sempre a do município, nunca do usuário (D-50; RF-004).</summary>
    public string City { get; } = string.Empty;

    /// <summary>Sempre a UF do município (D-50; RF-004).</summary>
    public string State { get; } = string.Empty;

    /// <summary>Texto livre obrigatório, como "número 181", "oposto ao número 181", "KM 203" ou "S/N" (RN-03).</summary>
    public string Reference { get; } = string.Empty;

    public GeoPoint Point { get; } = null!;

    /// <summary>
    /// Valida e monta a localização, com todos os erros de uma vez: CEP com 8 dígitos (RN-02), rua, bairro ativo,
    /// referência (RN-03) e coordenada válida e dentro do município (RN-02).
    /// </summary>
    public static Location Create(
        Municipality municipality,
        Neighborhood neighborhood,
        string? zipCode,
        string? street,
        string? reference,
        double latitude,
        double longitude)
    {
        ArgumentNullException.ThrowIfNull(municipality);
        ArgumentNullException.ThrowIfNull(neighborhood);

        var errors = new FieldErrors();

        if (!ZipCode.TryParse(zipCode, out var validZipCode))
        {
            errors.Add("zipCode", ZipCode.FormatMessage);
        }

        var validStreet = errors.RequiredText("street", street, StreetMinLength, StreetMaxLength, "a rua");

        if (!neighborhood.IsActive)
        {
            errors.Add("neighborhoodId", "O bairro escolhido está inativo.");
        }

        var validReference = errors.RequiredText(
            "reference", reference, ReferenceMinLength, ReferenceMaxLength, "a referência do local");

        GeoPoint? point = null;
        try
        {
            point = GeoPoint.Create(latitude, longitude);
        }
        catch (DomainValidationException exception)
        {
            errors.AddAll(exception);
        }

        if (point is not null && !municipality.Contains(point))
        {
            const string Outside = "O ponto está fora do município.";
            errors.Add("latitude", Outside);
            errors.Add("longitude", Outside);
        }

        errors.ThrowIfAny();
        return new Location(
            validZipCode, validStreet, neighborhood.Id, municipality.Name, municipality.State, validReference, point!);
    }

    /// <summary>Campos que mudaram de <paramref name="before"/> para <paramref name="after"/>, para o registro cadastral.</summary>
    public static IReadOnlyList<FieldChange> Changes(Location? before, Location? after, string prefix = "location")
    {
        var changes = new List<FieldChange>();

        void Compare(string field, string label, string? previous, string? current)
        {
            if (previous != current)
            {
                changes.Add(new FieldChange($"{prefix}.{field}", label, previous, current));
            }
        }

        Compare("zipCode", "CEP", before?.ZipCode.Formatted, after?.ZipCode.Formatted);
        Compare("street", "Rua", before?.Street, after?.Street);
        Compare("neighborhoodId", "Bairro", before?.NeighborhoodId.ToString(), after?.NeighborhoodId.ToString());
        Compare("reference", "Referência", before?.Reference, after?.Reference);
        Compare("point", "Coordenadas", before?.Point.ToString(), after?.Point.ToString());
        return changes;
    }
}
