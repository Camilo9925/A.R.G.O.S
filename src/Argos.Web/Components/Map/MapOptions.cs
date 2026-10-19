namespace Argos.Web.Components.Map;

/// <summary>
/// Centro e zoom iniciais do mapa, vindos do appsettings da Web (<c>Map:Center</c>, <c>Map:Zoom</c>).
/// Apontam para o centro do município de Prometeia (D-07, D-50), o mesmo do <c>prometeia.json</c>; o nome da cidade-base
/// não aparece na interface.
/// </summary>
public sealed record MapOptions(double CenterLatitude, double CenterLongitude, int Zoom);
