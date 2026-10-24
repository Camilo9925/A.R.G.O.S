using Argos.Application.Inventory.Timeline;
using Argos.Contracts.Common;
using Argos.Contracts.Inventory;
using Argos.Domain.Inventory;
using Argos.Domain.Inventory.Equipments;
using Microsoft.EntityFrameworkCore;
using AgentAssignment = Argos.Domain.Inventory.Agents.AgentAssignment;
using DomainAssignmentEndReason = Argos.Domain.Inventory.Agents.AssignmentEndReason;

namespace Argos.Infrastructure.Persistence.Queries;

/// <summary>
/// Linha do tempo de equipamento e de agente (US-08, RF-011, RF-018; Art. V.1, V.3; D-49; plan 15.6). Lê as fontes do
/// recurso, cada fato numa fonte só: registros de situação, manutenções (abertura, conclusão e cancelamento, cada um
/// com o seu instante), registros cadastrais e vínculos (início e fim). Projeta tudo em <see cref="TimelineFact"/> e
/// ordena do mais recente para o mais antigo.
/// <para>
/// A junção e a paginação são feitas em memória, depois de ler as fontes de um único recurso: no inventário, um
/// recurso tem dezenas de fatos, e uma união de quatro tabelas heterogêneas no banco seria mais frágil sem ganho
/// (plan 15.13). A rodada da Telemetria, que multiplica as mudanças de situação dos agentes, revê esta escolha.
/// </para>
/// </summary>
internal sealed class TimelineQueries(ArgosDbContext dbContext) : ITimelineQueries
{
    public async Task<PagedResult<TimelineFact>> ListEquipmentTimelineAsync(
        Guid equipmentId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var facts = new List<Ordered>();

        var statusChanges = await dbContext.EquipmentStatusChanges
            .AsNoTracking()
            .Where(change => change.EquipmentId == equipmentId)
            .ToListAsync(cancellationToken);
        facts.AddRange(statusChanges.Select(change => new Ordered(
            new TimelineFact(
                change.FromStatus is null ? TimelineEntryKind.Registered : TimelineEntryKind.StatusChanged,
                change.OccurredAt,
                change.Author)
            {
                EquipmentStatus = new TimelineEquipmentStatus(
                    change.FromStatus, change.ToStatus, change.Origin, change.Justification),
            },
            change.Id)));

        var maintenances = await dbContext.Maintenances
            .AsNoTracking()
            .Where(maintenance => maintenance.EquipmentId == equipmentId)
            .ToListAsync(cancellationToken);
        facts.AddRange(maintenances.SelectMany(MaintenanceFacts));

        facts.AddRange(await RegistrationFactsAsync(InventoryEntityType.Equipment, equipmentId, cancellationToken));

        var crew = await (
                from assignment in dbContext.AgentAssignments.AsNoTracking()
                join agent in dbContext.Agents on assignment.AgentId equals agent.Id
                join equipment in dbContext.Equipment on assignment.EquipmentId equals equipment.Id
                join type in dbContext.EquipmentTypes on equipment.TypeId equals type.Id
                where assignment.EquipmentId == equipmentId
                select new CrewRow
                {
                    Assignment = assignment,
                    AgentDisplayName = agent.DisplayName,
                    AgentRegistrationNumber = agent.RegistrationNumber,
                    EquipmentTypeName = type.Name,
                    EquipmentSerialNumber = equipment.SerialNumber,
                })
            .ToListAsync(cancellationToken);
        facts.AddRange(crew.SelectMany(CrewFacts));

        return Page(facts, page, pageSize);
    }

    public async Task<PagedResult<TimelineFact>> ListAgentTimelineAsync(
        Guid agentId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var facts = new List<Ordered>();

        var statusChanges = await dbContext.AgentStatusChanges
            .AsNoTracking()
            .Where(change => change.AgentId == agentId)
            .ToListAsync(cancellationToken);
        facts.AddRange(statusChanges.Select(change => new Ordered(
            new TimelineFact(
                change.FromStatus is null ? TimelineEntryKind.Registered : TimelineEntryKind.StatusChanged,
                change.OccurredAt,
                change.Author)
            {
                AgentStatus = new TimelineAgentStatus(change.FromStatus, change.ToStatus),
            },
            change.Id)));

        facts.AddRange(await RegistrationFactsAsync(InventoryEntityType.Agent, agentId, cancellationToken));

        var crew = await (
                from assignment in dbContext.AgentAssignments.AsNoTracking()
                join agent in dbContext.Agents on assignment.AgentId equals agent.Id
                join equipment in dbContext.Equipment on assignment.EquipmentId equals equipment.Id
                join type in dbContext.EquipmentTypes on equipment.TypeId equals type.Id
                where assignment.AgentId == agentId
                select new CrewRow
                {
                    Assignment = assignment,
                    AgentDisplayName = agent.DisplayName,
                    AgentRegistrationNumber = agent.RegistrationNumber,
                    EquipmentTypeName = type.Name,
                    EquipmentSerialNumber = equipment.SerialNumber,
                })
            .ToListAsync(cancellationToken);
        facts.AddRange(crew.SelectMany(CrewFacts));

        return Page(facts, page, pageSize);
    }

