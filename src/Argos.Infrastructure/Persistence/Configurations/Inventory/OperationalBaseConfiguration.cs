using Argos.Domain.Inventory.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argos.Infrastructure.Persistence.Configurations.Inventory;

/// <summary>
/// Tabela <c>operational_bases</c> (plan 15.13; D-57): nome único por órgão e localização obrigatória nas colunas
/// <c>location_*</c>.
/// </summary>
internal sealed class OperationalBaseConfiguration : IEntityTypeConfiguration<OperationalBase>
{
    public void Configure(EntityTypeBuilder<OperationalBase> builder)
    {
        builder.ToTable("operational_bases");

        builder.HasKey(operationalBase => operationalBase.Id);
        builder.Property(operationalBase => operationalBase.Id)
            .ValueGeneratedNever();

        builder.Property(operationalBase => operationalBase.Name)
            .HasMaxLength(OperationalBase.NameMaxLength)
            .IsRequired();

        builder.Property(operationalBase => operationalBase.NameKey)
            .HasMaxLength(OperationalBase.NameMaxLength)
            .IsRequired();
        builder.HasIndex(operationalBase => new { operationalBase.OrganizationId, operationalBase.NameKey })
            .IsUnique();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(operationalBase => operationalBase.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ComplexProperty(operationalBase => operationalBase.Location)
            .HasLocationColumns();

        builder.HasDeactivationColumns().HasAuditColumns();
    }
}
