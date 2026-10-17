using Argos.Domain.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argos.Infrastructure.Persistence.Configurations.Access;

/// <summary>
/// Tabela <c>users</c> (plan 14.5). O login é único entre todos os usuários, inclusive os inativos.
/// </summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    private const int RoleMaxLength = 20;
    private const int AuthorMaxLength = 100;

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id)
            .ValueGeneratedNever();

        builder.Property(user => user.Name)
            .HasMaxLength(User.NameMaxLength)
            .IsRequired();

        builder.Property(user => user.Login)
            .HasMaxLength(User.LoginMaxLength)
            .IsRequired();
        builder.HasIndex(user => user.Login)
            .IsUnique();

        builder.Property(user => user.PasswordHash)
            .IsRequired();

        builder.Property(user => user.Role)
            .HasConversion<string>()
            .HasMaxLength(RoleMaxLength)
            .IsRequired();

        builder.Property(user => user.DeactivatedBy)
            .HasMaxLength(AuthorMaxLength);

        builder.Property(user => user.CreatedBy)
            .HasMaxLength(AuthorMaxLength)
            .IsRequired();

        builder.Property(user => user.UpdatedBy)
            .HasMaxLength(AuthorMaxLength);
    }
}
