using Argos.Domain.Inventory.Equipments;
using Argos.Domain.Inventory.Types;

namespace Argos.Domain.Inventory.Agents;

/// <summary>
/// Serviço de domínio do vínculo agente–equipamento móvel (RF-016, US-07, RN-35; D-62; plan 15.2.10). A regra envolve
/// dois agregados, por isso fica aqui, e não em <see cref="Agent"/> nem em <see cref="Equipment"/>, que não conhece
/// agentes. A Aplicação chama com o agente, o equipamento, o tipo e os vínculos abertos já carregados, e grava tudo
/// numa só transação. Sem I/O.
/// </summary>
public static class AssignmentPolicy
{
    /// <summary>
    /// Situações em que o equipamento não recebe vínculo novo: Desativado e Furtado (D-62). Os vínculos existentes
    /// continuam até serem encerrados ou até a inativação. É a mesma lista que o <see cref="Assign"/> confere e que a
    /// consulta dos equipamentos vinculáveis exclui (D-68; plan 15.2.10, 15.13), para a regra ficar num lugar só.
    /// </summary>
    public static readonly IReadOnlyList<EquipmentStatus> StatusesRefusingNewAssignment =
        [EquipmentStatus.Deactivated, EquipmentStatus.Stolen];

    /// <summary>
    /// Vincula o agente ao equipamento (US-07): agente ativo; equipamento ativo, móvel e de tipo
    /// <see cref="EquipmentType.CrewAssignable"/> (viatura e moto de agente); equipamento fora de Desativado e Furtado
    /// (D-62). Um agente tem no máximo um vínculo aberto: o atual, se houver, é encerrado com
    /// <see cref="AssignmentEndReason.Reassigned"/> no mesmo instante. Um equipamento pode ter vários agentes.
    /// </summary>
    /// <param name="type">Tipo do equipamento, para <see cref="EquipmentType.CrewAssignable"/>.</param>
    /// <param name="currentAssignment">Vínculo aberto do agente, se houver.</param>
    /// <exception cref="InventoryRuleException">Qualquer regra violada (409 com o motivo); nada muda.</exception>
    public static AssignmentChange Assign(
        Agent agent,
        Equipment equipment,
        EquipmentType type,
        AgentAssignment? currentAssignment,
        DateTimeOffset at,
        string by)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(equipment);
        ArgumentNullException.ThrowIfNull(type);
        var author = InventoryAuthor.Valid(by);

        if (type.Id != equipment.TypeId)
        {
            throw new ArgumentException("O tipo informado não é o do equipamento.", nameof(type));
        }

        if (currentAssignment is not null)
        {
            EnsureOpenOf(currentAssignment, agentId: agent.Id, equipmentId: null, nameof(currentAssignment));
        }

        if (!agent.IsActive)
        {
            throw new InventoryRuleException("Agente inativo não pode ser vinculado a equipamentos.");
        }

        if (!equipment.IsActive)
        {
            throw new InventoryRuleException("Equipamento inativo não recebe vínculo de agentes.");
        }

        if (equipment.Category != EquipmentCategory.Mobile || !type.CrewAssignable)
        {
            throw new InventoryRuleException($"Equipamentos do tipo {type.Name} não recebem vínculo de agentes.");
        }

        if (StatusesRefusingNewAssignment.Contains(equipment.Status))
        {
            throw new InventoryRuleException(
                $"Não é possível vincular agentes a um equipamento {EquipmentStatusLabels.For(equipment.Status).ToLowerInvariant()}.");
        }

        if (currentAssignment?.EquipmentId == equipment.Id)
        {
            throw new InventoryRuleException("O agente já está vinculado a esse equipamento.");
        }

        currentAssignment?.Close(AssignmentEndReason.Reassigned, at, author);
        return new AssignmentChange(AgentAssignment.Start(agent.Id, equipment.Id, at, author), currentAssignment);
    }

    /// <summary>
    /// Inativa o agente (US-09, RN-35; <see cref="Agent.Inactivate"/>) e encerra o vínculo aberto, se houver, com
    /// <see cref="AssignmentEndReason.AgentInactivated"/>. Se a inativação for recusada, o vínculo continua aberto.
    /// </summary>
    /// <param name="openAssignment">Vínculo aberto do agente, se houver.</param>
    public static AgentInactivation InactivateAgent(
        Agent agent, AgentAssignment? openAssignment, string? reason, DateTimeOffset at, string by)
    {
        ArgumentNullException.ThrowIfNull(agent);

        if (openAssignment is not null)
        {
            EnsureOpenOf(openAssignment, agentId: agent.Id, equipmentId: null, nameof(openAssignment));
        }

        var change = agent.Inactivate(reason, at, by);
        openAssignment?.Close(AssignmentEndReason.AgentInactivated, at, change.Author);
        return new AgentInactivation(change, openAssignment);
    }

    /// <summary>
    /// Inativa o equipamento (US-09, RN-35; <see cref="Equipment.Inactivate"/>) e encerra todos os vínculos abertos com
    /// ele, com <see cref="AssignmentEndReason.EquipmentInactivated"/>. Se a inativação for recusada, os vínculos
    /// continuam abertos.
    /// </summary>
    /// <param name="openAssignments">Vínculos abertos do equipamento (nenhum, um ou vários).</param>
    public static EquipmentInactivation InactivateEquipment(
        Equipment equipment, IReadOnlyCollection<AgentAssignment> openAssignments, string? reason, DateTimeOffset at, string by)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        ArgumentNullException.ThrowIfNull(openAssignments);

        foreach (var assignment in openAssignments)
        {
            EnsureOpenOf(assignment, agentId: null, equipmentId: equipment.Id, nameof(openAssignments));
        }

        var change = equipment.Inactivate(reason, at, by);
        foreach (var assignment in openAssignments)
        {
            assignment.Close(AssignmentEndReason.EquipmentInactivated, at, change.Author);
        }

        return new EquipmentInactivation(change, openAssignments.ToArray());
    }

    /// <summary>Os vínculos carregados pela Aplicação precisam estar abertos e ser do agente ou do equipamento.</summary>
    private static void EnsureOpenOf(AgentAssignment assignment, Guid? agentId, Guid? equipmentId, string parameter)
    {
        ArgumentNullException.ThrowIfNull(assignment, parameter);

        if (!assignment.IsOpen
            || (agentId is not null && assignment.AgentId != agentId)
            || (equipmentId is not null && assignment.EquipmentId != equipmentId))
        {
            throw new ArgumentException("Informe só os vínculos abertos do agente ou do equipamento.", parameter);
        }
    }
}

/// <summary>Resultado de um vínculo: o novo e o anterior, encerrado com <see cref="AssignmentEndReason.Reassigned"/> (US-07).</summary>
public sealed record AssignmentChange(AgentAssignment Started, AgentAssignment? Ended);

/// <summary>Resultado da inativação do agente: o registro cadastral (D-49) e o vínculo encerrado, se havia (RN-35).</summary>
public sealed record AgentInactivation(InventoryChange Change, AgentAssignment? EndedAssignment);

/// <summary>Resultado da inativação do equipamento: o registro cadastral (D-49) e os vínculos encerrados (RN-35).</summary>
public sealed record EquipmentInactivation(InventoryChange Change, IReadOnlyList<AgentAssignment> EndedAssignments);
