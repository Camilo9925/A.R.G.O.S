namespace Argos.Contracts.Inventory;

/// <summary>
/// Detalhes de um equipamento (US-01 a US-05; plan 15.14). Traz prontos, do Domínio, a necessidade de manutenção, as
/// situações de destino permitidas ao usuário e o texto de exibição das características (D-59): a Web só exibe.
/// <see cref="Version"/> volta na edição, para a concorrência otimista (D-60).
/// </summary>
public sealed record EquipmentDetails
{
    public Guid Id { get; init; }

    public Guid TypeId { get; init; }

    public string TypeKey { get; init; } = string.Empty;

    public string TypeName { get; init; } = string.Empty;

    /// <summary>Chave de <see cref="EquipmentIcons"/>.</summary>
    public string TypeIcon { get; init; } = string.Empty;

    public EquipmentCategory Category { get; init; }

    public string SerialNumber { get; init; } = string.Empty;

    public string Brand { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public Guid OrganizationId { get; init; }

    public string OrganizationName { get; init; } = string.Empty;

    public string OrganizationAcronym { get; init; } = string.Empty;

    /// <summary>Localização do equipamento fixo; nula no móvel.</summary>
    public LocationData? Location { get; init; }

    /// <summary>Base do equipamento móvel; nula no fixo (RF-007).</summary>
    public Guid? BaseId { get; init; }

    public string? BaseName { get; init; }

    /// <summary>Bairro da localização (fixo) ou da base (móvel, D-65).</summary>
    public Guid NeighborhoodId { get; init; }

    public string NeighborhoodName { get; init; } = string.Empty;

    public Guid RegionId { get; init; }

    public string RegionName { get; init; } = string.Empty;

    /// <summary>Características informadas, na ordem das definições do tipo.</summary>
    public IReadOnlyList<AttributeValueData> Attributes { get; init; } = [];

    public EquipmentStatus Status { get; init; }

    public DateTimeOffset StatusChangedAt { get; init; }

    public MaintenanceNeed MaintenanceNeed { get; init; }

    public IReadOnlyList<MaintenanceData> OpenMaintenances { get; init; } = [];

    /// <summary>
    /// Situações para as quais o usuário pode mudar o equipamento agora (RN-05), com a justificativa exigida (RN-06),
    /// já filtradas pela permissão dele (D-47, D-59). Vazia para quem não altera situações.
    /// </summary>
    public IReadOnlyList<StatusChangeOptionData> AllowedStatusChanges { get; init; } = [];

    /// <summary>
    /// Agentes com vínculo aberto com o equipamento, por nome de exibição (US-07). Vazia nos tipos que não recebem
    /// vínculo e no cadastro inativo, porque a inativação encerra os vínculos (RN-35).
    /// </summary>
    public IReadOnlyList<AssignedAgentData> AssignedAgents { get; init; } = [];

    public bool IsActive { get; init; }

    /// <summary>Versão do cadastro lida, devolvida na edição (D-60).</summary>
    public uint Version { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public string CreatedBy { get; init; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; init; }

    public string? UpdatedBy { get; init; }
}

/// <summary>
/// Valor de uma característica: <see cref="Value"/> no texto invariante do contrato (o mesmo do cadastro, plan 15.14)
/// e <see cref="DisplayValue"/> pronto para exibir ("80 km/h", "Sim", "Débito, Pix").
/// </summary>
public sealed record AttributeValueData(string Key, string Label, string Value, string DisplayValue);

/// <summary>Situação de destino oferecida, com a indicação de justificativa obrigatória (RN-06; D-59).</summary>
public sealed record StatusChangeOptionData(EquipmentStatus Status, bool RequiresJustification);
