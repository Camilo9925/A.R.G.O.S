using Argos.Application.Common;
using Argos.Application.Common.Abstractions;
using Argos.Application.Common.Exceptions;
using Argos.Contracts.Inventory;
using DomainAgentStatus = Argos.Domain.Inventory.Agents.AgentStatus;

namespace Argos.Application.Inventory.Agents;

/// <summary>
/// Mudança de situação do agente (RF-015, RN-12; plan 15.2.9): qualquer situação diferente da atual, sem
/// justificativa, registrada com instante e autor. A regra e a recusa de cadastro inativo (RN-35) ficam no Domínio.
/// </summary>
public sealed class AgentStatusService(
    IAgentRepository agents,
    AgentQueryService agentQueries,
    IUnitOfWork unitOfWork,
    ICurrentActor actor,
    TimeProvider timeProvider)
{
    /// <exception cref="NotFoundException">Agente inexistente.</exception>
    /// <exception cref="ValidationFailedException">Situação não informada, no campo <c>status</c>.</exception>
    public async Task<AgentDetails> ChangeAsync(
        Guid id, ChangeAgentStatusRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var agent = await agents.FindByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(AgentQueryService.NotFoundMessage);

        if (request.Status is not { } status)
        {
            throw new ValidationFailedException("status", "Escolha a nova situação.");
        }

        agent.ChangeStatus(EnumNames.Convert<DomainAgentStatus>(status), timeProvider.GetUtcNow(), actor.Name);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await agentQueries.GetDetailsAsync(id, cancellationToken);
    }
}
