using Argos.Domain.Inventory.Equipments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argos.Infrastructure.Persistence.Configurations.Inventory;

/// <summary>
/// Tabela <c>equipment_status_changes</c> (plan 15.2.7, 15.13): histórico append-only de situação (<c>IAppendOnly</c>,
/// Art. V.1), lido do mais recente para o mais antigo.
/// </summary>
internal sealed class EquipmentStatusChangeConfiguration : IEntityTypeConfiguration<EquipmentStatusChange>
{
    public void Configure(EntityTypeBuilder<EquipmentStatusChange> builder)
    {
        builder.ToTable("equipment_status_changes");

        builder.HasKey(change => change.Id);
        builder.Property(change => change.Id)
            .ValueGeneratedNever();

        builder.Property(change => change.FromStatus)
            .HasEnumText();

        builder.Property(change => change.ToStatus)
            .HasEnumText();

        builder.Property(change => change.Origin)
            .HasEnumText();

        builder.Property(change => change.Justification)
            .HasMaxLength(Equipment.JustificationMaxLength);

        builder.HasOne<Maintenance>()
            .WithMany()
            .HasForeignKey(change => change.MaintenanceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(change => change.Author)
            .HasMaxLength(ColumnConventions.AuthorMaxLength)
            .IsRequired();

        builder.HasIndex(change => new { change.EquipmentId, change.OccurredAt });
    }
}
