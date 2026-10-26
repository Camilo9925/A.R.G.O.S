using Argos.Contracts.Inventory;
using Argos.Contracts.Operations;

namespace Argos.Contracts.Telemetry;

/// <summary>
/// Recursos ativos que o PROMETHEUS lê a cada ciclo (D-72; plan 16.4): <c>GET /api/telemetry/resources</c>.
/// </summary>
public sealed record TelemetryResourceList
{
    public IReadOnlyList<TelemetryEquipmentResource> Equipment { get; init; } = [];

    public IReadOnlyList<TelemetryAgentResource> Agents { get; init; } = [];
}

/// <summary>Equipamento ativo para a telemetria (plan 16.4).</summary>
public sealed record TelemetryEquipmentResource
{
    public Guid Id { get; init; }

    /// <summary>Marca + "/" + número de série, única pela RN-01.</summary>
    public string Key { get; init; } = string.Empty;

    public string TypeKey { get; init; } = string.Empty;

    public EquipmentCategory Category { get; init; }

    public EquipmentStatus Status { get; init; }

    /// <summary>Viatura ou moto que recebe tripulação (US-07).</summary>
    public bool CrewAssignable { get; init; }

    /// <summary>Posição da base; só móveis.</summary>
    public double? BaseLatitude { get; init; }

    public double? BaseLongitude { get; init; }

    /// <summary>Nome da região da base; só móveis (D-65).</summary>
    public string? BaseRegion { get; init; }
}

/// <summary>Agente ativo para a telemetria (plan 16.4).</summary>
public sealed record TelemetryAgentResource
{
    public Guid Id { get; init; }

    /// <summary>Sigla do órgão + "/" + matrícula.</summary>
    public string Key { get; init; } = string.Empty;

    public AgentRole Role { get; init; }

    public AgentStatus Status { get; init; }

    public double BaseLatitude { get; init; }

    public double BaseLongitude { get; init; }

    public string BaseRegion { get; init; } = string.Empty;

    /// <summary>Equipamento do vínculo aberto, se houver (US-07).</summary>
    public Guid? AssignedEquipmentId { get; init; }

    /// <summary>Turno da escala de referência; nulo para agente sem escala (D-94).</summary>
    public DutyShift? Shift { get; init; }

    /// <summary>
    /// Se o turno cobre o instante atual no fuso do município, calculado pelo Domínio (D-75 revista); falso sem escala.
    /// </summary>
    public bool ScheduledOnDuty { get; init; }
}
