using Argos.Contracts.Common;
using Argos.Contracts.Inventory;

namespace Argos.Application.Inventory.Agents;

/// <summary>
/// Consultas de leitura dos agentes, com projeção direta para os contratos (plan 5.1, 15.8, 15.12), como
/// <see cref="IInventoryQueries"/> faz com os equipamentos. A mesma consulta serve a todas as telas (Art. V.3).
/// </summary>
public interface IAgentQueries
{
    /// <summary>
    /// Pesquisa paginada (US-06, RF-017; plan 15.8): termo em nome, nome de exibição e matrícula; filtros por função,
    /// órgão, base, região (a do bairro da base, D-65), situação e ativos/inativos. A ordenação já chega conferida pela
    /// Aplicação e é estável (desempate por id).
    /// </summary>
    Task<PagedResult<AgentSummary>> SearchAgentsAsync(AgentSearchQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dados do agente com nomes do órgão, da base, do bairro e da região, instante da última mudança de situação,
    /// equipamento do vínculo aberto (US-07), versão e auditoria. As situações permitidas ficam com a Aplicação.
    /// Nulo se o agente não existe.
    /// </summary>
    Task<AgentDetails?> GetAgentDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> AgentExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pesquisa paginada dos equipamentos vinculáveis (US-07; D-62, D-68; plan 15.2.10, 15.13): ativos, móveis, de tipo
    /// com <c>CrewAssignable</c> e fora de <see cref="Argos.Domain.Inventory.Agents.AssignmentPolicy.StatusesRefusingNewAssignment"/>,
    /// lidas do Domínio, sem lista própria. Termo, ordenação, paginação e projeção como na pesquisa de equipamentos
    /// (<see cref="IInventoryQueries.SearchEquipmentAsync"/>), com a necessidade de manutenção calculada a partir do
    /// <paramref name="today"/> e da janela de manutenção próxima (D-54). A ordenação já chega conferida.
    /// </summary>
    Task<PagedResult<EquipmentSummary>> SearchAssignableEquipmentAsync(
        AssignableEquipmentQuery query, DateOnly today, int upcomingWindowDays, CancellationToken cancellationToken = default);
}
