using Argos.Domain.Inventory.Agents;
using Argos.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argos.Infrastructure.Persistence.Configurations.Operations;

/// <summary>
/// Tabela <c>agent_duty_shifts</c> (plan 16.9, 17.2; D-94): a escala de referência simulada, um turno por agente,
/// gravada uma vez pela carga (<c>IAppendOnly</c>, Art. V.1) e fora do agregado <c>Agent</c>, para não mexer na versão
/// dele (D-60). Sem índice em <c>shift</c>: só serviria à contagem por turno da US-21, que não é entregue (plan 18.5).
/// </summary>
internal sealed class AgentDutyShiftConfiguration : IEntityTypeConfiguration<AgentDutyShift>
{
    public void Configure(EntityTypeBuilder<AgentDutyShift> builder)
    {
        builder.ToTable("agent_duty_shifts");

        builder.HasKey(shift => shift.AgentId);
        builder.Property(shift => shift.AgentId)
            .ValueGeneratedNever();

        builder.HasOne<Agent>()
            .WithOne()
            .HasForeignKey<AgentDutyShift>(shift => shift.AgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(shift => shift.Shift)
            .HasEnumText();

        builder.Property(shift => shift.AssignedBy)
            .HasMaxLength(ColumnConventions.AuthorMaxLength)
            .IsRequired();
    }
}
