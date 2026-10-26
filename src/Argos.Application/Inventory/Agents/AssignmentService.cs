using Argos.Application.Common;
using Argos.Application.Common.Abstractions;
using Argos.Application.Common.Exceptions;
using Argos.Application.Inventory.Equipments;
using Argos.Application.Inventory.Types;
using Argos.Application.Operations;
using Argos.Contracts.Inventory;
using Argos.Contracts.Operations;
using Argos.Domain.Inventory.Agents;
using ContractEndReason = Argos.Contracts.Inventory.AssignmentEndReason;

namespace Argos.Application.Inventory.Agents;

/// <summary>
/// Vínculos entre agentes e equipamentos móveis (US-07, RF-016; D-62; plan 15.2.10, 15.12). As regras ficam na
/// <see cref="AssignmentPolicy"/>; aqui se carregam o agente, o equipamento, o tipo e o vínculo aberto, e se grava
/// tudo num único <c>SaveChanges</c>: na troca, o vínculo anterior encerrado e o novo entram na mesma gravação.
/// <para>Depois de cada gravação, avisa a Central com <c>OperationsChanged</c> (D-80; plan 16.8).</para>
/// </summary>
public sealed class AssignmentService(
    IAgentRepository agents,
    IAgentAssignmentRepository assignments,
    IEquipmentRepository equipments,
    IEquipmentTypeRepository types,
    IUnitOfWork unitOfWork,
    IRealtimeNotifier notifier,
    ICurrentActor actor,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Vincula o agente ao equipamento (US-07). Um vínculo aberto com outro equipamento é encerrado com o motivo
    /// "vinculado a outro equipamento" no mesmo instante, e os dois ficam na linha do tempo.
    /// </summary>
    /// <exception cref="NotFoundException">Agente ou equipamento inexistentes.</exception>
    /// <exception cref="ValidationFailedException">Equipamento não informado, no campo <c>equipmentId</c>.</exception>
    /// <exception cref="Argos.Domain.Inventory.InventoryRuleException">Regra do vínculo violada (409 com o motivo).</exception>
    public async Task<AssignmentData> AssignAsync(
        Guid agentId, AssignEquipmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var agent = await FindAgentAsync(agentId, cancellationToken);

        if (request.EquipmentId is not { } equipmentId)
        {
            throw new ValidationFailedException("equipmentId", "Escolha o equipamento.");
        }

        var equipment = await equipments.FindByIdAsync(equipmentId, cancellationToken)
            ?? throw new NotFoundException(EquipmentQueryService.NotFoundMessage);
        var type = await types.FindByIdAsync(equipment.TypeId, cancellationToken)
            ?? throw new InvalidOperationException($"Tipo {equipment.TypeId} do equipamento {equipmentId} não encontrado.");
        var current = await assignments.FindOpenByAgentAsync(agentId, cancellationToken);

        var change = AssignmentPolicy.Assign(agent, equipment, type, current, timeProvider.GetUtcNow(), actor.Name);

        assignments.Add(change.Started);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.PublishOperationsChangedAsync(
            OperationsChangeKind.Registration, timeProvider.GetUtcNow(), cancellationToken);

        return ToData(change.Started);
    }

    /// <summary>Encerra o vínculo por decisão de uma pessoa (RF-016), mantendo o histórico.</summary>
    /// <exception cref="NotFoundException">Agente inexistente ou vínculo que não é dele.</exception>
    /// <exception cref="Argos.Domain.Inventory.InventoryRuleException">Vínculo já encerrado (409).</exception>
    public async Task<AssignmentData> EndAsync(Guid agentId, Guid assignmentId, CancellationToken cancellationToken = default)
    {
        await FindAgentAsync(agentId, cancellationToken);

        var assignment = await assignments.FindByIdAsync(assignmentId, cancellationToken);
        if (assignment is null || assignment.AgentId != agentId)
        {
            throw new NotFoundException("Vínculo não encontrado.");
        }

        assignment.End(timeProvider.GetUtcNow(), actor.Name);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.PublishOperationsChangedAsync(
            OperationsChangeKind.Registration, timeProvider.GetUtcNow(), cancellationToken);

        return ToData(assignment);
    }

    private static AssignmentData ToData(AgentAssignment assignment) => new()
    {
        Id = assignment.Id,
        AgentId = assignment.AgentId,
        EquipmentId = assignment.EquipmentId,
        StartedAt = assignment.StartedAt,
        StartedBy = assignment.StartedBy,
        EndedAt = assignment.EndedAt,
        EndedBy = assignment.EndedBy,
        EndReason = EnumNames.ConvertNullable<ContractEndReason>(assignment.EndReason),
    };

    private async Task<Agent> FindAgentAsync(Guid id, CancellationToken cancellationToken) =>
        await agents.FindByIdAsync(id, cancellationToken) ?? throw new NotFoundException(AgentQueryService.NotFoundMessage);
}
