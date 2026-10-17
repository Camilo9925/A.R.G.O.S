namespace Argos.Web.Components.Map;

/// <summary>
/// Centro e zoom iniciais do mapa, vindos do appsettings da Web (<c>Map:Center</c>, <c>Map:Zoom</c>).
/// Apontam para a área urbana usada como base de Prometeia (D-07); o nome da cidade-base não aparece na interface.
/// </summary>
public sealed record MapOptions(double CenterLatitude, double CenterLongitude, int Zoom);
