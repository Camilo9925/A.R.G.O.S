using Argos.Simulation.Geography;

namespace Argos.Simulation.Models;

// Modelos da telemetria simulada (plan 16.5): a Simulação não conhece os contratos da API; o Simulador traduz os
// recursos da descoberta para estes modelos e o lote montado para o TelemetryBatchRequest (T117). Os enums têm os
// mesmos nomes dos do Domínio e dos contratos.

/// <summary>Categoria do equipamento (RN-04), com os mesmos nomes do Domínio.</summary>
public enum SimulatedEquipmentCategory
{
    Fixed,
    Mobile,
}

/// <summary>Situação do agente (RN-12), com os mesmos nomes do Domínio.</summary>
public enum SimulatedAgentStatus
{
    OnDuty,
    Responding,
    OnBreak,
    OffDuty,
    OnLeave,
}

/// <summary>Turno da escala de referência (D-75), com os mesmos nomes do Domínio.</summary>
public enum SimulatedDutyShift
{
    Morning,
    Afternoon,
    Night,
}

/// <summary>Tipo do recurso de uma posição.</summary>
public enum SimulatedResourceKind
{
    Equipment,
    Agent,
}

/// <summary>Equipamento ativo da descoberta (plan 16.4).</summary>
/// <param name="Key">Marca + "/" + número de série (RN-01): a chave estável do movimento (D-74).</param>
/// <param name="Base">Posição da base; só móveis.</param>
/// <param name="BaseRegion">Nome da região da base; só móveis.</param>
public sealed record SimulatedTelemetryEquipment(
    Guid Id,
    string Key,
    string TypeKey,
    SimulatedEquipmentCategory Category,
    SimulatedEquipmentStatus Status,
    bool CrewAssignable,
    GeoCoordinate? Base,
    string? BaseRegion);

/// <summary>Agente ativo da descoberta (plan 16.4).</summary>
/// <param name="Key">Sigla do órgão + "/" + matrícula: a chave estável do movimento a pé (D-74).</param>
/// <param name="ScheduledOnDuty">Se o turno da escala cobre o instante atual, calculado pela API (D-94).</param>
public sealed record SimulatedTelemetryAgent(
    Guid Id,
    string Key,
    SimulatedAgentStatus Status,
    GeoCoordinate Base,
    string BaseRegion,
    Guid? AssignedEquipmentId,
    bool ScheduledOnDuty);

public sealed record SimulatedTelemetryResources(
    IReadOnlyList<SimulatedTelemetryEquipment> Equipment,
    IReadOnlyList<SimulatedTelemetryAgent> Agents);

/// <summary>
/// O que o ciclo anterior deixou aplicado (plan 16.5): a escala de cada agente cuja borda já foi aceita pela API e o
/// último sinal de vida enviado de cada fixo. O Simulador só adota o estado novo depois do <c>202</c> do lote.
/// </summary>
public sealed record TelemetryCycleState(
    IReadOnlyDictionary<Guid, bool> ScheduledOnDuty,
    IReadOnlyDictionary<Guid, DateTimeOffset> LastHeartbeatAt)
{
    /// <summary>Estado da subida: sem escala aplicada, o primeiro ciclo reconcilia (plan 16.5).</summary>
    public static TelemetryCycleState Empty { get; } =
        new(new Dictionary<Guid, bool>(), new Dictionary<Guid, DateTimeOffset>());
}

/// <summary>Velocidades por meio de deslocamento (plan 16.5; configuração técnica da simulação, Art. III.9).</summary>
public sealed record TelemetrySpeeds(double VehicleKmh, double MotorcycleKmh, double ScooterKmh, double OnFootKmh)
{
    /// <summary>Os padrões de plan 16.5.</summary>
    public static TelemetrySpeeds Default { get; } = new(30, 35, 12, 4);

    /// <summary>
    /// Velocidade do tipo móvel: moto para <c>traffic-agent-motorcycle</c>, patinete para <c>electric-scooter</c> e
    /// veículo para os demais, inclusive os tipos personalizados.
    /// </summary>
    public double ForType(string typeKey) => typeKey switch
    {
        "traffic-agent-motorcycle" => MotorcycleKmh,
        "electric-scooter" => ScooterKmh,
        _ => VehicleKmh,
    };
}

/// <summary>Configurações do lote (plan 16.5): a semente, o intervalo dos sinais de vida e as velocidades.</summary>
public sealed record TelemetryBatchSettings(int Seed, TimeSpan FixedHeartbeatInterval, TelemetrySpeeds Speeds);

/// <summary>Situação reportada de um agente (RF-023, RN-12).</summary>
public sealed record SimulatedStatusReport(Guid AgentId, SimulatedAgentStatus Status, DateTimeOffset ReportedAt);

/// <summary>Posição de um equipamento móvel ou de um agente em atividade.</summary>
public sealed record SimulatedPosition(
    SimulatedResourceKind ResourceKind, Guid ResourceId, double Latitude, double Longitude, DateTimeOffset RecordedAt);

/// <summary>Sinal de vida de um equipamento fixo.</summary>
public sealed record SimulatedFixedHeartbeat(Guid EquipmentId, DateTimeOffset RecordedAt);

/// <summary>Lote de um ciclo, com o mesmo instante na situação, nas posições e nos sinais de vida (plan 16.4).</summary>
public sealed record SimulatedTelemetryBatch(
    DateTimeOffset SentAt,
    IReadOnlyList<SimulatedStatusReport> StatusReports,
    IReadOnlyList<SimulatedPosition> Positions,
    IReadOnlyList<SimulatedFixedHeartbeat> Heartbeats);

/// <summary>O lote do ciclo e o estado a adotar se a API o aceitar.</summary>
public sealed record TelemetryCycle(SimulatedTelemetryBatch Batch, TelemetryCycleState NextState);
