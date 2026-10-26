namespace Argos.Domain.Telemetry;

/// <summary>
/// Estado atual de um recurso (RF-007, RF-015, RF-024; D-70, D-71; plan 16.2): a última posição conhecida e a última
/// comunicação, derivadas das leituras (a fonte é a leitura, Art. V.3). Uma linha por recurso, fora dos agregados
/// <c>Equipment</c> e <c>Agent</c>, para uma posição a cada poucos segundos não mudar a versão deles (D-60).
/// <para>
/// <b>D-71:</b> <see cref="ApplyPosition"/> e <see cref="ApplyHeartbeat"/> só avançam. Uma leitura mais antiga que a
/// atual vai para o registro bruto, mas não altera o estado.
/// </para>
/// </summary>
public sealed class ResourceLiveState
{
    // Usado pelo EF Core ao materializar.
    private ResourceLiveState()
    {
    }

    private ResourceLiveState(ResourceKind resourceKind, Guid resourceId, DateTimeOffset lastCommunicationAt, DateTimeOffset updatedAt)
    {
        if (!Enum.IsDefined(resourceKind))
        {
            throw new ArgumentOutOfRangeException(nameof(resourceKind), resourceKind, "Tipo de recurso desconhecido.");
        }

        ResourceKind = resourceKind;
        ResourceId = resourceId;
        LastCommunicationAt = lastCommunicationAt.ToUniversalTime();
        UpdatedAt = updatedAt.ToUniversalTime();
    }

    public ResourceKind ResourceKind { get; private set; }

    public Guid ResourceId { get; private set; }

    public double? Latitude { get; private set; }

    public double? Longitude { get; private set; }

    /// <summary>Instante da medição da posição atual; nulo enquanto só houve sinal de vida.</summary>
    public DateTimeOffset? PositionRecordedAt { get; private set; }

    /// <summary>Instante da leitura mais recente, de qualquer tipo (US-14, RF-024).</summary>
    public DateTimeOffset LastCommunicationAt { get; private set; }

    /// <summary>Quando o estado mudou pela última vez, no relógio da API ("Última telemetria", Art. V.4).</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Estado criado pela primeira posição do recurso.</summary>
    /// <exception cref="Common.DomainValidationException">Coordenadas inválidas.</exception>
    public static ResourceLiveState FromPosition(
        ResourceKind resourceKind, Guid resourceId, double latitude, double longitude, DateTimeOffset recordedAt, DateTimeOffset receivedAt)
    {
        TelemetryCoordinates.Ensure(latitude, longitude);
        var state = new ResourceLiveState(resourceKind, resourceId, recordedAt, receivedAt);
        state.SetPosition(latitude, longitude, recordedAt);
        return state;
    }

    /// <summary>Estado criado pelo primeiro sinal de vida do recurso, ainda sem posição.</summary>
    public static ResourceLiveState FromHeartbeat(
        ResourceKind resourceKind, Guid resourceId, DateTimeOffset recordedAt, DateTimeOffset receivedAt) =>
        new(resourceKind, resourceId, recordedAt, receivedAt);

    /// <summary>
    /// Aplica uma posição (D-71): só se for mais recente que a atual. Devolve se a posição mudou, para a ingestão saber
    /// quais posições avisar à Central.
    /// </summary>
    /// <exception cref="Common.DomainValidationException">Coordenadas inválidas, mesmo numa leitura antiga.</exception>
    public bool ApplyPosition(double latitude, double longitude, DateTimeOffset recordedAt, DateTimeOffset receivedAt)
    {
        TelemetryCoordinates.Ensure(latitude, longitude);
        var communicated = AdvanceCommunication(recordedAt, receivedAt);

        if (PositionRecordedAt is { } current && recordedAt.ToUniversalTime() <= current)
        {
            return false;
        }

        SetPosition(latitude, longitude, recordedAt);
        if (!communicated)
        {
            UpdatedAt = receivedAt.ToUniversalTime();
        }

        return true;
    }

    /// <summary>Aplica um sinal de vida (D-71): só avança a última comunicação. Devolve se o estado mudou.</summary>
    public bool ApplyHeartbeat(DateTimeOffset recordedAt, DateTimeOffset receivedAt) => AdvanceCommunication(recordedAt, receivedAt);

    private bool AdvanceCommunication(DateTimeOffset recordedAt, DateTimeOffset receivedAt)
    {
        if (recordedAt.ToUniversalTime() <= LastCommunicationAt)
        {
            return false;
        }

        LastCommunicationAt = recordedAt.ToUniversalTime();
        UpdatedAt = receivedAt.ToUniversalTime();
        return true;
    }

    private void SetPosition(double latitude, double longitude, DateTimeOffset recordedAt)
    {
        Latitude = latitude;
        Longitude = longitude;
        PositionRecordedAt = recordedAt.ToUniversalTime();
    }
}
