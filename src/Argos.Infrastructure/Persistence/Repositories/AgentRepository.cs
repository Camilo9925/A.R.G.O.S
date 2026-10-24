using Argos.Application.Inventory.Agents;
using Argos.Domain.Inventory.Agents;
using Argos.Infrastructure.Persistence.Configurations.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Argos.Infrastructure.Persistence.Repositories;

/// <summary>
/// Agentes (plan 15.12). O histórico de situação não é carregado e só recebe registros novos (Art. V.1), como no
/// equipamento.
/// </summary>
internal sealed class AgentRepository(ArgosDbContext dbContext) : IAgentRepository
{
    public Task<Agent?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Agents.SingleOrDefaultAsync(agent => agent.Id == id, cancellationToken);

    public Task<Guid?> FindIdByRegistrationAsync(
        Guid organizationId, string registrationKey, CancellationToken cancellationToken = default) =>
        dbContext.Agents
            .Where(agent => agent.OrganizationId == organizationId && agent.RegistrationKey == registrationKey)
            .Select(agent => (Guid?)agent.Id)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>O <c>xmin</c> lido com o agregado, que também é o usado na gravação (D-60).</summary>
    public uint VersionOf(Agent agent) =>
        dbContext.Entry(agent).Property<uint>(AgentConfiguration.VersionProperty).OriginalValue;

    public Task<bool> AnyActiveInBaseAsync(Guid baseId, CancellationToken cancellationToken = default) =>
        dbContext.Agents.AnyAsync(agent => agent.IsActive && agent.BaseId == baseId, cancellationToken);

    public void Add(Agent agent) => dbContext.Agents.Add(agent);
}

/// <summary>Vínculos entre agentes e equipamentos móveis (plan 15.2.10, 15.12). Nunca removidos, só encerrados.</summary>
internal sealed class AgentAssignmentRepository(ArgosDbContext dbContext) : IAgentAssignmentRepository
{
    public Task<AgentAssignment?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.AgentAssignments.SingleOrDefaultAsync(assignment => assignment.Id == id, cancellationToken);

    public Task<AgentAssignment?> FindOpenByAgentAsync(Guid agentId, CancellationToken cancellationToken = default) =>
        dbContext.AgentAssignments.SingleOrDefaultAsync(
            assignment => assignment.AgentId == agentId && assignment.EndedAt == null, cancellationToken);

    public async Task<IReadOnlyList<AgentAssignment>> ListOpenByEquipmentAsync(
        Guid equipmentId, CancellationToken cancellationToken = default) =>
        await dbContext.AgentAssignments
            .Where(assignment => assignment.EquipmentId == equipmentId && assignment.EndedAt == null)
            .OrderBy(assignment => assignment.StartedAt)
            .ThenBy(assignment => assignment.Id)
            .ToListAsync(cancellationToken);

    public void Add(AgentAssignment assignment) => dbContext.AgentAssignments.Add(assignment);
}
