using Argos.Domain.Common;
using Argos.Domain.Inventory.Geography;
using static Argos.UnitTests.Domain.Inventory.InventoryTestData;

namespace Argos.UnitTests.Domain.Inventory;

/// <summary>
/// Coordenadas, polígonos, CEP, município e localização (RN-02, RN-03; plan 15.2.1, 15.2.4).
/// </summary>
public sealed class GeographyTests
{
    private static readonly GeoPolygon WithHole = GeoPolygon.Create(
    [
        [Point(0, 0), Point(0, 10), Point(10, 10), Point(10, 0), Point(0, 0)],
        [Point(4, 4), Point(4, 6), Point(6, 6), Point(6, 4), Point(4, 4)],
    ]);

    [Theory]
    [InlineData(5, 2, true)]
    [InlineData(5, 11, false)]
    [InlineData(-1, 5, false)]
    public void Contains_InsideAndOutside_RN02(double latitude, double longitude, bool expected)
    {
        Assert.Equal(expected, WithHole.Contains(Point(latitude, longitude)));
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(10, 10)]
    [InlineData(5, 0)]
    [InlineData(4, 5)]
    public void Contains_PointOnBoundary_CountsAsInside_RN02(double latitude, double longitude)
    {
        Assert.True(WithHole.Contains(Point(latitude, longitude)));
    }

    [Fact]
    public void Contains_PointInHole_IsOutside_RN02()
    {
        Assert.False(WithHole.Contains(Point(5, 5)));
    }

    [Theory]
    [InlineData(91, 0, "latitude")]
    [InlineData(-90.5, 0, "latitude")]
    [InlineData(0, 180.1, "longitude")]
    [InlineData(double.NaN, 0, "latitude")]
    public void GeoPoint_InvalidCoordinate_IsRejectedInField_RN02(double latitude, double longitude, string field)
    {
        var exception = Assert.Throws<DomainValidationException>(() => GeoPoint.Create(latitude, longitude));

        Assert.Contains(field, exception.Errors.Keys);
    }

    [Fact]
    public void GeoPoint_IsRoundedToSixDecimals()
    {
        var point = Point(-22.1234567, -49.7654321);

        Assert.Equal(-22.123457, point.Latitude);
        Assert.Equal(-49.765432, point.Longitude);
        Assert.Equal(Point(-22.123457, -49.765432), point);
    }

    [Fact]
    public void GeoPolygon_OpenRingOrTooFewVertices_IsRejected()
    {
        Assert.Throws<DomainValidationException>(() =>
            GeoPolygon.FromRing([Point(0, 0), Point(0, 1), Point(1, 1), Point(1, 0)]));
        Assert.Throws<DomainValidationException>(() =>
            GeoPolygon.FromRing([Point(0, 0), Point(0, 1), Point(0, 0)]));
    }

    [Theory]
    [InlineData("16985001", "16985001")]
    [InlineData("16985-001", "16985001")]
    [InlineData(" 16985-001 ", "16985001")]
    public void ZipCode_EightDigitsWithOrWithoutHyphen_IsAccepted_RN02(string text, string expected)
    {
        var zipCode = ZipCode.Parse(text);

        Assert.Equal(expected, zipCode.Value);
        Assert.Equal("16985-001", zipCode.Formatted);
    }

    [Theory]
    [InlineData("1698500")]
    [InlineData("169850011")]
    [InlineData("16985.001")]
    [InlineData("1698-5001")]
    [InlineData("ABCDE-FGH")]
    [InlineData("")]
    [InlineData(null)]
    public void ZipCode_OtherForms_AreRejectedInZipCodeField_RN02(string? text)
    {
        var exception = Assert.Throws<DomainValidationException>(() => ZipCode.Parse(text));

        Assert.Equal(["zipCode"], exception.Errors.Keys);
    }

    [Fact]
    public void Municipality_CenterOutsideBoundary_IsRejected()
    {
        var exception = Assert.Throws<DomainValidationException>(() => Municipality.Create(
            "Prometeia", "SP", "America/Sao_Paulo", Square(-22.30, -49.80, -22.10, -49.50), Point(-21, -49.65)));

        Assert.Contains("center", exception.Errors.Keys);
    }

    [Fact]
    public void Municipality_UnknownTimeZone_IsRejected()
    {
        var exception = Assert.Throws<DomainValidationException>(() => Municipality.Create(
            "Prometeia", "SP", "Mars/Olympus", Square(-22.30, -49.80, -22.10, -49.50), Point(-22.2, -49.65)));

        Assert.Contains("timeZoneId", exception.Errors.Keys);
    }

