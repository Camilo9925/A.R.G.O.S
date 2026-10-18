using Argos.Domain.Inventory.Types;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argos.Infrastructure.Persistence.Configurations.Inventory;

/// <summary>
/// Tabela <c>equipment_types</c> (plan 15.13; D-48), com as definições das características em
/// <c>equipment_type_attributes</c>. O repositório carrega o tipo sempre com elas, em ordem.
/// </summary>
internal sealed class EquipmentTypeConfiguration : IEntityTypeConfiguration<EquipmentType>
{
    private const int KeyMaxLength = 60;
    private const int IconMaxLength = 30;

    public void Configure(EntityTypeBuilder<EquipmentType> builder)
    {
        builder.ToTable("equipment_types");

        builder.HasKey(type => type.Id);
        builder.Property(type => type.Id)
            .ValueGeneratedNever();

        builder.Property(type => type.Key)
            .HasMaxLength(KeyMaxLength)
            .IsRequired();
        builder.HasIndex(type => type.Key)
            .IsUnique();

        builder.Property(type => type.Name)
            .HasMaxLength(EquipmentType.NameMaxLength)
            .IsRequired();

        builder.Property(type => type.NameKey)
            .HasMaxLength(EquipmentType.NameMaxLength)
            .IsRequired();
        builder.HasIndex(type => type.NameKey)
            .IsUnique();

        builder.Property(type => type.Category)
            .HasEnumText();

        builder.Property(type => type.Icon)
            .HasMaxLength(IconMaxLength)
            .IsRequired();

        builder.HasMany(type => type.Attributes)
            .WithOne()
            .HasForeignKey("EquipmentTypeId")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasDeactivationColumns().HasAuditColumns();
    }
}
