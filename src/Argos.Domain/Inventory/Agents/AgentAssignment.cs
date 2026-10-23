using Argos.Domain.Common;

namespace Argos.Domain.Inventory.Agents;

/// <summary>Por que um vínculo foi encerrado (plan 15.2.10).</summary>
public enum AssignmentEndReason
{
    /// <summary>O agente foi vinculado a outro equipamento (US-07).</summary>
    Reassigned,

    /// <summary>Encerrado por uma pessoa (RF-016).</summary>
    Ended,

    /// <summary>O agente foi inativado (RN-35).</summary>
    AgentInactivated,

    /// <summary>O equipamento foi inativado (RN-35).</summary>
    EquipmentInactivated,
}

/// <summary>
/// Vínculo de um agente com um equipamento móvel (RF-016, US-07; D-62; plan 15.2.10): entidade explícita com início e
/// fim, nunca coleção substituída nem many-to-many automático, que gerariam <c>DELETE</c>. Início e fim são gravados
/// uma única vez; um vínculo encerrado nunca reabre (um novo vínculo é outra linha). Começa só pela
/// <see cref="AssignmentPolicy"/>, que confere as regras dos dois agregados.
/// </summary>
public sealed class AgentAssignment : IAuditable
{
    // Usado pelo EF Core ao materializar.
    private AgentAssignment()
    {
    }

    private AgentAssignment(Guid agentId, Guid equipmentId, DateTimeOffset startedAt, string startedBy)
    {
        Id = Guid.NewGuid();
        AgentId = agentId;
        EquipmentId = equipmentId;
        StartedAt = startedAt.ToUniversalTime();
        StartedBy = startedBy;
    }

    public Guid Id { get; private set; }

    public Guid AgentId { get; private set; }

    public Guid EquipmentId { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public string StartedBy { get; private set; } = string.Empty;

    public DateTimeOffset? EndedAt { get; private set; }

    public string? EndedBy { get; private set; }

    public AssignmentEndReason? EndReason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; private set; }

    public string? UpdatedBy { get; private set; }

    public bool IsOpen => EndedAt is null;

    /// <summary>Encerra o vínculo por decisão de uma pessoa (RF-016), com <see cref="AssignmentEndReason.Ended"/>.</summary>
    /// <exception cref="InventoryRuleException">Vínculo já encerrado.</exception>
    public void End(DateTimeOffset at, string by) => Close(AssignmentEndReason.Ended, at, InventoryAuthor.Valid(by));

    internal static AgentAssignment Start(Guid agentId, Guid equipmentId, DateTimeOffset at, string by) =>
        new(agentId, equipmentId, at, by);

    internal void Close(AssignmentEndReason reason, DateTimeOffset at, string by)
    {
        if (!IsOpen)
        {
            throw new InventoryRuleException("O vínculo já foi encerrado e não volta a abrir.");
        }

        EndedAt = at.ToUniversalTime();
        EndedBy = by;
        EndReason = reason;
    }
}
