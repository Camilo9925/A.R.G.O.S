using Argos.Domain.Inventory.Equipments;

namespace Argos.Domain.Operations.Alerts;

/// <summary>Causa e severidade de um alerta a abrir.</summary>
public sealed record AlertDefinition(AlertCause Cause, AlertSeverity Severity);

/// <summary>
/// RN-18 (parcial, spec 4.3; D-77): que alerta uma mudança de situação de equipamento gera. Furtado → equipamento
/// furtado, Crítica; Mau funcionamento → equipamento em mau funcionamento, Alta; qualquer outro destino, nenhum.
/// </summary>
public static class AlertCatalog
{
    /// <param name="from">Situação anterior; nula no cadastro (D-66).</param>
    /// <param name="to">Situação nova.</param>
    /// <returns>O alerta a abrir ou a atualizar (RN-19), ou nulo quando a mudança não gera alerta.</returns>
    public static AlertDefinition? ForStatusChange(EquipmentStatus? from, EquipmentStatus to)
    {
        if (from == to)
        {
            return null;
        }

        return to switch
        {
            EquipmentStatus.Stolen => new AlertDefinition(AlertCause.EquipmentStolen, AlertSeverity.Critical),
            EquipmentStatus.Malfunction => new AlertDefinition(AlertCause.EquipmentMalfunction, AlertSeverity.High),
            _ => null,
        };
    }
}
