using System.Globalization;
using Argos.Domain.Common;

namespace Argos.Domain.Inventory.Geography;

/// <summary>
/// Coordenada geográfica (plan 15.2.4), arredondada a 6 casas decimais (~0,1 m). Latitude entre −90 e 90 e longitude
/// entre −180 e 180 (coordenada válida, RN-02); estar dentro do município é verificado por <see cref="Location"/>.
/// </summary>
public sealed record GeoPoint
{
    private const int Decimals = 6;

    private GeoPoint(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public double Latitude { get; }

    public double Longitude { get; }

    /// <summary>Valida e cria o ponto. Erros nos campos <c>latitude</c> e <c>longitude</c>, juntos.</summary>
    public static GeoPoint Create(double latitude, double longitude)
    {
        var errors = new FieldErrors();

        if (!double.IsFinite(latitude) || latitude is < -90 or > 90)
        {
            errors.Add("latitude", "Informe uma latitude entre -90 e 90.");
        }

        if (!double.IsFinite(longitude) || longitude is < -180 or > 180)
        {
            errors.Add("longitude", "Informe uma longitude entre -180 e 180.");
        }

        errors.ThrowIfAny();
        return new GeoPoint(Math.Round(latitude, Decimals), Math.Round(longitude, Decimals));
    }

    /// <summary>Texto de exibição, com ponto decimal, como as coordenadas costumam ser escritas.</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Latitude:0.######}, {Longitude:0.######}");
}
