using Argos.Domain.Common;

namespace Argos.Domain.Telemetry;

/// <summary>
/// Coordenadas de uma leitura de telemetria (plan 16.2, 16.4): latitude entre −90 e 90 e longitude entre −180 e 180,
/// sem conferir o contorno do município, porque o Simulador só gera pontos sobre as ruas. As mensagens são as da
/// validação do lote, para o mesmo erro sair igual da API e do Domínio.
/// </summary>
public static class TelemetryCoordinates
{
    public const string LatitudeMessage = "A latitude deve estar entre -90 e 90.";

    public const string LongitudeMessage = "A longitude deve estar entre -180 e 180.";

    public static bool IsValidLatitude(double latitude) => double.IsFinite(latitude) && latitude is >= -90 and <= 90;

    public static bool IsValidLongitude(double longitude) => double.IsFinite(longitude) && longitude is >= -180 and <= 180;

    /// <exception cref="DomainValidationException">Coordenada fora da faixa, nos campos <c>latitude</c> e <c>longitude</c>.</exception>
    internal static void Ensure(double latitude, double longitude)
    {
        var errors = new FieldErrors();

        if (!IsValidLatitude(latitude))
        {
            errors.Add("latitude", LatitudeMessage);
        }

        if (!IsValidLongitude(longitude))
        {
            errors.Add("longitude", LongitudeMessage);
        }

        errors.ThrowIfAny();
    }

    internal static string ValidAuthor(string by) =>
        string.IsNullOrWhiteSpace(by)
            ? throw new ArgumentException("Informe o autor da operação.", nameof(by))
            : by.Trim();
}
