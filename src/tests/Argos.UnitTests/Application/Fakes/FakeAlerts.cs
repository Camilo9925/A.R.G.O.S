using Argos.Application.Operations;
using Argos.Domain.Operations.Alerts;
using Argos.Domain.Telemetry;

namespace Argos.UnitTests.Application.Fakes;

/// <summary>Alertas em memória; o que é acrescentado fica visível na hora, como no <see cref="FakeInventory"/>.</summary>
internal sealed class FakeAlertRepository : IAlertRepository
{
    public List<Alert> Alerts { get; } = [];

    public Task<Alert?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Alerts.SingleOrDefault(alert => alert.Id == id));

    public Task<Alert?> FindOpenAsync(
        ResourceKind resourceKind, Guid resourceId, AlertCause cause, CancellationToken cancellationToken = default) =>
        Task.FromResult(Alerts.SingleOrDefault(alert =>
            alert.IsOpen && alert.ResourceKind == resourceKind && alert.ResourceId == resourceId && alert.Cause == cause));

    public void Add(Alert alert) => Alerts.Add(alert);
}

/// <summary>
/// Consultas da Central com as linhas montadas pelo teste: devolvem o que foi posto nas listas, sem filtrar, porque os
/// predicados de filtro são da Infraestrutura (T113). Guarda o último filtro recebido.
/// </summary>
internal sealed class FakeOperationsQueries : IOperationsQueries
{
    public List<EquipmentMarkerRow> Equipment { get; } = [];

    public List<AgentMarkerRow> Agents { get; } = [];

    public List<RegionAgentCountRow> RegionCounts { get; } = [];

    public List<AlertRow> Alerts { get; } = [];

    public Dictionary<Guid, EquipmentLiveRow> EquipmentLive { get; } = [];

    public Dictionary<Guid, LiveStateRow> AgentLive { get; } = [];

    public DateTimeOffset? LastTelemetryAt { get; set; }

    public OperationsFilter? LastFilter { get; private set; }

    public bool EquipmentListed { get; private set; }

    public bool AgentsListed { get; private set; }

    public Task<IReadOnlyList<EquipmentMarkerRow>> ListEquipmentAsync(OperationsFilter filter, CancellationToken cancellationToken = default)
    {
        LastFilter = filter;
        EquipmentListed = true;
        return Task.FromResult<IReadOnlyList<EquipmentMarkerRow>>(Equipment);
    }

    public Task<IReadOnlyList<AgentMarkerRow>> ListAgentsWithPositionAsync(
        OperationsFilter filter, CancellationToken cancellationToken = default)
    {
        AgentsListed = true;
        return Task.FromResult<IReadOnlyList<AgentMarkerRow>>(Agents);
    }

    public Task<IReadOnlyList<RegionAgentCountRow>> CountAgentsInActivityByRegionAsync(
        OperationsFilter filter, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RegionAgentCountRow>>(RegionCounts);

    public Task<IReadOnlyList<AlertRow>> ListOpenAlertsAsync(OperationsFilter filter, CancellationToken cancellationToken = default)
    {
        LastFilter = filter;
        return Task.FromResult<IReadOnlyList<AlertRow>>(Alerts);
    }

    public Task<IReadOnlyList<AlertRow>> ListOpenAlertsOfAsync(
        ResourceKind resourceKind, Guid resourceId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AlertRow>>(
            Alerts.Where(row => row.ResourceKind == resourceKind && row.ResourceId == resourceId).ToArray());

    public Task<AlertRow?> FindAlertAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Alerts.SingleOrDefault(row => row.Id == id));

    public Task<DateTimeOffset?> LastTelemetryAtAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(LastTelemetryAt);

    public Task<EquipmentLiveRow?> FindEquipmentLiveAsync(Guid equipmentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(EquipmentLive.GetValueOrDefault(equipmentId));

    public Task<LiveStateRow?> FindAgentLiveAsync(Guid agentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(AgentLive.GetValueOrDefault(agentId));
}
