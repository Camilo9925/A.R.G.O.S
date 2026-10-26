using Argos.Contracts.Inventory;

namespace Argos.Contracts.Operations;

/// <summary>
/// Filtro da Central (US-13, RF-033; D-53, D-79; plan 16.7): <c>GET /api/operations/snapshot</c>. Cada filtro aceita
/// vários valores, que combinam por "ou"; filtros diferentes combinam por "e". Na query string, um filtro com vários
/// valores se repete, por isso os nomes ficam no singular, como na pesquisa do Inventário. O escopo dos grupos segue a
/// RN-36: tipo, situação e necessidade de manutenção valem só para equipamentos; função, só para agentes.
/// </summary>
public sealed record OperationsSnapshotQuery
{
    public List<Guid> TypeId { get; set; } = [];

    public List<EquipmentStatus> Status { get; set; } = [];

    public List<MaintenanceNeed> MaintenanceNeed { get; set; } = [];

    public List<AgentRole> AgentRole { get; set; } = [];

    public List<Guid> OrganizationId { get; set; } = [];

    public List<Guid> RegionId { get; set; } = [];

    public List<Guid> NeighborhoodId { get; set; } = [];
}

/// <summary>
/// Retrato da Central para um filtro (D-79; plan 16.7, 16.10): marcadores, indicadores e alertas, com tudo já decidido
/// pela API (escopo, gravidade, indicadores, ordem, <c>outsideFilter</c> e ações; D-59).
/// </summary>
public sealed record OperationsSnapshot
{
    public DateTimeOffset GeneratedAt { get; init; }

    /// <summary>Mudança mais recente dos estados atuais ("Última telemetria"); nula sem telemetria.</summary>
    public DateTimeOffset? LastTelemetryAt { get; init; }

    public IReadOnlyList<EquipmentMarker> Equipment { get; init; } = [];

    /// <summary>Se o usuário vê posição de agentes (RN-27); falso para o Gestor, que recebe só as contagens.</summary>
    public bool AgentPositionsVisible { get; init; }

    public IReadOnlyList<AgentMarker> Agents { get; init; } = [];

    public OperationsIndicators Indicators { get; init; } = new();

    public IReadOnlyList<AlertSummary> Alerts { get; init; } = [];
}

/// <summary>Marcador de equipamento ativo (US-11, RF-027 a RF-030).</summary>
public sealed record EquipmentMarker
{
    public Guid Id { get; init; }

    public Guid TypeId { get; init; }

    public string TypeName { get; init; } = string.Empty;

    /// <summary>Chave de <see cref="EquipmentIcons"/>.</summary>
    public string TypeIcon { get; init; } = string.Empty;

    public EquipmentCategory Category { get; init; }

    public string SerialNumber { get; init; } = string.Empty;

    public EquipmentStatus Status { get; init; }

    /// <summary>Gravidade da situação, 1 (Furtado) a 6 (Operacional), para o agrupamento (D-82).</summary>
    public int SeverityRank { get; init; }

    public MaintenanceNeed MaintenanceNeed { get; init; }

    /// <summary>Severidade do alerta aberto mais grave do equipamento, se houver.</summary>
    public AlertSeverity? OpenAlertSeverity { get; init; }

    public double Latitude { get; init; }

    public double Longitude { get; init; }

    public PositionSource PositionSource { get; init; }

    /// <summary>Instante da posição atual; nulo fora de <see cref="PositionSource.Live"/>.</summary>
    public DateTimeOffset? PositionRecordedAt { get; init; }

    public string OrganizationAcronym { get; init; } = string.Empty;

    /// <summary>Bairro da localização (fixo) ou da base (móvel, D-65).</summary>
    public string NeighborhoodName { get; init; } = string.Empty;

    public string RegionName { get; init; } = string.Empty;
}

/// <summary>Marcador de agente em atividade (RN-13), só para quem tem <c>agents.positions.view</c> (RN-27).</summary>
public sealed record AgentMarker
{
    public Guid Id { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public AgentRole Role { get; init; }

    public string OrganizationAcronym { get; init; } = string.Empty;

    public AgentStatus Status { get; init; }

    /// <summary>Agente em atividade conta como Operacional, 6 (D-82).</summary>
    public int SeverityRank { get; init; }

    public double Latitude { get; init; }

    public double Longitude { get; init; }

    public DateTimeOffset PositionRecordedAt { get; init; }

    public Guid? AssignedEquipmentId { get; init; }
}

/// <summary>Indicadores sobre os mesmos equipamentos filtrados (US-12, RF-032; RN-14 a RN-16; plan 16.7).</summary>
public sealed record OperationsIndicators
{
    public int TotalEquipment { get; init; }

    public int Operational { get; init; }

    /// <summary>Mau funcionamento + Em manutenção + Furtado (RN-15).</summary>
    public int Unavailable { get; init; }

    /// <summary>RN-14, arredondada a uma casa; nula sem equipamento que deveria estar operando.</summary>
    public decimal? AvailabilityPercent { get; init; }

    /// <summary>As seis situações, inclusive com zero.</summary>
    public IReadOnlyList<StatusCount> CountsByStatus { get; init; } = [];

    public int AgentsInActivity { get; init; }

    /// <summary>As cinco regiões, inclusive com zero, para todos os perfis (D-10).</summary>
    public IReadOnlyList<RegionAgentCount> AgentsInActivityByRegion { get; init; } = [];

    /// <summary>Alertas abertos que atendem ao filtro, nas quatro severidades.</summary>
    public IReadOnlyList<AlertSeverityCount> OpenAlertsBySeverity { get; init; } = [];
}

public sealed record StatusCount
{
    public EquipmentStatus Status { get; init; }

    public int Count { get; init; }
}

public sealed record AlertSeverityCount
{
    public AlertSeverity Severity { get; init; }

    public int Count { get; init; }
}

public sealed record RegionAgentCount
{
    public Guid RegionId { get; init; }

    public string RegionName { get; init; } = string.Empty;

    public int AgentsInActivity { get; init; }
}
