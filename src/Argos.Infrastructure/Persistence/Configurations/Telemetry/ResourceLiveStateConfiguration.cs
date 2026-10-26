using Argos.Domain.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argos.Infrastructure.Persistence.Configurations.Telemetry;

/// <summary>
/// Tabela <c>resource_live_states</c> (plan 16.9; D-70): uma linha por recurso, chave (tipo, id), sem chave estrangeira,
/// porque o mesmo campo serve a equipamento e agente (plan 16.2). Fica fora de <c>equipment</c> e <c>agents</c> para a
/// posição não mudar a versão (<c>xmin</c>) deles (D-60).
/// </summary>
internal sealed class ResourceLiveStateConfiguration : IEntityTypeConfiguration<ResourceLiveState>
{
    public void Configure(EntityTypeBuilder<ResourceLiveState> builder)
    {
        builder.ToTable("resource_live_states");

        builder.HasKey(state => new { state.ResourceKind, state.ResourceId });

        builder.Property(state => state.ResourceKind)
            .HasEnumText();
    }
}
