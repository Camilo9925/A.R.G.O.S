using Argos.Domain.Inventory.Geography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Argos.Infrastructure.Persistence.Configurations.Inventory;

/// <summary>
/// Objetos de valor de geografia como tipos complexos (plan 15.13): colunas na própria tabela, sem tabela nem chave
/// própria. Trocar a localização por outra é uma atualização das colunas, nunca <c>DELETE</c> + <c>INSERT</c>
/// (plan 5.4), e o EF compara coluna a coluna, então só o que mudou é gravado.
/// </summary>
internal static class GeographyColumns
{
    public const int ZipCodeLength = 8;
    public const int StateLength = 2;
    public const int CityMaxLength = 80;

    /// <summary>CEP gravado com os 8 dígitos, sem hífen (plan 15.2.4).</summary>
    public static readonly ValueConverter<ZipCode, string> ZipCodeConverter =
        new(zipCode => zipCode.Value, digits => ZipCode.Parse(digits, "zipCode"));

    public static ComplexPropertyBuilder<GeoPoint> HasPointColumns(
        this ComplexPropertyBuilder<GeoPoint> point, string latitudeColumn, string longitudeColumn)
    {
        point.Property(value => value.Latitude).HasColumnName(latitudeColumn);
        point.Property(value => value.Longitude).HasColumnName(longitudeColumn);
        return point;
    }

    /// <summary>Colunas <c>location_*</c> (plan 15.13), as mesmas em <c>equipment</c> e <c>operational_bases</c>.</summary>
    public static ComplexPropertyBuilder<Location> HasLocationColumns(this ComplexPropertyBuilder<Location> location)
    {
        location.Property(value => value.ZipCode)
            .HasConversion(ZipCodeConverter)
            .HasMaxLength(ZipCodeLength)
            .IsFixedLength()
            .HasColumnName("location_zip_code")
            .IsRequired();

        location.Property(value => value.Street)
            .HasColumnName("location_street")
            .HasMaxLength(Location.StreetMaxLength)
            .IsRequired();

        location.Property(value => value.NeighborhoodId)
            .HasColumnName("location_neighborhood_id");

        location.Property(value => value.City)
            .HasColumnName("location_city")
            .HasMaxLength(CityMaxLength)
            .IsRequired();

        location.Property(value => value.State)
            .HasColumnName("location_state")
            .HasMaxLength(StateLength)
            .IsFixedLength()
            .IsRequired();

        location.Property(value => value.Reference)
            .HasColumnName("location_reference")
            .HasMaxLength(Location.ReferenceMaxLength)
            .IsRequired();

        location.ComplexProperty(value => value.Point)
            .HasPointColumns("location_latitude", "location_longitude");

        return location;
    }
}
