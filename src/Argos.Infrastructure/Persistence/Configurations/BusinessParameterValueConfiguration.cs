using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argos.Infrastructure.Persistence.Configurations;

/// <summary>Tabela <c>business_parameters</c> (plan 15.10, 15.13): a chave estável do catálogo é a chave primária.</summary>
internal sealed class BusinessParameterValueConfiguration : IEntityTypeConfiguration<BusinessParameterValue>
{
    public void Configure(EntityTypeBuilder<BusinessParameterValue> builder)
    {
        builder.ToTable("business_parameters");

        builder.HasKey(parameter => parameter.Key);
        builder.Property(parameter => parameter.Key)
            .HasMaxLength(BusinessParameterValue.KeyMaxLength)
            .ValueGeneratedNever();

        builder.Property(parameter => parameter.Value)
            .HasColumnType("numeric");

        builder.HasAuditColumns();
    }
}
