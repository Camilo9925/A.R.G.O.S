namespace Argos.Contracts.Inventory;

/// <summary>
/// Detalhes de um agente (US-06, US-07, US-09, RF-013 a RF-016; plan 15.14), com a base, a região (D-65) e o
/// equipamento vinculado (US-07). <see cref="AllowedStatusChanges"/> vem pronta da Aplicação (RN-12, D-59): a Web só
/// exibe. <see cref="Version"/> volta na edição, para a concorrência otimista (D-60).
/// </summary>
public sealed record AgentDetails
{
    public Guid Id { get; init; }

    public string FullName { get; init; } = string.Empty;

    /// <summary>Nome de exibição ou de guerra (US-14).</summary>
    public string DisplayName { get; init; } = string.Empty;

    public string RegistrationNumber { get; init; } = string.Empty;

    public AgentRole Role { get; init; }

    public string? Rank { get; init; }

    public Guid OrganizationId { get; init; }

    public string OrganizationName { get; init; } = string.Empty;

    public string OrganizationAcronym { get; init; } = string.Empty;

    public Guid BaseId { get; init; }

    public string BaseName { get; init; } = string.Empty;

    /// <summary>Bairro da base (D-65).</summary>
    public Guid NeighborhoodId { get; init; }

    public string NeighborhoodName { get; init; } = string.Empty;

    public Guid RegionId { get; init; }

    public string RegionName { get; init; } = string.Empty;

    public AgentStatus Status { get; init; }

    /// <summary>Instante da última mudança de situação, ou do cadastro (Art. V.4).</summary>
    public DateTimeOffset StatusChangedAt { get; init; }

    /// <summary>
    /// Situações para as quais o usuário pode mudar o agente agora: qualquer uma diferente da atual (RN-12), vazia para
    /// cadastro inativo (RN-35) e para quem não altera situações (D-59).
    /// </summary>
    public IReadOnlyList<AgentStatus> AllowedStatusChanges { get; init; } = [];

    /// <summary>Vínculo aberto com equipamento móvel, se houver (US-07).</summary>
    public AssignedEquipmentData? AssignedEquipment { get; init; }

    public bool IsActive { get; init; }

    /// <summary>Versão do cadastro lida, devolvida na edição (D-60).</summary>
    public uint Version { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public string CreatedBy { get; init; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; init; }

    public string? UpdatedBy { get; init; }
}

/// <summary>Equipamento móvel do vínculo aberto de um agente, nos detalhes do agente (US-07).</summary>
public sealed record AssignedEquipmentData
{
    public Guid AssignmentId { get; init; }

    public Guid EquipmentId { get; init; }

    public string TypeName { get; init; } = string.Empty;

    /// <summary>Chave de <see cref="EquipmentIcons"/>.</summary>
    public string TypeIcon { get; init; } = string.Empty;

    public string SerialNumber { get; init; } = string.Empty;

    public string Brand { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public EquipmentStatus Status { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public string StartedBy { get; init; } = string.Empty;
}

/// <summary>Agente com vínculo aberto, nos detalhes de viatura e moto (US-07).</summary>
public sealed record AssignedAgentData
{
    public Guid AssignmentId { get; init; }

    public Guid AgentId { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string RegistrationNumber { get; init; } = string.Empty;

    public AgentRole Role { get; init; }

    public AgentStatus Status { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public string StartedBy { get; init; } = string.Empty;
}

/// <summary>
/// Um vínculo entre agente e equipamento móvel, com início e fim (RF-016, US-07; D-62; plan 15.2.10): a resposta do
/// vínculo novo e do encerramento. Início e fim são gravados uma única vez.
/// </summary>
public sealed record AssignmentData
{
    public Guid Id { get; init; }

    public Guid AgentId { get; init; }

    public Guid EquipmentId { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public string StartedBy { get; init; } = string.Empty;

    public DateTimeOffset? EndedAt { get; init; }

    public string? EndedBy { get; init; }

    public AssignmentEndReason? EndReason { get; init; }
}
