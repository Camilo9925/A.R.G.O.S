using Argos.Domain.Inventory.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argos.Infrastructure.Persistence.Configurations.Inventory;

/// <summary>Tabela <c>organizations</c> (plan 15.13): nome e sigla únicos pelas chaves normalizadas.</summary>
internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations");

        builder.HasKey(organization => organization.Id);
        builder.Property(organization => organization.Id)
            .ValueGeneratedNever();

        builder.Property(organization => organization.Name)
            .HasMaxLength(Organization.NameMaxLength)
            .IsRequired();

        builder.Property(organization => organization.NameKey)
            .HasMaxLength(Organization.NameMaxLength)
            .IsRequired();
        builder.HasIndex(organization => organization.NameKey)
            .IsUnique();

        builder.Property(organization => organization.Acronym)
            .HasMaxLength(Organization.AcronymMaxLength)
            .IsRequired();

        builder.Property(organization => organization.AcronymKey)
            .HasMaxLength(Organization.AcronymMaxLength)
            .IsRequired();
        builder.HasIndex(organization => organization.AcronymKey)
            .IsUnique();

        builder.HasDeactivationColumns().HasAuditColumns();
    }
}
