using Argos.Application.Operations;
using Argos.Contracts.Common;
using Argos.Domain.Inventory.Agents;
using Argos.Domain.Operations.Alerts;
using Argos.Domain.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace Argos.Infrastructure.Persistence.Queries;

/// <summary>
/// Consultas da Central (plan 16.7, 16.8), sem rastreamento e com projeção direta para as linhas da Aplicação. Os
/// equipamentos usam as linhas e os predicados da pesquisa do Inventário (<see cref="EquipmentSearch.Rows"/> e
/// <see cref="EquipmentSearch.WhereFilters"/>), para a Central e o Inventário contarem igual (RN-16, D-79). Região e
/// bairro de móveis e agentes pela base (D-65). Nenhuma regra de negócio além dos predicados: escopo (RN-36),
/// visibilidade (RN-27), indicadores e lista de alertas (RN-21) ficam na Aplicação.
/// </summary>
internal sealed class OperationsQueries(ArgosDbContext dbContext) : IOperationsQueries
{
    public async Task<IReadOnlyList<EquipmentMarkerRow>> ListEquipmentAsync(
        OperationsFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var rows = FilteredEquipment(EquipmentSearch.WhereActivity(EquipmentRows(filter), ActivityFilter.Active), filter);
        var lives = dbContext.ResourceLiveStates.AsNoTracking().Where(state => state.ResourceKind == ResourceKind.Equipment);

        var items = await (
                from row in rows
                join live in lives on row.Id equals live.ResourceId into liveStates
                from live in liveStates.DefaultIfEmpty()
                orderby row.Id
                select new
                {
                    Row = row,
                    LiveLatitude = live == null ? null : live.Latitude,
                    LiveLongitude = live == null ? null : live.Longitude,
                    LiveRecordedAt = live == null ? null : live.PositionRecordedAt,
                })
            .ToListAsync(cancellationToken);

        var severities = await OpenAlertSeveritiesAsync(cancellationToken);

        return items
            .Select(item => new EquipmentMarkerRow(
                item.Row.Id,
                item.Row.TypeId,
                item.Row.TypeName,
                item.Row.TypeIcon,
                item.Row.Category,
                item.Row.CrewAssignable,
                item.Row.SerialNumber,
                item.Row.Status,
                item.Row.MaintenanceNeed,
                severities.GetValueOrDefault(item.Row.Id),
                new GeoPosition(item.Row.MapLatitude, item.Row.MapLongitude),
                Live(item.LiveLatitude, item.LiveLongitude),
                item.LiveRecordedAt,
                item.Row.OrganizationAcronym,
                item.Row.NeighborhoodName,
                item.Row.RegionName))
            .ToArray();
    }

    public async Task<IReadOnlyList<AgentMarkerRow>> ListAgentsWithPositionAsync(
        OperationsFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var lives = dbContext.ResourceLiveStates.AsNoTracking()
            .Where(state => state.ResourceKind == ResourceKind.Agent && state.PositionRecordedAt != null);

        var items = await (
                from row in FilteredAgents(filter)
                join live in lives on row.Id equals live.ResourceId
                orderby row.Id
                select new
                {
                    Row = row,
                    Latitude = live.Latitude!.Value,
                    Longitude = live.Longitude!.Value,
                    RecordedAt = live.PositionRecordedAt!.Value,
                    StatusChangedAt = dbContext.AgentStatusChanges
                        .Where(change => change.AgentId == row.Id)
                        .Max(change => (DateTimeOffset?)change.OccurredAt),
                    AssignedEquipmentId = dbContext.AgentAssignments
                        .Where(assignment => assignment.AgentId == row.Id && assignment.EndedAt == null)
                        .Select(assignment => (Guid?)assignment.EquipmentId)
                        .FirstOrDefault(),
                })
            .ToListAsync(cancellationToken);

        return items
            .Select(item => new AgentMarkerRow(
                item.Row.Id,
                item.Row.DisplayName,
                item.Row.Role,
                item.Row.OrganizationAcronym,
                item.Row.Status,
                item.StatusChangedAt ?? item.Row.CreatedAt,
                new GeoPosition(item.Latitude, item.Longitude),
                item.RecordedAt,
                item.AssignedEquipmentId))
            .ToArray();
    }

