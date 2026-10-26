using Argos.Contracts.Operations;

namespace Argos.Contracts.Realtime;

/// <summary>Posição de um equipamento móvel numa mensagem de tempo real.</summary>
public sealed record EquipmentPositionUpdate
{
    public Guid EquipmentId { get; init; }

    public double Latitude { get; init; }

    public double Longitude { get; init; }

    public DateTimeOffset RecordedAt { get; init; }
}

/// <summary>Posição de um agente em atividade numa mensagem de tempo real.</summary>
public sealed record AgentPositionUpdate
{
    public Guid AgentId { get; init; }

    public double Latitude { get; init; }

    public double Longitude { get; init; }

    public DateTimeOffset RecordedAt { get; init; }
}

/// <summary>
/// Lote de telemetria processado (D-80; plan 16.11), para todos, mesmo sem posições: alimenta "Última telemetria".
/// Viaturas e motos (<c>CrewAssignable</c>) ficam de fora, porque revelariam a posição dos agentes (RN-27, A-38).
/// </summary>
public sealed record TelemetryBatchProcessed
{
    public DateTimeOffset ReceivedAt { get; init; }

    public IReadOnlyList<EquipmentPositionUpdate> EquipmentPositions { get; init; } = [];
}

/// <summary>
/// Posições de agentes e das viaturas e motos do lote (RN-27, A-38; plan 16.11), só para quem tem
/// <c>agents.positions.view</c>.
/// </summary>
public sealed record AgentPositionsUpdated
{
    public DateTimeOffset ReceivedAt { get; init; }

    public IReadOnlyList<AgentPositionUpdate> Positions { get; init; } = [];

    public IReadOnlyList<EquipmentPositionUpdate> CrewEquipmentPositions { get; init; } = [];
}

/// <summary>Aviso de que algo que a Central mostra mudou (D-80; plan 16.11): a Web busca um novo retrato.</summary>
public sealed record OperationsChanged
{
    public OperationsChangeKind Kind { get; init; }

    public DateTimeOffset OccurredAt { get; init; }
}
