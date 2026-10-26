using Argos.Application.Telemetry;
using Argos.Contracts.Telemetry;
using Argos.Domain.Inventory.Agents;
using Argos.Domain.Telemetry;

namespace Argos.UnitTests.Application.Fakes;

/// <summary>
/// Telemetria em memória: os agentes vêm do <see cref="FakeInventory"/>; estados atuais e leituras ficam em listas.
/// </summary>
internal sealed class FakeTelemetry(FakeInventory inventory) : ITelemetryRepository, ITelemetryQueries
{
    public List<ResourceLiveState> LiveStates { get; } = [];

    public List<TelemetryReading> Readings { get; } = [];

    public Dictionary<Guid, TelemetryEquipmentFacts> EquipmentFacts { get; } = [];

    public TelemetryResourceList Resources { get; set; } = new();

    public Task<IReadOnlyList<Agent>> FindAgentsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Agent>>(inventory.Agents.Where(agent => ids.Contains(agent.Id)).ToArray());

    public Task<IReadOnlyList<ResourceLiveState>> FindLiveStatesAsync(
        ResourceKind resourceKind, IReadOnlyCollection<Guid> resourceIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ResourceLiveState>>(LiveStates
            .Where(state => state.ResourceKind == resourceKind && resourceIds.Contains(state.ResourceId))
            .ToArray());

    public void AddLiveState(ResourceLiveState state) => LiveStates.Add(state);

    public void AddReadings(IEnumerable<TelemetryReading> readings) => Readings.AddRange(readings);

    public Task<TelemetryResourceList> ListActiveResourcesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Resources);

    public Task<IReadOnlyDictionary<Guid, TelemetryEquipmentFacts>> FindEquipmentFactsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, TelemetryEquipmentFacts>>(
            EquipmentFacts.Where(pair => ids.Contains(pair.Key)).ToDictionary());
}
