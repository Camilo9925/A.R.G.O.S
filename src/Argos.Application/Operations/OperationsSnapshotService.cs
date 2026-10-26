using Argos.Application.Common;
using Argos.Application.Common.Abstractions;
using Argos.Application.Inventory.Equipments;
using Argos.Contracts.Operations;
using Argos.Domain.Inventory.Agents;
using Argos.Domain.Inventory.Types;
using Argos.Domain.Operations;
using Argos.Domain.Operations.Alerts;
using ContractEquipmentStatus = Argos.Contracts.Inventory.EquipmentStatus;
using DomainAgentRole = Argos.Domain.Inventory.Agents.AgentRole;
using DomainEquipmentStatus = Argos.Domain.Inventory.Equipments.EquipmentStatus;
using DomainMaintenanceNeed = Argos.Domain.Inventory.Equipments.MaintenanceNeed;

namespace Argos.Application.Operations;

/// <summary>
/// Retrato da Central (US-11 a US-13, US-15; RF-027 a RF-037; RN-13 a RN-16, RN-21, RN-27, RN-36; D-79, D-82; plan
/// 16.7). Uma consulta por grupo sobre o mesmo filtro; aqui se decidem o escopo dos grupos (RN-36), o que o perfil vê
/// (RN-27, A-38) e faz (RN-26, D-59), os indicadores (RN-14, RN-15), a gravidade (D-82) e a lista de alertas (RN-21).
/// </summary>
public sealed class OperationsSnapshotService(
    IOperationsQueries queries,
    EquipmentQueryService equipmentQueries,
    ICurrentActor actor,
    TimeProvider timeProvider)
{
    public async Task<OperationsSnapshot> GetAsync(OperationsSnapshotQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filter = await ToFilterAsync(query, cancellationToken);
        var scope = CentralFilterScope.From(filter.HasEquipmentFilters, filter.HasAgentFilters);
        var canSeeAgents = OperationsPermissions.CanSeeAgentPositions(actor);
        var canHandleAlerts = OperationsPermissions.CanHandleAlerts(actor);

        var equipmentRows = scope.IncludeEquipment
            ? await queries.ListEquipmentAsync(filter, cancellationToken)
            : [];
        var agentRows = scope.IncludeAgents && canSeeAgents
            ? await queries.ListAgentsWithPositionAsync(filter, cancellationToken)
            : [];
        var regionCounts = await queries.CountAgentsInActivityByRegionAsync(filter, cancellationToken);
        var alertRows = await queries.ListOpenAlertsAsync(filter, cancellationToken);
        var lastTelemetryAt = await queries.LastTelemetryAtAsync(cancellationToken);

        // RN-21: "atende ao filtro" usa só os filtros do usuário; com o grupo de equipamentos fora da visão (RN-36),
        // nenhum alerta de equipamento atende.
        var alerts = OperationsMapping.Ordered(alertRows)
            .Select(row => (Row: row, Listing: AlertListPolicy.Include(row.MatchesFilter && scope.IncludeEquipment, row.Severity)))
            .Where(item => item.Listing != AlertListing.Hidden)
            .ToArray();

        return new OperationsSnapshot
        {
            GeneratedAt = timeProvider.GetUtcNow(),
            LastTelemetryAt = lastTelemetryAt,
            Equipment = equipmentRows.Select(row => ToMarker(row, canSeeAgents)).ToArray(),
            AgentPositionsVisible = canSeeAgents,
            Agents = agentRows.Where(IsInCurrentActivity).Select(ToMarker).ToArray(),
            Indicators = Indicators(equipmentRows, regionCounts, alerts.Where(item => item.Listing == AlertListing.Listed).Select(item => item.Row)),
            Alerts = alerts
                .Select(item => OperationsMapping.ToSummary(
                    item.Row, item.Listing == AlertListing.ListedOutsideFilter, canHandleAlerts, canSeeAgents))
                .ToArray(),
        };
    }

    /// <summary>
    /// RN-13 e Art. VIII.2: o agente só ganha marcador em atividade e com posição medida no período de atividade atual,
    /// para não exibir a posição de um turno anterior como atual (Art. V.4).
    /// </summary>
    internal static bool IsInCurrentActivity(AgentMarkerRow row) =>
        AgentActivity.IsInActivity(row.Status) && row.PositionRecordedAt >= row.StatusChangedAt;

    private static OperationsIndicators Indicators(
        IReadOnlyList<EquipmentMarkerRow> equipment, IReadOnlyList<RegionAgentCountRow> regionCounts, IEnumerable<AlertRow> matchingAlerts)
    {
        var counts = Enum.GetValues<DomainEquipmentStatus>()
            .ToDictionary(status => status, status => equipment.Count(row => row.Status == status));
        var availability = AvailabilityCalculator.Calculate(counts);
        var alertCounts = matchingAlerts.GroupBy(row => row.Severity).ToDictionary(group => group.Key, group => group.Count());

        return new OperationsIndicators
        {
            TotalEquipment = availability.Total,
            Operational = availability.Operational,
            Unavailable = availability.Unavailable,
            AvailabilityPercent = availability.AvailabilityPercent,
            CountsByStatus = counts
                .Select(pair => new StatusCount { Status = EnumNames.Convert<ContractEquipmentStatus>(pair.Key), Count = pair.Value })
                .ToArray(),
            AgentsInActivity = regionCounts.Sum(region => region.AgentsInActivity),
            AgentsInActivityByRegion = regionCounts
                .Select(region => new RegionAgentCount
                {
                    RegionId = region.RegionId,
                    RegionName = region.RegionName,
                    AgentsInActivity = region.AgentsInActivity,
                })
                .ToArray(),
            OpenAlertsBySeverity = OperationsMapping.AllSeverities
                .Select(severity => new AlertSeverityCount
                {
                    Severity = EnumNames.Convert<Contracts.Operations.AlertSeverity>(severity),
                    Count = alertCounts.GetValueOrDefault(severity),
                })
                .ToArray(),
        };
    }

    private static EquipmentMarker ToMarker(EquipmentMarkerRow row, bool canSeeAgents)
    {
        var (position, source, recordedAt) = OperationsMapping.MapPosition(
            row.Category == EquipmentCategory.Fixed, row.CrewAssignable, row.MapPosition, row.LivePosition, row.LiveRecordedAt, canSeeAgents);

        return new EquipmentMarker
        {
            Id = row.Id,
            TypeId = row.TypeId,
            TypeName = row.TypeName,
            TypeIcon = row.TypeIcon,
            Category = EnumNames.Convert<Contracts.Inventory.EquipmentCategory>(row.Category),
            SerialNumber = row.SerialNumber,
            Status = EnumNames.Convert<ContractEquipmentStatus>(row.Status),
            SeverityRank = EquipmentStatusSeverity.Rank(row.Status),
            MaintenanceNeed = EnumNames.Convert<Contracts.Inventory.MaintenanceNeed>(row.MaintenanceNeed),
            OpenAlertSeverity = EnumNames.ConvertNullable<Contracts.Operations.AlertSeverity>(row.OpenAlertSeverity),
            Latitude = position.Latitude,
            Longitude = position.Longitude,
            PositionSource = source,
            PositionRecordedAt = recordedAt,
            OrganizationAcronym = row.OrganizationAcronym,
            NeighborhoodName = row.NeighborhoodName,
            RegionName = row.RegionName,
        };
    }

    private static AgentMarker ToMarker(AgentMarkerRow row) => new()
    {
        Id = row.Id,
        DisplayName = row.DisplayName,
        Role = EnumNames.Convert<Contracts.Inventory.AgentRole>(row.Role),
        OrganizationAcronym = row.OrganizationAcronym,
        Status = EnumNames.Convert<Contracts.Inventory.AgentStatus>(row.Status),
        SeverityRank = EquipmentStatusSeverity.AgentInActivityRank,
        Latitude = row.Position.Latitude,
        Longitude = row.Position.Longitude,
        PositionRecordedAt = row.PositionRecordedAt,
        AssignedEquipmentId = row.AssignedEquipmentId,
    };

    private async Task<OperationsFilter> ToFilterAsync(OperationsSnapshotQuery query, CancellationToken cancellationToken)
    {
        var (today, window) = await equipmentQueries.MaintenanceCalendarAsync(cancellationToken);

        return new OperationsFilter(
            query.TypeId.Distinct().ToArray(),
            query.Status.Distinct().Select(status => EnumNames.Convert<DomainEquipmentStatus>(status)).ToArray(),
            query.MaintenanceNeed.Distinct().Select(need => EnumNames.Convert<DomainMaintenanceNeed>(need)).ToArray(),
            query.AgentRole.Distinct().Select(role => EnumNames.Convert<DomainAgentRole>(role)).ToArray(),
            query.OrganizationId.Distinct().ToArray(),
            query.RegionId.Distinct().ToArray(),
            query.NeighborhoodId.Distinct().ToArray(),
            today,
            window);
    }
}
