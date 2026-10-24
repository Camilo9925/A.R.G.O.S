using Argos.Domain.Inventory.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argos.Infrastructure.Persistence.Configurations.Inventory;

/// <summary>
/// Tabela <c>agent_status_changes</c> (plan 15.2.9, 15.13): histórico append-only de situação do agente
/// (<c>IAppendOnly</c>, Art. V.1), na forma do registro do equipamento, sem justificativa nem manutenção (RN-12).
/// </summary>
internal sealed class AgentStatusChangeConfiguration : IEntityTypeConfiguration<AgentStatusChange>
{
    public void Configure(EntityTypeBuilder<AgentStatusChange> builder)
    {
        builder.ToTable("agent_status_changes");

        builder.HasKey(change => change.Id);
        builder.Property(change => change.Id)
            .ValueGeneratedNever();

        builder.Property(change => change.FromStatus)
            .HasEnumText();

        builder.Property(change => change.ToStatus)
            .HasEnumText();

        builder.Property(change => change.Origin)
            .HasEnumText();

        builder.Property(change => change.Author)
            .HasMaxLength(ColumnConventions.AuthorMaxLength)
            .IsRequired();

        builder.HasIndex(change => new { change.AgentId, change.OccurredAt });
    }
}
