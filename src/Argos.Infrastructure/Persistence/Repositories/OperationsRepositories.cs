using Argos.Application.Operations;
using Argos.Application.Seeding.Operations;
using Argos.Application.Telemetry;
using Argos.Domain.Inventory.Agents;
using Argos.Domain.Inventory.Equipments;
using Argos.Domain.Operations;
using Argos.Domain.Operations.Alerts;
using Argos.Domain.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace Argos.Infrastructure.Persistence.Repositories;

/// <summary>
/// Telemetria (plan 16.9; D-70, D-71): estados atuais carregados pelas chaves do lote e atualizados pelo EF Core;
/// leituras por <c>AddRange</c>, sem SQL bruto. Os agentes do lote vêm rastreados, para a situação reportada (RN-12) ser
/// gravada no mesmo <c>SaveChanges</c>. Sem remoção (Art. V.1).
/// </summary>
internal sealed class TelemetryRepository(ArgosDbContext dbContext) : ITelemetryRepository
{
    public async Task<IReadOnlyList<Agent>> FindAgentsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var values = ids.ToArray();
        return await dbContext.Agents.Where(agent => values.Contains(agent.Id)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ResourceLiveState>> FindLiveStatesAsync(
        ResourceKind resourceKind, IReadOnlyCollection<Guid> resourceIds, CancellationToken cancellationToken = default)
    {
        if (resourceIds.Count == 0)
        {
            return [];
        }

        var values = resourceIds.ToArray();
        return await dbContext.ResourceLiveStates
            .Where(state => state.ResourceKind == resourceKind && values.Contains(state.ResourceId))
            .ToListAsync(cancellationToken);
    }

    public void AddLiveState(ResourceLiveState state) => dbContext.ResourceLiveStates.Add(state);

    public void AddReadings(IEnumerable<TelemetryReading> readings) => dbContext.TelemetryReadings.AddRange(readings);
}

/// <summary>Alertas (plan 16.9; RN-19, RN-23): nunca removidos, só resolvidos.</summary>
internal sealed class AlertRepository(ArgosDbContext dbContext) : IAlertRepository
{
    public Task<Alert?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Alerts.SingleOrDefaultAsync(alert => alert.Id == id, cancellationToken);

    public Task<Alert?> FindOpenAsync(
        ResourceKind resourceKind, Guid resourceId, AlertCause cause, CancellationToken cancellationToken = default) =>
        dbContext.Alerts.SingleOrDefaultAsync(
            alert => alert.ResourceKind == resourceKind
                && alert.ResourceId == resourceId
                && alert.Cause == cause
                && alert.State != AlertState.Resolved,
            cancellationToken);

    public void Add(Alert alert) => dbContext.Alerts.Add(alert);
}

/// <summary>Cargas da Central (plan 16.9; D-78, D-94).</summary>
internal sealed class OperationsSeedStore(ArgosDbContext dbContext) : IOperationsSeedStore
{
    public async Task<IReadOnlyList<Guid>> ListActiveEquipmentIdsAsync(
        IReadOnlyCollection<EquipmentStatus> statuses, CancellationToken cancellationToken = default)
    {
        var values = statuses.ToArray();
        return await dbContext.Equipment
            .AsNoTracking()
            .Where(equipment => equipment.IsActive && values.Contains(equipment.Status))
            .OrderBy(equipment => equipment.Id)
            .Select(equipment => equipment.Id)
            .ToListAsync(cancellationToken);
    }

    public void AddDutyShift(AgentDutyShift shift) => dbContext.AgentDutyShifts.Add(shift);
}
