using Argos.Domain.Common;

namespace Argos.Domain.Telemetry;

/// <summary>
/// Leitura de telemetria recebida (RF-023, RF-024; D-70; plan 16.2): uma posição ou um sinal de vida, com o instante da
/// medição (<see cref="RecordedAt"/>) e o do recebimento (<see cref="ReceivedAt"/>). Registro bruto: gravado uma única
/// vez e nunca alterado nem removido (Art. V.1). Posição exige coordenadas; sinal de vida não as tem.
/// </summary>
public sealed class TelemetryReading : IAppendOnly
{
    // Usado pelo EF Core ao materializar.
    private TelemetryReading()
    {
    }

    private TelemetryReading(
        ResourceKind resourceKind,
        Guid resourceId,
        TelemetryReadingKind kind,
        DateTimeOffset recordedAt,
        DateTimeOffset receivedAt,
        double? latitude,
        double? longitude,
        bool isSimulated,
        string author)
    {
        if (!Enum.IsDefined(resourceKind))
        {
            throw new ArgumentOutOfRangeException(nameof(resourceKind), resourceKind, "Tipo de recurso desconhecido.");
        }

        ResourceKind = resourceKind;
        ResourceId = resourceId;
        Kind = kind;
        RecordedAt = recordedAt.ToUniversalTime();
        ReceivedAt = receivedAt.ToUniversalTime();
        Latitude = latitude;
        Longitude = longitude;
        IsSimulated = isSimulated;
        Author = TelemetryCoordinates.ValidAuthor(author);
    }

    /// <summary>Gerado pelo banco.</summary>
    public long Id { get; private set; }

    public ResourceKind ResourceKind { get; private set; }

    public Guid ResourceId { get; private set; }

    public TelemetryReadingKind Kind { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public double? Latitude { get; private set; }

    public double? Longitude { get; private set; }

    /// <summary>Se a leitura veio do Simulador (Art. IX.1).</summary>
    public bool IsSimulated { get; private set; }

    /// <summary>Quem enviou: <c>PROMETHEUS</c> na entrega.</summary>
    public string Author { get; private set; } = string.Empty;

    /// <exception cref="DomainValidationException">Coordenadas inválidas.</exception>
    public static TelemetryReading Position(
        ResourceKind resourceKind,
        Guid resourceId,
        double latitude,
        double longitude,
        DateTimeOffset recordedAt,
        DateTimeOffset receivedAt,
        bool isSimulated,
        string author)
    {
        TelemetryCoordinates.Ensure(latitude, longitude);
        return new TelemetryReading(
            resourceKind, resourceId, TelemetryReadingKind.Position, recordedAt, receivedAt, latitude, longitude, isSimulated, author);
    }

    public static TelemetryReading Heartbeat(
        ResourceKind resourceKind, Guid resourceId, DateTimeOffset recordedAt, DateTimeOffset receivedAt, bool isSimulated, string author) =>
        new(resourceKind, resourceId, TelemetryReadingKind.Heartbeat, recordedAt, receivedAt, null, null, isSimulated, author);
}
