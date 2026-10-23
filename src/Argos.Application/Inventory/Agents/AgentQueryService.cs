using Argos.Application.Common;
using Argos.Application.Common.Abstractions;
using Argos.Application.Common.Exceptions;
using Argos.Application.Inventory.Equipments;
using Argos.Contracts.Common;
using Argos.Contracts.Inventory;
using DomainAgentStatus = Argos.Domain.Inventory.Agents.AgentStatus;

namespace Argos.Application.Inventory.Agents;

/// <summary>
/// Consulta dos agentes (US-06, US-07, RF-013, RF-017; plan 15.8, 15.14). A pesquisa e os detalhes vêm de
/// <see cref="IAgentQueries"/>; as situações oferecidas ao usuário saem daqui (RN-12, D-59), para a Web não repetir a
/// regra.
/// </summary>
public sealed class AgentQueryService(IAgentQueries queries, ICurrentActor actor)
{
    internal const string NotFoundMessage = "Agente não encontrado.";

    /// <summary>
    /// Pesquisa paginada (US-06, RF-017). A ordenação é de uma lista fechada (<see cref="AgentSortFields"/>), e o filtro
    /// de ativos e inativos segue para a consulta (padrão: ativos; US-09, RN-35).
    /// </summary>
    public async Task<PagedResult<AgentSummary>> SearchAsync(AgentSearchQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var sort = PageValidation.ValidateSearch(query, AgentSortFields.All, AgentSortFields.Default, query.Activity);

        return await queries.SearchAgentsAsync(query with { Sort = sort }, cancellationToken);
    }

    /// <exception cref="NotFoundException">Agente inexistente.</exception>
    public async Task<AgentDetails> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var details = await queries.GetAgentDetailsAsync(id, cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);

        return details with { AllowedStatusChanges = AllowedStatusChanges(details) };
    }

    /// <summary>
    /// RN-12: qualquer situação diferente da atual, na ordem do Domínio. Nenhuma em cadastro inativo (RN-35) nem para
    /// quem não tem <c>resources.status.change</c> (plan 15.14, a mesma política do equipamento).
    /// </summary>
    private IReadOnlyList<AgentStatus> AllowedStatusChanges(AgentDetails details)
    {
        if (!details.IsActive || !EquipmentPermissions.CanChangeStatus(actor))
        {
            return [];
        }

        var current = EnumNames.Convert<DomainAgentStatus>(details.Status);
        return Enum.GetValues<DomainAgentStatus>()
            .Where(status => status != current)
            .Select(status => EnumNames.Convert<AgentStatus>(status))
            .ToArray();
    }
}
