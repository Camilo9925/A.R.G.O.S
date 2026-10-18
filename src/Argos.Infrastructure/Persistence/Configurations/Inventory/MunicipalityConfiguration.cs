using Argos.Domain.Inventory.Geography;
using Argos.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argos.Infrastructure.Persistence.Configurations.Inventory;

/// <summary>Tabela <c>municipalities</c> (plan 15.13): registro único, com o contorno em GeoJSON (D-50).</summary>
internal sealed class MunicipalityConfiguration : IEntityTypeConfiguration<Municipality>
{
    private const int TimeZoneIdMaxLength = 64;

    public void Configure(EntityTypeBuilder<Municipality> builder)
    {
        builder.ToTable("municipalities");

        builder.HasKey(municipality => municipality.Id);
        builder.Property(municipality => municipality.Id)
            .ValueGeneratedNever();

        builder.Property(municipality => municipality.Name)
            .HasMaxLength(GeographyColumns.CityMaxLength)
            .IsRequired();

        builder.Property(municipality => municipality.State)
            .HasMaxLength(GeographyColumns.StateLength)
            .IsFixedLength()
            .IsRequired();

        builder.Property(municipality => municipality.TimeZoneId)
            .HasMaxLength(TimeZoneIdMaxLength)
            .IsRequired();

        builder.Property(municipality => municipality.Boundary)
            .HasGeoJson()
            .IsRequired();

        builder.ComplexProperty(municipality => municipality.Center)
            .HasPointColumns("center_latitude", "center_longitude");
    }
}
