using Argos.Domain.Inventory.Agents;
using Argos.Domain.Inventory.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argos.Infrastructure.Persistence.Configurations.Inventory;

/// <summary>
/// Tabela <c>agents</c> (plan 15.2.9, 15.13). Matrícula única por órgão pela chave normalizada, inclusive entre
/// inativos (US-06); <c>search_text</c> como propriedade-sombra (D-53) e <c>xmin</c> como versão (D-60), como no
/// equipamento. Só os dados mínimos da operação (RN-25).
/// </summary>
internal sealed class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    /// <summary>Propriedade-sombra da busca sem acento, preenchida pelo <c>SearchTextInterceptor</c>.</summary>
    public const string SearchTextProperty = "SearchText";

    /// <summary>Propriedade-sombra mapeada para a coluna de sistema <c>xmin</c>: o token de concorrência (D-60).</summary>
    public const string VersionProperty = "Version";

    /// <summary>Nome do índice único da matrícula no órgão, que a unidade de trabalho reconhece na violação (409).</summary>
    public const string RegistrationIndex = "ux_agents_organization_id_registration_key";

    private const int SearchTextMaxLength = 250;

    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.ToTable("agents");

        builder.HasKey(agent => agent.Id);
        builder.Property(agent => agent.Id)
            .ValueGeneratedNever();

        builder.Property(agent => agent.FullName)
            .HasMaxLength(Agent.FullNameMaxLength)
            .IsRequired();

        builder.Property(agent => agent.DisplayName)
            .HasMaxLength(Agent.DisplayNameMaxLength)
            .IsRequired();

        builder.Property(agent => agent.RegistrationNumber)
            .HasMaxLength(Agent.RegistrationNumberMaxLength)
            .IsRequired();

        builder.Property(agent => agent.RegistrationKey)
            .HasMaxLength(Agent.RegistrationNumberMaxLength)
            .IsRequired();

        builder.Property(agent => agent.Role)
            .HasEnumText();
        builder.HasIndex(agent => agent.Role);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(agent => agent.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(agent => new { agent.OrganizationId, agent.RegistrationKey })
            .IsUnique()
            .HasDatabaseName(RegistrationIndex);

        builder.Property(agent => agent.Rank)
            .HasMaxLength(Agent.RankMaxLength);

        builder.HasOne<OperationalBase>()
            .WithMany()
            .HasForeignKey(agent => agent.BaseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(agent => agent.BaseId);

        builder.Property(agent => agent.Status)
            .HasEnumText();
        builder.HasIndex(agent => agent.Status);

        builder.Property<string>(SearchTextProperty)
            .HasMaxLength(SearchTextMaxLength)
            .IsRequired();

        builder.Property<uint>(VersionProperty)
            .HasColumnName("xmin")
            .IsRowVersion();

        builder.HasMany(agent => agent.StatusChanges)
            .WithOne()
            .HasForeignKey(change => change.AgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(agent => agent.IsActive);

        builder.HasDeactivationColumns().HasAuditColumns();
    }
}
