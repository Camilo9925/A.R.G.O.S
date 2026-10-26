using Argos.Domain.Inventory.Equipments;

namespace Argos.Domain.Operations;

/// <summary>
/// Pior situação de um agrupamento no mapa (RF-030; A-28; D-82; plan 16.3): quanto menor o número, mais grave. Vai em
/// cada marcador como <c>severityRank</c>, e o mapa só toma o menor número do grupo, sem repetir a regra.
/// </summary>
public static class EquipmentStatusSeverity
{
    /// <summary>Agente em atividade conta como Operacional (D-82).</summary>
    public const int AgentInActivityRank = 6;

    /// <summary>Furtado 1, Mau funcionamento 2, Em manutenção 3, Desativado 4, Ainda não instalado 5, Operacional 6.</summary>
    public static int Rank(EquipmentStatus status) => status switch
    {
        EquipmentStatus.Stolen => 1,
        EquipmentStatus.Malfunction => 2,
        EquipmentStatus.UnderMaintenance => 3,
        EquipmentStatus.Deactivated => 4,
        EquipmentStatus.NotInstalled => 5,
        EquipmentStatus.Operational => 6,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Situação desconhecida."),
    };
}
