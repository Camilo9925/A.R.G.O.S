using Argos.Contracts.Inventory;

namespace Argos.Contracts.Operations;

/// <summary>Posição atual recebida pela telemetria, com o instante da medição.</summary>
public sealed record PositionData
{
    public double Latitude { get; init; }

    public double Longitude { get; init; }

    public DateTimeOffset RecordedAt { get; init; }
}

/// <summary>
/// Detalhes de um equipamento pelo mapa (US-14, RF-034; D-81; plan 16.10): <c>GET /api/operations/equipment/{id}</c>.
/// </summary>
public sealed record OperationsEquipmentDetails
{
    /// <summary>Os detalhes do Inventário, com as situações de destino já filtradas pela permissão (D-59).</summary>
    public EquipmentDetails Equipment { get; init; } = new();

    /// <summary>
    /// Posição atual do móvel; nula sem telemetria e, em viatura ou moto, para quem não vê a posição de agentes (RN-27,
    /// A-38).
    /// </summary>
    public PositionData? CurrentPosition { get; init; }

    /// <summary>Leitura mais recente de qualquer tipo; nula com "Nenhuma comunicação recebida" (RF-024).</summary>
    public DateTimeOffset? LastCommunicationAt { get; init; }

    public IReadOnlyList<AlertSummary> OpenAlerts { get; init; } = [];
}

/// <summary>
/// Detalhes de um agente pelo mapa (US-14; RN-25, RN-27; plan 16.10): <c>GET /api/operations/agents/{id}</c>, só para
/// <c>agents.positions.view</c>. Sem nome completo nem matrícula: a US-14 pede o nome de exibição (Art. VIII.1).
/// </summary>
public sealed record OperationsAgentDetails
{
    public Guid Id { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public AgentRole Role { get; init; }

    public string OrganizationName { get; init; } = string.Empty;

    public string OrganizationAcronym { get; init; } = string.Empty;

    public AgentStatus Status { get; init; }

    public DateTimeOffset StatusChangedAt { get; init; }

    public string BaseName { get; init; } = string.Empty;

    public AssignedEquipmentData? AssignedEquipment { get; init; }

    /// <summary>Posição do período de atividade atual (Art. VIII.2, V.4); nula fora de atividade.</summary>
    public PositionData? Position { get; init; }

    public IReadOnlyList<AgentStatus> AllowedStatusChanges { get; init; } = [];
}
