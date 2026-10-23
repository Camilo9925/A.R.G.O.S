using Argos.Domain.Inventory.Agents;

namespace Argos.Application.Inventory.Agents;

// Portas de persistência dos agentes e dos vínculos (plan 15.12). Sem remoção: agentes são inativados e vínculos são
// encerrados, nunca apagados (plan 5.4, RN-23).

/// <summary>Agentes, com o histórico de situação só recebendo registros novos (Art. V.1).</summary>
public interface IAgentRepository
{
    /// <summary>Agente pelo id, inclusive inativo.</summary>
    Task<Agent?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Id do agente do órgão com a mesma matrícula, pela chave normalizada (<see cref="Agent.RegistrationKey"/>),
    /// entre todos, inclusive os inativos (US-06). O índice único do banco é a garantia final.
    /// </summary>
    Task<Guid?> FindIdByRegistrationAsync(
        Guid organizationId, string registrationKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Versão do cadastro carregado (D-60), comparada pela Aplicação com a que o cliente leu, como no equipamento.
    /// </summary>
    uint VersionOf(Agent agent);

    void Add(Agent agent);
}

/// <summary>Vínculos entre agentes e equipamentos móveis (RF-016, US-07; D-62; plan 15.2.10).</summary>
public interface IAgentAssignmentRepository
{
    /// <summary>Vínculo pelo id, aberto ou encerrado.</summary>
    Task<AgentAssignment?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>O vínculo aberto do agente, se houver; há no máximo um (índice único parcial no banco).</summary>
    Task<AgentAssignment?> FindOpenByAgentAsync(Guid agentId, CancellationToken cancellationToken = default);

    /// <summary>Os vínculos abertos com o equipamento (nenhum, um ou vários; US-07).</summary>
    Task<IReadOnlyList<AgentAssignment>> ListOpenByEquipmentAsync(
        Guid equipmentId, CancellationToken cancellationToken = default);

    void Add(AgentAssignment assignment);
}
