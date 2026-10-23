using Argos.Domain.Common;

namespace Argos.Domain.Inventory.Agents;

/// <summary>
/// Registro de uma mudança de situação do agente (RF-015, RF-018, US-06, RN-12; plan 15.2.9), com instante e autor,
/// na forma do registro do equipamento. Não tem justificativa, porque a RN-12 não a pede, nem manutenção. O registro
/// inicial do cadastro não tem situação anterior. Nunca é alterado nem removido (Art. V.1).
/// </summary>
public sealed class AgentStatusChange : IAppendOnly
{
    // Usado pelo EF Core ao materializar.
    private AgentStatusChange()
    {
    }

    private AgentStatusChange(
        Guid agentId,
        AgentStatus? fromStatus,
        AgentStatus toStatus,
        AgentStatusChangeOrigin origin,
        DateTimeOffset occurredAt,
        string author)
    {
        Id = Guid.NewGuid();
        AgentId = agentId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Origin = origin;
        OccurredAt = occurredAt.ToUniversalTime();
        Author = author;
    }

    public Guid Id { get; private set; }

    public Guid AgentId { get; private set; }

    /// <summary>Nula no registro inicial (US-06).</summary>
    public AgentStatus? FromStatus { get; private set; }

    public AgentStatus ToStatus { get; private set; }

    public AgentStatusChangeOrigin Origin { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public string Author { get; private set; } = string.Empty;

    internal static AgentStatusChange Record(
        Guid agentId,
        AgentStatus? fromStatus,
        AgentStatus toStatus,
        AgentStatusChangeOrigin origin,
        DateTimeOffset occurredAt,
        string author) =>
        new(agentId, fromStatus, toStatus, origin, occurredAt, author);
}
