namespace Argos.Web.Interop;

/// <summary>Opções do seletor de ponto, serializadas para o <c>js/map.js</c> (coordenadas como [latitude, longitude]).</summary>
public sealed record PickerOptions(
    double Latitude,
    double Longitude,
    int Zoom,
    bool IsDarkMode,
    double[][]? Boundary,
    double[]? Point,
    bool ReadOnly);
