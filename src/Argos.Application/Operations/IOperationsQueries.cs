using Argos.Domain.Inventory.Agents;
using Argos.Domain.Inventory.Equipments;
using Argos.Domain.Inventory.Types;
using Argos.Domain.Operations.Alerts;
using Argos.Domain.Telemetry;

namespace Argos.Application.Operations;

/// <summary>
/// Consultas da Central (plan 16.7, 16.8): projeção direta das linhas, sem regra de negócio além dos predicados de
/// filtro, que são os mesmos da pesquisa do Inventário (<c>EquipmentSearch.WhereFilters</c>; RN-16, D-79). Escopo dos
/// grupos (RN-36), visibilidade por perfil (RN-27), indicadores (RN-14, RN-15), lista de alertas (RN-21) e o período de
/// atividade do marcador de agente (Art. VIII.2) ficam na Aplicação, sobre estas linhas.
/// </summary>
public interface IOperationsQueries
{
    /// <summary>
    /// Equipamentos <b>ativos</b> que atendem aos filtros de tipo, situação, necessidade de manutenção, órgão, região e
    /// bairro (região e bairro do móvel pela base, D-65).
    /// </summary>
    Task<IReadOnlyList<EquipmentMarkerRow>> ListEquipmentAsync(OperationsFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Agentes <b>ativos</b> com posição atual que atendem aos filtros de função, órgão, região e bairro (pela base,
    /// D-65), com a situação e o instante da última mudança, para a Aplicação aplicar a RN-13 e o período de atividade.
    /// </summary>
    Task<IReadOnlyList<AgentMarkerRow>> ListAgentsWithPositionAsync(
        OperationsFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Agentes ativos em atividade (RN-13) por região da base, com os filtros de função, órgão, região e bairro; as
    /// cinco regiões, inclusive com zero (D-10), em ordem de nome.
    /// </summary>
    Task<IReadOnlyList<RegionAgentCountRow>> CountAgentsInActivityByRegionAsync(
        OperationsFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Alertas abertos de todos os recursos, inclusive de cadastro inativo (D-77), com <see cref="AlertRow.MatchesFilter"/>
    /// calculado no banco <b>só pelos filtros de equipamento</b> do usuário (tipo, situação, necessidade de manutenção,
    /// órgão, região e bairro), com os mesmos predicados da pesquisa do Inventário, <b>sem o filtro de ativos</b> e
    /// ignorando a função do agente, porque todo alerta da entrega é de equipamento (plan 16.7). O escopo da RN-36 é
    /// aplicado depois, pela Aplicação.
    /// </summary>
    Task<IReadOnlyList<AlertRow>> ListOpenAlertsAsync(OperationsFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Alertas abertos de um recurso, com <see cref="AlertRow.MatchesFilter"/> verdadeiro.</summary>
    Task<IReadOnlyList<AlertRow>> ListOpenAlertsOfAsync(
        ResourceKind resourceKind, Guid resourceId, CancellationToken cancellationToken = default);

    /// <summary>Um alerta em qualquer estado, com <see cref="AlertRow.MatchesFilter"/> verdadeiro; nulo se não existe.</summary>
    Task<AlertRow?> FindAlertAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Maior <c>UpdatedAt</c> dos estados atuais ("Última telemetria", Art. V.4); nulo sem telemetria.</summary>
    Task<DateTimeOffset?> LastTelemetryAtAsync(CancellationToken cancellationToken = default);

    /// <summary>Estado atual de um equipamento existente, com o tipo; nulo se o equipamento não existe.</summary>
    Task<EquipmentLiveRow?> FindEquipmentLiveAsync(Guid equipmentId, CancellationToken cancellationToken = default);

    /// <summary>Estado atual de um agente; nulo sem telemetria.</summary>
    Task<LiveStateRow?> FindAgentLiveAsync(Guid agentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Filtro da Central nos tipos do Domínio (plan 16.7). "Ou" dentro do mesmo filtro, "e" entre filtros (D-53).
/// <see cref="Today"/> e <see cref="UpcomingWindowDays"/> servem ao filtro de necessidade de manutenção (RN-10, D-54).
/// </summary>
public sealed record OperationsFilter(
    IReadOnlyList<Guid> TypeIds,
    IReadOnlyList<EquipmentStatus> Statuses,
    IReadOnlyList<MaintenanceNeed> MaintenanceNeeds,
    IReadOnlyList<AgentRole> AgentRoles,
    IReadOnlyList<Guid> OrganizationIds,
    IReadOnlyList<Guid> RegionIds,
    IReadOnlyList<Guid> NeighborhoodIds,
    DateOnly Today,
    int UpcomingWindowDays)
{
    /// <summary>Filtros só de equipamento: tipo, situação e necessidade de manutenção (RN-36).</summary>
    public bool HasEquipmentFilters => TypeIds.Count > 0 || Statuses.Count > 0 || MaintenanceNeeds.Count > 0;

    /// <summary>Filtro só de agente: função (RN-36).</summary>
    public bool HasAgentFilters => AgentRoles.Count > 0;
}

/// <summary>Coordenada de uma linha da consulta.</summary>
public sealed record GeoPosition(double Latitude, double Longitude);

/// <summary>Linha de equipamento ativo para o mapa e os indicadores.</summary>
/// <param name="MapPosition">Fixo: localização cadastral; móvel: posição da base.</param>
/// <param name="LivePosition">Posição atual recebida pela telemetria, se houver (só móveis).</param>
/// <param name="OpenAlertSeverity">Severidade do alerta aberto mais grave do equipamento.</param>
public sealed record EquipmentMarkerRow(
    Guid Id,
    Guid TypeId,
    string TypeName,
    string TypeIcon,
    EquipmentCategory Category,
    bool CrewAssignable,
    string SerialNumber,
    EquipmentStatus Status,
    MaintenanceNeed MaintenanceNeed,
    AlertSeverity? OpenAlertSeverity,
    GeoPosition MapPosition,
    GeoPosition? LivePosition,
    DateTimeOffset? LiveRecordedAt,
    string OrganizationAcronym,
    string NeighborhoodName,
    string RegionName);

/// <summary>Linha de agente ativo com posição atual.</summary>
public sealed record AgentMarkerRow(
    Guid Id,
    string DisplayName,
    AgentRole Role,
    string OrganizationAcronym,
    AgentStatus Status,
    DateTimeOffset StatusChangedAt,
    GeoPosition Position,
    DateTimeOffset PositionRecordedAt,
    Guid? AssignedEquipmentId);

public sealed record RegionAgentCountRow(Guid RegionId, string RegionName, int AgentsInActivity);

/// <summary>Linha de alerta com o recurso, como aparece no mapa (RF-037).</summary>
/// <param name="MapPosition">Fixo: localização cadastral; móvel: posição da base.</param>
/// <param name="LivePosition">Posição atual do móvel, se houver.</param>
public sealed record AlertRow(
    Guid Id,
    AlertCause Cause,
    AlertSeverity Severity,
    AlertState State,
    ResourceKind ResourceKind,
    Guid ResourceId,
    string TypeName,
    string SerialNumber,
    bool CrewAssignable,
    bool ResourceIsActive,
    string NeighborhoodName,
    string RegionName,
    GeoPosition MapPosition,
    GeoPosition? LivePosition,
    DateTimeOffset OpenedAt,
    DateTimeOffset LastOccurredAt,
    int OccurrenceCount,
    DateTimeOffset? AcknowledgedAt,
    string? AcknowledgedBy,
    bool MatchesFilter);

/// <summary>Estado atual de um recurso (D-70).</summary>
public sealed record LiveStateRow(GeoPosition? Position, DateTimeOffset? PositionRecordedAt, DateTimeOffset LastCommunicationAt);

/// <summary>Estado atual de um equipamento, com o que a RN-27 precisa saber do tipo (A-38).</summary>
public sealed record EquipmentLiveRow(bool CrewAssignable, LiveStateRow? Live);
