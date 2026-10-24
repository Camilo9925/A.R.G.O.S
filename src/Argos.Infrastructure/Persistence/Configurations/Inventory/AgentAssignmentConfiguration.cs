using Argos.Domain.Inventory.Agents;
using Argos.Domain.Inventory.Equipments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argos.Infrastructure.Persistence.Configurations.Inventory;

/// <summary>
/// Tabela <c>agent_assignments</c> (plan 15.2.10, 15.13; D-62): o vínculo como entidade explícita com início e fim,
/// nunca coleção substituída (que geraria <c>DELETE</c>). O índice único parcial em <c>agent_id</c> com
/// <c>ended_at</c> nulo garante no banco que um agente tenha no máximo um vínculo aberto (US-07).
/// </summary>
internal sealed class AgentAssignmentConfiguration : IEntityTypeConfiguration<AgentAssignment>
{
    /// <summary>Nome do índice único parcial do vínculo aberto do agente.</summary>
    public const string OpenAssignmentIndex = "ux_agent_assignments_agent_id_open";

    public void Configure(EntityTypeBuilder<AgentAssignment> builder)
    {
        builder.ToTable("agent_assignments");

        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Id)
            .ValueGeneratedNever();

        // Calculada pelo Domínio a partir de EndedAt; não é coluna.
        builder.Ignore(assignment => assignment.IsOpen);

        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(assignment => assignment.AgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(assignment => assignment.AgentId)
            .IsUnique()
            .HasFilter("ended_at IS NULL")
            .HasDatabaseName(OpenAssignmentIndex);

        builder.HasOne<Equipment>()
            .WithMany()
            .HasForeignKey(assignment => assignment.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(assignment => new { assignment.EquipmentId, assignment.EndedAt });

        builder.Property(assignment => assignment.StartedBy)
            .HasMaxLength(ColumnConventions.AuthorMaxLength)
            .IsRequired();

        builder.Property(assignment => assignment.EndedBy)
            .HasMaxLength(ColumnConventions.AuthorMaxLength);

        builder.Property(assignment => assignment.EndReason)
            .HasEnumText();

        builder.HasAuditColumns();
    }
}
