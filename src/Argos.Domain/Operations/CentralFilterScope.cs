namespace Argos.Domain.Operations;

/// <summary>
/// RN-36 (A-27; D-79; plan 16.3): quais grupos da Central entram na visão conforme os filtros escolhidos. Região, bairro
/// e órgão valem para os dois grupos; tipo, situação e necessidade de manutenção só para equipamentos; função só para
/// agentes. Só filtros de equipamento → agentes fora da visão; só filtros de agente → equipamentos fora; dos dois
/// tipos, ou nenhum deles → os dois grupos, cada um com os seus filtros.
/// </summary>
public sealed record CentralFilterScope(bool IncludeEquipment, bool IncludeAgents)
{
    /// <param name="hasEquipmentFilters">Se há filtro de tipo, situação ou necessidade de manutenção.</param>
    /// <param name="hasAgentFilters">Se há filtro de função do agente.</param>
    public static CentralFilterScope From(bool hasEquipmentFilters, bool hasAgentFilters) =>
        new(IncludeEquipment: hasEquipmentFilters || !hasAgentFilters, IncludeAgents: hasAgentFilters || !hasEquipmentFilters);
}
