using Argos.Domain.Common;

namespace Argos.Domain.Inventory.Geography;

/// <summary>
/// O município da demonstração, Prometeia/SP (RF-020, RN-02; D-50; plan 15.2.1). Registro único, carregado pelo
/// Migrator e sem edição pela interface (YAGNI). Dá a cidade e a UF de toda localização (RF-004) e o fuso em que se
/// conta o "hoje" das manutenções (D-54).
/// </summary>
public sealed class Municipality
{
    private TimeZoneInfo? _timeZone;

    // Usado pelo EF Core ao materializar.
    private Municipality()
    {
    }

    private Municipality(Guid id, string name, string state, string timeZoneId, GeoPolygon boundary, GeoPoint center)
    {
        Id = id;
        Name = name;
        State = state;
        TimeZoneId = timeZoneId;
        Boundary = boundary;
        Center = center;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>UF, em duas letras maiúsculas.</summary>
    public string State { get; private set; } = string.Empty;

    /// <summary>Identificador IANA do fuso, como <c>America/Sao_Paulo</c>.</summary>
    public string TimeZoneId { get; private set; } = string.Empty;

    public GeoPolygon Boundary { get; private set; } = null!;

    public GeoPoint Center { get; private set; } = null!;

    public TimeZoneInfo TimeZone => _timeZone ??= TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    public static Municipality Create(string name, string state, string timeZoneId, GeoPolygon boundary, GeoPoint center)
    {
        var errors = new FieldErrors();
        var validName = errors.RequiredText("name", name, 2, 80, "o nome do município");
        var validState = (state ?? string.Empty).Trim().ToUpperInvariant();

        if (validState.Length != 2 || !validState.All(char.IsAsciiLetterUpper))
        {
            errors.Add("state", "Informe a UF com duas letras.");
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId ?? string.Empty, out _))
        {
            errors.Add("timeZoneId", "Fuso horário desconhecido.");
        }

        if (!boundary.Contains(center))
        {
            errors.Add("center", "O centro deve ficar dentro do contorno do município.");
        }

        errors.ThrowIfAny();
        return new Municipality(Guid.NewGuid(), validName, validState, timeZoneId!, boundary, center);
    }

    /// <summary>RN-02: o ponto está dentro do município (ponto em polígono).</summary>
    public bool Contains(GeoPoint point) => Boundary.Contains(point);

    /// <summary>Data do instante no fuso do município (D-54): o "hoje" e a data de conclusão das manutenções.</summary>
    public DateOnly DateOf(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, TimeZone).DateTime);
}
