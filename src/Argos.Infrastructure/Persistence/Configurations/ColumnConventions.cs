using Argos.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argos.Infrastructure.Persistence.Configurations;

/// <summary>Colunas que se repetem nas tabelas: auditoria, inativação e enums gravados como texto (plan 5.4, 15.13).</summary>
internal static class ColumnConventions
{
    public const int AuthorMaxLength = 100;
    public const int EnumMaxLength = 30;

    /// <summary>Autor da criação obrigatório e da última alteração opcional, preenchidos pelo interceptador (plan 5.4).</summary>
    public static EntityTypeBuilder<TEntity> HasAuditColumns<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class, IAuditable
    {
        builder.Property(nameof(IAuditable.CreatedBy))
            .HasMaxLength(AuthorMaxLength)
            .IsRequired();

        builder.Property(nameof(IAuditable.UpdatedBy))
            .HasMaxLength(AuthorMaxLength);

        return builder;
    }

    /// <summary>Autor da inativação (plan 5.4).</summary>
    public static EntityTypeBuilder<TEntity> HasDeactivationColumns<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class, IDeactivatable
    {
        builder.Property(nameof(IDeactivatable.DeactivatedBy))
            .HasMaxLength(AuthorMaxLength);

        return builder;
    }

    /// <summary>Enum gravado pelo nome do item, como <c>users.role</c> (plan 15.13).</summary>
    public static PropertyBuilder<TEnum> HasEnumText<TEnum>(this PropertyBuilder<TEnum> property) =>
        property
            .HasConversion<string>()
            .HasMaxLength(EnumMaxLength);
}
