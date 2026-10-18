using Argos.Domain.Inventory.Geography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argos.Infrastructure.Persistence.Configurations.Inventory;

/// <summary>Tabela <c>neighborhoods</c> (plan 15.13): nome único pela chave normalizada e ponto central.</summary>
internal sealed class NeighborhoodConfiguration : IEntityTypeConfiguration<Neighborhood>
{
    public void Configure(EntityTypeBuilder<Neighborhood> builder)
    {
        builder.ToTable("neighborhoods");

        builder.HasKey(neighborhood => neighborhood.Id);
        builder.Property(neighborhood => neighborhood.Id)
            .ValueGeneratedNever();

        builder.Property(neighborhood => neighborhood.Name)
            .HasMaxLength(Neighborhood.NameMaxLength)
            .IsRequired();

        builder.Property(neighborhood => neighborhood.NameKey)
            .HasMaxLength(Neighborhood.NameMaxLength)
            .IsRequired();
        builder.HasIndex(neighborhood => neighborhood.NameKey)
            .IsUnique();

        builder.HasOne<Region>()
            .WithMany()
            .HasForeignKey(neighborhood => neighborhood.RegionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ComplexProperty(neighborhood => neighborhood.Center)
            .HasPointColumns("center_latitude", "center_longitude");

        builder.HasDeactivationColumns().HasAuditColumns();
    }
}
