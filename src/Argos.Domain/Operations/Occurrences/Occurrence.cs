using Argos.Domain.Common;
using Argos.Domain.Inventory.Geography;
using Argos.Domain.Telemetry;

namespace Argos.Domain.Operations.Occurrences;

/// <summary>
/// Ocorrência registrada na cidade (RF-025, parcial; RN-02, RN-23; Art. V.1, IX.2; D-86; plan 17.2): tipo, instante,
/// bairro e ponto dentro do município. Nunca é alterada nem excluída. Na entrega só a carga simulada as cria
/// (<c>IsSimulated</c>, autor <c>simulação</c>); o recurso de origem fica para as ocorrências ao vivo (A-31).
/// </summary>
public sealed class Occurrence : IAppendOnly
{
    public const int StreetMaxLength = 120;

    public const int DescriptionMaxLength = 300;

    // Usado pelo EF Core ao materializar.
    private Occurrence()
    {
    }

    private Occurrence(
        OccurrenceKind kind,
        DateTimeOffset occurredAt,
        Guid neighborhoodId,
        GeoPoint point,
        string street,
        string description,
        bool isSimulated,
        string author)
    {
        Id = Guid.NewGuid();
        Kind = kind;
        OccurredAt = occurredAt.ToUniversalTime();
        NeighborhoodId = neighborhoodId;
        Latitude = point.Latitude;
        Longitude = point.Longitude;
        Street = street;
        Description = description;
        IsSimulated = isSimulated;
        Author = author;
    }

    public Guid Id { get; private set; }

    public OccurrenceKind Kind { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public Guid NeighborhoodId { get; private set; }

    public double Latitude { get; private set; }

    public double Longitude { get; private set; }

    public string Street { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    /// <summary>Recurso de origem, quando houver; nulo nas simuladas (A-31).</summary>
    public ResourceKind? SourceResourceKind { get; private set; }

    public Guid? SourceResourceId { get; private set; }

    public bool IsSimulated { get; private set; }

    /// <summary>Autor do registro: <c>simulação</c> na carga (Art. IX.2).</summary>
    public string Author { get; private set; } = string.Empty;

    /// <summary>Registra uma ocorrência com o ponto dentro do município (RN-02).</summary>
    /// <exception cref="DomainValidationException">Campo inválido ou ponto fora do município (<c>latitude</c>).</exception>
    public static Occurrence Record(
        OccurrenceKind kind,
        DateTimeOffset occurredAt,
        Guid neighborhoodId,
        double latitude,
        double longitude,
        string street,
        string description,
        bool isSimulated,
        string author,
        Municipality municipality)
    {
        ArgumentNullException.ThrowIfNull(municipality);

        if (string.IsNullOrWhiteSpace(author))
        {
            throw new ArgumentException("Informe o autor da operação.", nameof(author));
        }

        var errors = new FieldErrors();

        if (!Enum.IsDefined(kind))
        {
            errors.Add("kind", "Tipo de ocorrência desconhecido.");
        }

        var validStreet = errors.RequiredText("street", street, 2, StreetMaxLength, "o logradouro");
        var validDescription = errors.RequiredText("description", description, 2, DescriptionMaxLength, "a descrição");
        errors.ThrowIfAny();

        var point = GeoPoint.Create(latitude, longitude);

        if (!municipality.Contains(point))
        {
            throw new DomainValidationException("latitude", "O ponto da ocorrência deve ficar dentro do município.");
        }

        return new Occurrence(kind, occurredAt, neighborhoodId, point, validStreet, validDescription, isSimulated, author.Trim());
    }
}
