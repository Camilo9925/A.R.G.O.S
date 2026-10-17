using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argos.Infrastructure.Persistence.Configurations;

internal sealed class SeedHistoryEntryConfiguration : IEntityTypeConfiguration<SeedHistoryEntry>
{
    public void Configure(EntityTypeBuilder<SeedHistoryEntry> builder)
    {
        builder.ToTable("seed_history");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Version)
            .HasMaxLength(SeedHistoryEntry.VersionMaxLength)
            .IsRequired();

        builder.Property(entry => entry.AppliedAt)
            .IsRequired();

        builder.HasIndex(entry => new { entry.Version, entry.Seed })
            .IsUnique();
    }
}
