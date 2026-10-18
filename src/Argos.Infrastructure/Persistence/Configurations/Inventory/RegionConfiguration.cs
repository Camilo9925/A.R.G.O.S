using Argos.Domain.Inventory.Geography;
using Argos.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argos.Infrastructure.Persistence.Configurations.Inventory;

/// <summary>Tabela <c>regions</c> (plan 15.13). O nome é único pela chave normalizada, inclusive entre as inativas.</summary>
internal sealed class RegionConfiguration : IEntityTypeConfiguration<Region>
{
    public void Configure(EntityTypeBuilder<Region> builder)
    {
        builder.ToTable("regions");

        builder.HasKey(region => region.Id);
        builder.Property(region => region.Id)
            .ValueGeneratedNever();

        builder.Property(region => region.Name)
            .HasMaxLength(Region.NameMaxLength)
            .IsRequired();

        builder.Property(region => region.NameKey)
            .HasMaxLength(Region.NameMaxLength)
            .IsRequired();
        builder.HasIndex(region => region.NameKey)
            .IsUnique();

        builder.Property(region => region.Boundary!)
            .HasGeoJson();

        builder.HasDeactivationColumns().HasAuditColumns();
    }
}
