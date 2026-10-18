using Argos.Domain.Inventory;
using Argos.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argos.Infrastructure.Persistence.Configurations.Inventory;

/// <summary>
/// Tabela <c>inventory_changes</c> (plan 15.2.11, 15.13; D-49): registro append-only das alterações cadastrais, com os
/// campos alterados em <c>jsonb</c>.
/// </summary>
internal sealed class InventoryChangeConfiguration : IEntityTypeConfiguration<InventoryChange>
{
    private const int ReasonMaxLength = 500;

    public void Configure(EntityTypeBuilder<InventoryChange> builder)
    {
        builder.ToTable("inventory_changes");

        builder.HasKey(change => change.Id);
        builder.Property(change => change.Id)
            .ValueGeneratedNever();

        builder.Property(change => change.EntityType)
            .HasEnumText();

        builder.Property(change => change.Kind)
            .HasEnumText();

        builder.Property(change => change.Author)
            .HasMaxLength(ColumnConventions.AuthorMaxLength)
            .IsRequired();

        builder.Property(change => change.Reason)
            .HasMaxLength(ReasonMaxLength);

        builder.Property(change => change.Changes)
            .HasChangesJson()
            .IsRequired();

        builder.HasIndex(change => new { change.EntityType, change.EntityId, change.OccurredAt });
    }
}
