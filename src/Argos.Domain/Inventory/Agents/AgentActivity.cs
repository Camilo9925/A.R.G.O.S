namespace Argos.Domain.Inventory.Agents;

/// <summary>
/// RN-13: agente em atividade é o que está Em serviço ou Em atendimento. Usada na ingestão (posição aceita ou
/// descartada, Art. VIII.2), no retrato e nos indicadores da Central (plan 16.3). Não confundir com agente ativo
/// (cadastro não inativado, RN-35).
/// </summary>
public static class AgentActivity
{
    public static bool IsInActivity(AgentStatus status) => status is AgentStatus.OnDuty or AgentStatus.Responding;
}
