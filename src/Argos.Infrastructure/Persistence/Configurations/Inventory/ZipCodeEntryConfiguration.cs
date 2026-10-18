using Argos.Domain.Inventory.Geography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argos.Infrastructure.Persistence.Configurations.Inventory;

/// <summary>Tabela <c>zip_codes</c> (plan 15.13; D-52): o CEP, com os 8 dígitos sem hífen, é a chave.</summary>
internal sealed class ZipCodeEntryConfiguration : IEntityTypeConfiguration<ZipCodeEntry>
{
    public void Configure(EntityTypeBuilder<ZipCodeEntry> builder)
    {
        builder.ToTable("zip_codes");

        builder.HasKey(entry => entry.ZipCode);
        builder.Property(entry => entry.ZipCode)
            .HasConversion(GeographyColumns.ZipCodeConverter)
            .HasMaxLength(GeographyColumns.ZipCodeLength)
            .IsFixedLength()
            .ValueGeneratedNever();

        builder.Property(entry => entry.Street)
            .HasMaxLength(Location.StreetMaxLength)
            .IsRequired();

        builder.HasOne<Neighborhood>()
            .WithMany()
            .HasForeignKey(entry => entry.NeighborhoodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ComplexProperty(entry => entry.ReferencePoint)
            .HasPointColumns("latitude", "longitude");
    }
}
