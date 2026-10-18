using Argos.Domain.Inventory.Geography;
using Argos.Domain.Inventory.Support;

namespace Argos.UnitTests.Domain.Inventory;

/// <summary>
/// Cenário mínimo dos testes do Inventário: um município quadrado (lat −22,30 a −22,10; lon −49,80 a −49,50),
/// uma região, um bairro, um órgão e uma base.
/// </summary>
internal static class InventoryTestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 18, 13, 0, 0, TimeSpan.Zero);

    public const string Author = "admin";

    public static GeoPoint Point(double latitude, double longitude) => GeoPoint.Create(latitude, longitude);

    public static GeoPolygon Square(double south, double west, double north, double east) =>
        GeoPolygon.FromRing(
        [
            Point(north, west),
            Point(north, east),
            Point(south, east),
            Point(south, west),
            Point(north, west),
        ]);

    public static Municipality NewMunicipality() =>
        Municipality.Create(
            "Prometeia", "SP", "America/Sao_Paulo", Square(-22.30, -49.80, -22.10, -49.50), Point(-22.20, -49.65));

    public static Region NewRegion(string name = "Centro") => Region.Create(name);

    public static Neighborhood NewNeighborhood(Municipality? municipality = null, Region? region = null) =>
        Neighborhood.Create(
            "Jardim das Musas", region ?? NewRegion(), Point(-22.20, -49.65), municipality ?? NewMunicipality());

    public static Location NewLocation(Municipality? municipality = null, Neighborhood? neighborhood = null)
    {
        municipality ??= NewMunicipality();
        return Location.Create(
            municipality,
            neighborhood ?? NewNeighborhood(municipality),
            "16985-001",
            "Rua das Acácias",
            "número 181",
            -22.21,
            -49.66);
    }

    public static Organization NewOrganization(string name = "Secretaria Municipal de Mobilidade", string acronym = "SMM") =>
        Organization.Create(name, acronym);

    public static OperationalBase NewBase(Organization? organization = null) =>
        OperationalBase.Create("Central de Mobilidade", organization ?? NewOrganization(), NewLocation());
}