    /// <summary>Alterações de cadastro e inativação (D-49). O cadastro de equipamento e de agente não gera <c>Created</c>.</summary>
    private async Task<IEnumerable<Ordered>> RegistrationFactsAsync(
        InventoryEntityType entityType, Guid entityId, CancellationToken cancellationToken)
    {
        var changes = await dbContext.InventoryChanges
            .AsNoTracking()
            .Where(change => change.EntityType == entityType
                && change.EntityId == entityId
                && change.Kind != InventoryChangeKind.Created)
            .ToListAsync(cancellationToken);

        return changes.Select(change => new Ordered(
            new TimelineFact(
                change.Kind == InventoryChangeKind.Inactivated ? TimelineEntryKind.Inactivated : TimelineEntryKind.RegistrationUpdated,
                change.OccurredAt,
                change.Author)
            {
                Registration = new TimelineRegistration(change.Changes, change.Reason),
            },
            change.Id));
    }

    /// <summary>A mesma manutenção gera o fato da abertura e, depois, o da conclusão ou do cancelamento.</summary>
    private static IEnumerable<Ordered> MaintenanceFacts(Maintenance maintenance)
    {
        var part = new TimelineMaintenance(
            maintenance.Kind,
            maintenance.Origin,
            maintenance.Description,
            maintenance.DueDate,
            maintenance.PeriodicityDays,
            maintenance.Result,
            maintenance.CompletionNotes,
            maintenance.CancellationReason);

        yield return new Ordered(
            new TimelineFact(TimelineEntryKind.MaintenanceOpened, maintenance.OpenedAt, maintenance.OpenedBy) { Maintenance = part },
            maintenance.Id);

        if (maintenance.CompletedAt is { } completedAt)
        {
            yield return new Ordered(
                new TimelineFact(TimelineEntryKind.MaintenanceCompleted, completedAt, maintenance.CompletedBy ?? string.Empty)
                {
                    Maintenance = part,
                },
                maintenance.Id);
        }

        if (maintenance.CancelledAt is { } cancelledAt)
        {
            yield return new Ordered(
                new TimelineFact(TimelineEntryKind.MaintenanceCancelled, cancelledAt, maintenance.CancelledBy ?? string.Empty)
                {
                    Maintenance = part,
                },
                maintenance.Id);
        }
    }

    /// <summary>O início do vínculo e, se houver, o fim, com o motivo do encerramento (plan 15.2.10).</summary>
    private static IEnumerable<Ordered> CrewFacts(CrewRow row)
    {
        var assignment = row.Assignment;
        TimelineCrew Crew(DomainAssignmentEndReason? endReason) => new(
            assignment.AgentId,
            row.AgentDisplayName,
            row.AgentRegistrationNumber,
            assignment.EquipmentId,
            row.EquipmentTypeName,
            row.EquipmentSerialNumber,
            endReason);

        yield return new Ordered(
            new TimelineFact(TimelineEntryKind.CrewAssigned, assignment.StartedAt, assignment.StartedBy) { Crew = Crew(null) },
            assignment.Id);

        if (assignment.EndedAt is { } endedAt)
        {
            yield return new Ordered(
                new TimelineFact(TimelineEntryKind.CrewUnassigned, endedAt, assignment.EndedBy ?? string.Empty)
                {
                    Crew = Crew(assignment.EndReason),
                },
                assignment.Id);
        }
    }

    /// <summary>
    /// Do mais recente para o mais antigo. No mesmo instante, os fatos de uma operação aparecem na ordem inversa à
    /// que aconteceram (<see cref="Sequence"/>), e o id da fonte desempata, para a paginação ser estável.
    /// </summary>
    private static PagedResult<TimelineFact> Page(List<Ordered> facts, int page, int pageSize)
    {
        var ordered = facts
            .OrderByDescending(fact => fact.Fact.OccurredAt)
            .ThenByDescending(fact => Sequence(fact.Fact.Kind))
            .ThenBy(fact => fact.SourceId)
            .ToList();

        return new PagedResult<TimelineFact>
        {
            Items = ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(fact => fact.Fact).ToArray(),
            Page = page,
            PageSize = pageSize,
            TotalCount = ordered.Count,
        };
    }

    /// <summary>
    /// Ordem em que os fatos de uma mesma operação acontecem no mesmo instante: o cadastro antes da periódica que ele
    /// abre (RN-08); a conclusão antes da mudança de situação e da próxima periódica (RN-09, RN-11); a mudança de
    /// situação antes da imediata (RN-07) e do cancelamento (Furtado); a inativação antes do cancelamento e dos vínculos
    /// encerrados (RN-35); o vínculo anterior encerrado antes do novo (US-07).
    /// </summary>
    private static int Sequence(TimelineEntryKind kind) => kind switch
    {
        TimelineEntryKind.Registered => 0,
        TimelineEntryKind.MaintenanceCompleted => 1,
        TimelineEntryKind.StatusChanged => 2,
        TimelineEntryKind.Inactivated => 3,
        TimelineEntryKind.MaintenanceCancelled => 4,
        TimelineEntryKind.CrewUnassigned => 5,
        TimelineEntryKind.MaintenanceOpened => 6,
        TimelineEntryKind.CrewAssigned => 7,
        _ => 8,
    };

    private sealed record Ordered(TimelineFact Fact, Guid SourceId);

    private sealed class CrewRow
    {
        public AgentAssignment Assignment { get; init; } = null!;

        public string AgentDisplayName { get; init; } = string.Empty;

        public string AgentRegistrationNumber { get; init; } = string.Empty;

        public string EquipmentTypeName { get; init; } = string.Empty;

        public string EquipmentSerialNumber { get; init; } = string.Empty;
    }
}
