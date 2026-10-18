using Argos.Domain.Inventory.Equipments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argos.Infrastructure.Persistence.Configurations.Inventory;

/// <summary>
/// Tabela <c>maintenances</c> (plan 15.2.8, 15.13). A data prevista é <c>date</c>, um dia do calendário do município
/// (D-54). Os índices servem ao agregado (abertas do equipamento) e à necessidade de manutenção da pesquisa.
/// </summary>
internal sealed class MaintenanceConfiguration : IEntityTypeConfiguration<Maintenance>
{
    public void Configure(EntityTypeBuilder<Maintenance> builder)
    {
        builder.ToTable("maintenances");

        builder.HasKey(maintenance => maintenance.Id);
        builder.Property(maintenance => maintenance.Id)
            .ValueGeneratedNever();

        builder.Property(maintenance => maintenance.Kind)
            .HasEnumText();

        builder.Property(maintenance => maintenance.State)
            .HasEnumText();

        builder.Property(maintenance => maintenance.Description)
            .HasMaxLength(Equipment.DescriptionMaxLength);

        builder.Property(maintenance => maintenance.DueDate)
            .HasColumnType("date");

        builder.Property(maintenance => maintenance.Origin)
            .HasEnumText();

        builder.HasOne<Maintenance>()
            .WithMany()
            .HasForeignKey(maintenance => maintenance.PreviousMaintenanceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(maintenance => maintenance.OpenedBy)
            .HasMaxLength(ColumnConventions.AuthorMaxLength)
            .IsRequired();

        builder.Property(maintenance => maintenance.CompletedBy)
            .HasMaxLength(ColumnConventions.AuthorMaxLength);

        builder.Property(maintenance => maintenance.Result)
            .HasEnumText();

        builder.Property(maintenance => maintenance.CompletionNotes)
            .HasMaxLength(Equipment.CompletionNotesMaxLength);

        builder.Property(maintenance => maintenance.CancelledBy)
            .HasMaxLength(ColumnConventions.AuthorMaxLength);

        builder.Property(maintenance => maintenance.CancellationReason)
            .HasMaxLength(Equipment.DescriptionMaxLength);

        builder.HasIndex(maintenance => new { maintenance.EquipmentId, maintenance.State });
        builder.HasIndex(maintenance => new { maintenance.State, maintenance.DueDate });

        builder.HasAuditColumns();
    }
}
