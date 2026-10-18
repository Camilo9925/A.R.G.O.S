namespace Argos.Contracts.Inventory;

/// <summary>
/// Linha da lista de equipamentos (US-03; plan 15.14). Bairro e região de um equipamento móvel vêm da base (D-65).
/// </summary>
public sealed record EquipmentSummary
{
    public Guid Id { get; init; }

    public Guid TypeId { get; init; }

    public string TypeName { get; init; } = string.Empty;

    /// <summary>Chave de <see cref="EquipmentIcons"/>.</summary>
    public string TypeIcon { get; init; } = string.Empty;

    public EquipmentCategory Category { get; init; }

    public string SerialNumber { get; init; } = string.Empty;

    public string Brand { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public EquipmentStatus Status { get; init; }

    public MaintenanceNeed MaintenanceNeed { get; init; }

    public Guid OrganizationId { get; init; }

    public string OrganizationAcronym { get; init; } = string.Empty;

    public Guid NeighborhoodId { get; init; }

    public string NeighborhoodName { get; init; } = string.Empty;

    public Guid RegionId { get; init; }

    public string RegionName { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    /// <summary>Última alteração do cadastro, ou o próprio cadastro se nunca foi alterado (Art. V.4).</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
