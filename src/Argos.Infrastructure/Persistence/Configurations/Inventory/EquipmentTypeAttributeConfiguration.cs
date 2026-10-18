using Argos.Domain.Inventory.Types;
using Argos.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argos.Infrastructure.Persistence.Configurations.Inventory;

/// <summary>
/// Definição de característica: entidade filha do tipo, sem identidade no Domínio. A chave <c>id</c> e a referência ao
/// tipo são propriedades-sombra; a chave é única dentro do tipo.
/// </summary>
internal sealed class EquipmentTypeAttributeConfiguration : IEntityTypeConfiguration<EquipmentTypeAttribute>
{
    private const string IdProperty = "Id";
    private const string TypeIdProperty = "EquipmentTypeId";
    private const int KeyMaxLength = 50;
    private const int LabelMaxLength = 60;
    private const int UnitMaxLength = 20;
    private const int OptionKeyMaxLength = 60;

    public void Configure(EntityTypeBuilder<EquipmentTypeAttribute> builder)
    {
        builder.ToTable("equipment_type_attributes");

        builder.Property<Guid>(IdProperty);
        builder.HasKey(IdProperty);

        builder.Property<Guid>(TypeIdProperty);
        builder.HasIndex(TypeIdProperty, nameof(EquipmentTypeAttribute.Key))
            .IsUnique();

        builder.Property(attribute => attribute.Key)
            .HasMaxLength(KeyMaxLength)
            .IsRequired();

        builder.Property(attribute => attribute.Label)
            .HasMaxLength(LabelMaxLength)
            .IsRequired();

        builder.Property(attribute => attribute.DataType)
            .HasEnumText();

        builder.ComplexProperty(attribute => attribute.RequiredWhen, condition =>
        {
            condition.Property(value => value.AttributeKey)
                .HasColumnName("required_when_key")
                .HasMaxLength(KeyMaxLength);
            condition.Property(value => value.OptionKey)
                .HasColumnName("required_when_value")
                .HasMaxLength(OptionKeyMaxLength);
        });

        builder.Property(attribute => attribute.Unit)
            .HasMaxLength(UnitMaxLength);

        builder.Property(attribute => attribute.Options)
            .HasOptionsJson()
            .IsRequired();
    }
}
