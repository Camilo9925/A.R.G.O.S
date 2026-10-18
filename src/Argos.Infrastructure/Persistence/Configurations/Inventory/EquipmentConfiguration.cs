using Argos.Domain.Inventory.Equipments;
using Argos.Domain.Inventory.Support;
using Argos.Domain.Inventory.Types;
using Argos.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argos.Infrastructure.Persistence.Configurations.Inventory;

/// <summary>
/// Tabela <c>equipment</c> (plan 15.13). Marca + número de série únicos pelas chaves normalizadas (RN-01), localização
/// opcional nas colunas <c>location_*</c> (só fixos) ou base (só móveis), com <c>CHECK</c> de coerência; características
/// em <c>jsonb</c> (D-48); <c>search_text</c> como propriedade-sombra (D-53) e <c>xmin</c> como versão (D-60).
/// </summary>
internal sealed class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    /// <summary>Propriedade-sombra da busca sem acento, preenchida pelo <c>SearchTextInterceptor</c>.</summary>
    public const string SearchTextProperty = "SearchText";

    /// <summary>Propriedade-sombra mapeada para a coluna de sistema <c>xmin</c>: o token de concorrência (D-60).</summary>
    public const string VersionProperty = "Version";

    /// <summary>Nome do índice único da RN-01, que a unidade de trabalho reconhece na violação (409).</summary>
    public const string BrandAndSerialIndex = "ux_equipment_brand_key_serial_key";

    private const int SearchTextMaxLength = 600;

    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.ToTable("equipment", table => table.HasCheckConstraint(
            "ck_equipment_fixed_or_mobile",
            "(category = 'Fixed' AND location_neighborhood_id IS NOT NULL AND base_id IS NULL)"
            + " OR (category = 'Mobile' AND location_neighborhood_id IS NULL AND base_id IS NOT NULL)"));

        builder.HasKey(equipment => equipment.Id);
        builder.Property(equipment => equipment.Id)
            .ValueGeneratedNever();

        builder.Property(equipment => equipment.TypeId)
            .HasColumnName("equipment_type_id");
        builder.HasOne<EquipmentType>()
            .WithMany()
            .HasForeignKey(equipment => equipment.TypeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(equipment => equipment.TypeId);

        builder.Property(equipment => equipment.Category)
            .HasEnumText();

        builder.Property(equipment => equipment.SerialNumber)
            .HasMaxLength(Equipment.SerialNumberMaxLength)
            .IsRequired();

        builder.Property(equipment => equipment.SerialKey)
            .HasMaxLength(Equipment.SerialNumberMaxLength)
            .IsRequired();

        builder.Property(equipment => equipment.Brand)
            .HasMaxLength(Equipment.BrandMaxLength)
            .IsRequired();

        builder.Property(equipment => equipment.BrandKey)
            .HasMaxLength(Equipment.BrandMaxLength)
            .IsRequired();

        builder.HasIndex(equipment => new { equipment.BrandKey, equipment.SerialKey })
            .IsUnique()
            .HasDatabaseName(BrandAndSerialIndex);

        builder.Property(equipment => equipment.Model)
            .HasMaxLength(Equipment.ModelMaxLength)
            .IsRequired();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(equipment => equipment.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(equipment => equipment.OrganizationId);

        builder.ComplexProperty(equipment => equipment.Location)
            .HasLocationColumns();

        builder.HasOne<OperationalBase>()
            .WithMany()
            .HasForeignKey(equipment => equipment.BaseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(equipment => equipment.BaseId);

        builder.Property(equipment => equipment.Attributes)
            .HasAttributeJson()
            .IsRequired();

        builder.Property(equipment => equipment.Status)
            .HasEnumText();
        builder.HasIndex(equipment => equipment.Status);

        builder.Property<string>(SearchTextProperty)
            .HasMaxLength(SearchTextMaxLength)
            .IsRequired();

        builder.Property<uint>(VersionProperty)
            .HasColumnName("xmin")
            .IsRowVersion();

        builder.HasMany(equipment => equipment.Maintenances)
            .WithOne()
            .HasForeignKey(maintenance => maintenance.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(equipment => equipment.StatusChanges)
            .WithOne()
            .HasForeignKey(change => change.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(equipment => equipment.IsActive);

        builder.HasDeactivationColumns().HasAuditColumns();
    }
}