    [Fact]
    public void Municipality_DateOf_UsesMunicipalTimeZone_D54()
    {
        var municipality = NewMunicipality();

        // 01:30 UTC de 19/10 ainda é 18/10 em São Paulo (UTC−3).
        Assert.Equal(new DateOnly(2026, 10, 18), municipality.DateOf(new DateTimeOffset(2026, 10, 19, 1, 30, 0, TimeSpan.Zero)));
        Assert.Equal(new DateOnly(2026, 10, 19), municipality.DateOf(new DateTimeOffset(2026, 10, 19, 3, 30, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Location_Valid_TakesCityAndStateFromMunicipality_RN02()
    {
        var municipality = NewMunicipality();
        var neighborhood = NewNeighborhood(municipality);

        var location = Location.Create(municipality, neighborhood, "16985001", " Rua das Acácias ", "S/N", -22.2, -49.6);

        Assert.Equal("Prometeia", location.City);
        Assert.Equal("SP", location.State);
        Assert.Equal("Rua das Acácias", location.Street);
        Assert.Equal(neighborhood.Id, location.NeighborhoodId);
    }

    [Fact]
    public void Location_PointOutsideMunicipality_PointsAtCoordinates_RN02()
    {
        var municipality = NewMunicipality();

        var exception = Assert.Throws<DomainValidationException>(() => Location.Create(
            municipality, NewNeighborhood(municipality), "16985001", "Rua das Acácias", "número 181", -23.5, -46.6));

        Assert.Equal(["latitude", "longitude"], exception.Errors.Keys.Order());
    }

    [Theory]
    [InlineData("número 181")]
    [InlineData("oposto ao número 181")]
    [InlineData("KM 203")]
    [InlineData("S/N")]
    [InlineData("em frente ao Mercado do Tio")]
    public void Location_FreeTextReference_IsAccepted_RN03(string reference)
    {
        var municipality = NewMunicipality();

        var location = Location.Create(
            municipality, NewNeighborhood(municipality), "16985001", "Rua das Acácias", reference, -22.2, -49.6);

        Assert.Equal(reference, location.Reference);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Location_MissingReference_IsRejectedInReferenceField_RN03(string? reference)
    {
        var municipality = NewMunicipality();

        var exception = Assert.Throws<DomainValidationException>(() => Location.Create(
            municipality, NewNeighborhood(municipality), "16985001", "Rua das Acácias", reference, -22.2, -49.6));

        Assert.Equal(["reference"], exception.Errors.Keys);
    }

    [Fact]
    public void Location_SeveralErrors_AreReportedAtOnce_D46()
    {
        var municipality = NewMunicipality();

        var exception = Assert.Throws<DomainValidationException>(() => Location.Create(
            municipality, NewNeighborhood(municipality), "123", "R", "", 95, -49.6));

        Assert.Equal(["latitude", "reference", "street", "zipCode"], exception.Errors.Keys.Order());
    }

    [Fact]
    public void Location_InactiveNeighborhood_IsRejected_RN22()
    {
        var municipality = NewMunicipality();
        var neighborhood = NewNeighborhood(municipality);
        neighborhood.Inactivate(hasActiveDependents: false, Now, Author);

        var exception = Assert.Throws<DomainValidationException>(() => Location.Create(
            municipality, neighborhood, "16985001", "Rua das Acácias", "S/N", -22.2, -49.6));

        Assert.Equal(["neighborhoodId"], exception.Errors.Keys);
    }

    [Fact]
    public void Location_Changes_ListsOnlyChangedFields_D49()
    {
        var municipality = NewMunicipality();
        var neighborhood = NewNeighborhood(municipality);
        var before = Location.Create(municipality, neighborhood, "16985001", "Rua das Acácias", "S/N", -22.2, -49.6);
        var after = Location.Create(municipality, neighborhood, "16985002", "Rua das Acácias", "número 10", -22.2, -49.6);

        var changes = Location.Changes(before, after);

        Assert.Equal(["location.reference", "location.zipCode"], changes.Select(change => change.Field).Order());
        Assert.Contains(changes, change => change is { Before: "16985-001", After: "16985-002" });
    }

    [Fact]
    public void ZipCodeEntry_PointOutsideMunicipality_IsRejected_RN02()
    {
        var municipality = NewMunicipality();

        Assert.Throws<Argos.Domain.Inventory.InventoryRuleException>(() => ZipCodeEntry.Create(
            ZipCode.Parse("16985001"), "Rua das Acácias", NewNeighborhood(municipality), Point(-23, -46), municipality));
    }
}