    public async Task<IReadOnlyList<RegionAgentCountRow>> CountAgentsInActivityByRegionAsync(
        OperationsFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        // RN-13: Em serviço ou Em atendimento, lidos da regra do Domínio.
        var inActivity = Enum.GetValues<AgentStatus>().Where(AgentActivity.IsInActivity).ToArray();

        var counts = await FilteredAgents(filter)
            .Where(row => inActivity.Contains(row.Status))
            .GroupBy(row => row.RegionId)
            .Select(group => new { RegionId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.RegionId, item => item.Count, cancellationToken);

        var regions = await dbContext.Regions
            .AsNoTracking()
            .OrderBy(region => region.Name)
            .ThenBy(region => region.Id)
            .Select(region => new { region.Id, region.Name })
            .ToListAsync(cancellationToken);

        return regions
            .Select(region => new RegionAgentCountRow(region.Id, region.Name, counts.GetValueOrDefault(region.Id)))
            .ToArray();
    }

    /// <summary>
    /// <see cref="AlertRow.MatchesFilter"/> pelos predicados de equipamento da pesquisa (tipo, situação, necessidade,
    /// órgão, região e bairro), sem o filtro de ativos e sem a função do agente (plan 16.7): todo alerta da entrega é de
    /// equipamento.
    /// </summary>
    public async Task<IReadOnlyList<AlertRow>> ListOpenAlertsAsync(
        OperationsFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var alerts = await AlertsWithResource(dbContext.Alerts.Where(alert => alert.State != AlertState.Resolved))
            .ToListAsync(cancellationToken);
        if (alerts.Count == 0)
        {
            return [];
        }

        var resourceIds = alerts.Select(alert => alert.Alert.ResourceId).Distinct().ToArray();
        var matching = (await FilteredEquipment(EquipmentRows(filter), filter)
                .Where(row => resourceIds.Contains(row.Id))
                .Select(row => row.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        return alerts.Select(alert => ToRow(alert, matching.Contains(alert.Alert.ResourceId))).ToArray();
    }

    public async Task<IReadOnlyList<AlertRow>> ListOpenAlertsOfAsync(
        ResourceKind resourceKind, Guid resourceId, CancellationToken cancellationToken = default)
    {
        var alerts = await AlertsWithResource(dbContext.Alerts.Where(alert => alert.State != AlertState.Resolved
                && alert.ResourceKind == resourceKind
                && alert.ResourceId == resourceId))
            .ToListAsync(cancellationToken);

        return alerts.Select(alert => ToRow(alert, matchesFilter: true)).ToArray();
    }

    public async Task<AlertRow?> FindAlertAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var alert = await AlertsWithResource(dbContext.Alerts.Where(alert => alert.Id == id))
            .SingleOrDefaultAsync(cancellationToken);

        return alert is null ? null : ToRow(alert, matchesFilter: true);
    }

    public Task<DateTimeOffset?> LastTelemetryAtAsync(CancellationToken cancellationToken = default) =>
        dbContext.ResourceLiveStates.AsNoTracking().MaxAsync(state => (DateTimeOffset?)state.UpdatedAt, cancellationToken);

    public async Task<EquipmentLiveRow?> FindEquipmentLiveAsync(Guid equipmentId, CancellationToken cancellationToken = default)
    {
        var crewAssignable = await (
                from equipment in dbContext.Equipment.AsNoTracking()
                join type in dbContext.EquipmentTypes on equipment.TypeId equals type.Id
                where equipment.Id == equipmentId
                select (bool?)type.CrewAssignable)
            .SingleOrDefaultAsync(cancellationToken);

        return crewAssignable is { } crew
            ? new EquipmentLiveRow(crew, await FindLiveAsync(ResourceKind.Equipment, equipmentId, cancellationToken))
            : null;
    }

    public Task<LiveStateRow?> FindAgentLiveAsync(Guid agentId, CancellationToken cancellationToken = default) =>
        FindLiveAsync(ResourceKind.Agent, agentId, cancellationToken);

    private IQueryable<EquipmentSearch.Row> EquipmentRows(OperationsFilter filter) =>
        EquipmentSearch.Rows(dbContext, filter.Today, filter.Today.AddDays(filter.UpcomingWindowDays));

    private static IQueryable<EquipmentSearch.Row> FilteredEquipment(IQueryable<EquipmentSearch.Row> rows, OperationsFilter filter) =>
        EquipmentSearch.WhereFilters(
            rows,
            filter.TypeIds,
            filter.Statuses,
            filter.MaintenanceNeeds,
            filter.OrganizationIds,
            filter.RegionIds,
            filter.NeighborhoodIds);

    /// <summary>Agentes <b>ativos</b> com os filtros de função, órgão, região e bairro (pela base, D-65).</summary>
    private IQueryable<AgentRow> FilteredAgents(OperationsFilter filter)
    {
        var rows =
            from agent in dbContext.Agents.AsNoTracking()
            join organization in dbContext.Organizations on agent.OrganizationId equals organization.Id
            join operationalBase in dbContext.OperationalBases on agent.BaseId equals operationalBase.Id
            join neighborhood in dbContext.Neighborhoods on operationalBase.Location.NeighborhoodId equals neighborhood.Id
            where agent.IsActive
            select new AgentRow
            {
                Id = agent.Id,
                DisplayName = agent.DisplayName,
                Role = agent.Role,
                OrganizationId = agent.OrganizationId,
                OrganizationAcronym = organization.Acronym,
                NeighborhoodId = neighborhood.Id,
                RegionId = neighborhood.RegionId,
                Status = agent.Status,
                CreatedAt = agent.CreatedAt,
            };

        if (filter.AgentRoles.Count > 0)
        {
            var roles = filter.AgentRoles.ToArray();
            rows = rows.Where(row => roles.Contains(row.Role));
        }

        if (filter.OrganizationIds.Count > 0)
        {
            var organizationIds = filter.OrganizationIds.ToArray();
            rows = rows.Where(row => organizationIds.Contains(row.OrganizationId));
        }

        if (filter.RegionIds.Count > 0)
        {
            var regionIds = filter.RegionIds.ToArray();
            rows = rows.Where(row => regionIds.Contains(row.RegionId));
        }

        if (filter.NeighborhoodIds.Count > 0)
        {
            var neighborhoodIds = filter.NeighborhoodIds.ToArray();
            rows = rows.Where(row => neighborhoodIds.Contains(row.NeighborhoodId));
        }

        return rows;
    }

    /// <summary>Severidade mais grave entre os alertas abertos de cada equipamento (a ordem do enum, RN-18).</summary>
    private async Task<Dictionary<Guid, AlertSeverity?>> OpenAlertSeveritiesAsync(CancellationToken cancellationToken)
    {
        var open = await dbContext.Alerts
            .AsNoTracking()
            .Where(alert => alert.State != AlertState.Resolved && alert.ResourceKind == ResourceKind.Equipment)
            .Select(alert => new { alert.ResourceId, alert.Severity })
            .ToListAsync(cancellationToken);

        return open
            .GroupBy(alert => alert.ResourceId)
            .ToDictionary(group => group.Key, group => (AlertSeverity?)group.Min(alert => alert.Severity));
    }

    /// <summary>
    /// Alertas com o equipamento como aparece no mapa (RF-037): fixo na instalação, móvel na base e, se houver, na posição
    /// atual. Inclui equipamento inativo (D-77).
    /// </summary>
    private IQueryable<AlertWithResource> AlertsWithResource(IQueryable<Alert> alerts) =>
        from alert in alerts.AsNoTracking()
        join equipment in dbContext.Equipment on alert.ResourceId equals equipment.Id
        join type in dbContext.EquipmentTypes on equipment.TypeId equals type.Id
        join operationalBase in dbContext.OperationalBases on equipment.BaseId equals (Guid?)operationalBase.Id into bases
        from operationalBase in bases.DefaultIfEmpty()
        join neighborhood in dbContext.Neighborhoods
            on equipment.BaseId == null ? equipment.Location!.NeighborhoodId : operationalBase!.Location.NeighborhoodId
            equals neighborhood.Id
        join region in dbContext.Regions on neighborhood.RegionId equals region.Id
        join live in dbContext.ResourceLiveStates.Where(state => state.ResourceKind == ResourceKind.Equipment)
            on equipment.Id equals live.ResourceId into liveStates
        from live in liveStates.DefaultIfEmpty()
        where alert.ResourceKind == ResourceKind.Equipment
        select new AlertWithResource
        {
            Alert = alert,
            TypeName = type.Name,
            SerialNumber = equipment.SerialNumber,
            CrewAssignable = type.CrewAssignable,
            ResourceIsActive = equipment.IsActive,
            NeighborhoodName = neighborhood.Name,
            RegionName = region.Name,
            MapLatitude = equipment.BaseId == null ? equipment.Location!.Point.Latitude : operationalBase!.Location.Point.Latitude,
            MapLongitude = equipment.BaseId == null ? equipment.Location!.Point.Longitude : operationalBase!.Location.Point.Longitude,
            LiveLatitude = live == null ? null : live.Latitude,
            LiveLongitude = live == null ? null : live.Longitude,
        };

    private async Task<LiveStateRow?> FindLiveAsync(ResourceKind kind, Guid id, CancellationToken cancellationToken)
    {
        var live = await dbContext.ResourceLiveStates
            .AsNoTracking()
            .SingleOrDefaultAsync(state => state.ResourceKind == kind && state.ResourceId == id, cancellationToken);

        return live is null
            ? null
            : new LiveStateRow(Live(live.Latitude, live.Longitude), live.PositionRecordedAt, live.LastCommunicationAt);
    }

    private static GeoPosition? Live(double? latitude, double? longitude) =>
        latitude is { } lat && longitude is { } lon ? new GeoPosition(lat, lon) : null;

    private static AlertRow ToRow(AlertWithResource item, bool matchesFilter) => new(
        item.Alert.Id,
        item.Alert.Cause,
        item.Alert.Severity,
        item.Alert.State,
        item.Alert.ResourceKind,
        item.Alert.ResourceId,
        item.TypeName,
        item.SerialNumber,
        item.CrewAssignable,
        item.ResourceIsActive,
        item.NeighborhoodName,
        item.RegionName,
        new GeoPosition(item.MapLatitude, item.MapLongitude),
        Live(item.LiveLatitude, item.LiveLongitude),
        item.Alert.OpenedAt,
        item.Alert.LastOccurredAt,
        item.Alert.OccurrenceCount,
        item.Alert.AcknowledgedAt,
        item.Alert.AcknowledgedBy,
        matchesFilter);

    /// <summary>Linha de agente da Central, montada no banco; os filtros se compõem sobre ela.</summary>
    private sealed class AgentRow
    {
        public Guid Id { get; init; }

        public string DisplayName { get; init; } = string.Empty;

        public AgentRole Role { get; init; }

        public Guid OrganizationId { get; init; }

        public string OrganizationAcronym { get; init; } = string.Empty;

        public Guid NeighborhoodId { get; init; }

        public Guid RegionId { get; init; }

        public AgentStatus Status { get; init; }

        public DateTimeOffset CreatedAt { get; init; }
    }

    /// <summary>Alerta com o recurso, montado no banco.</summary>
    private sealed class AlertWithResource
    {
        public Alert Alert { get; init; } = null!;

        public string TypeName { get; init; } = string.Empty;

        public string SerialNumber { get; init; } = string.Empty;

        public bool CrewAssignable { get; init; }

        public bool ResourceIsActive { get; init; }

        public string NeighborhoodName { get; init; } = string.Empty;

        public string RegionName { get; init; } = string.Empty;

        public double MapLatitude { get; init; }

        public double MapLongitude { get; init; }

        public double? LiveLatitude { get; init; }

        public double? LiveLongitude { get; init; }
    }
}
