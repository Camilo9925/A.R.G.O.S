namespace Argos.Domain.Inventory.Equipments;

/// <summary>
/// Tabela de transições da RN-05 e as regras de justificativa da RN-06 (plan 15.3). É a única fonte usada para validar
/// a mudança e para listar as opções na interface (D-59).
/// </summary>
public static class EquipmentStatusTransitions
{
    private static readonly IReadOnlyDictionary<EquipmentStatus, EquipmentStatus[]> Allowed =
        new Dictionary<EquipmentStatus, EquipmentStatus[]>
        {
            [EquipmentStatus.NotInstalled] = [EquipmentStatus.Operational, EquipmentStatus.Deactivated],
            [EquipmentStatus.Operational] =
                [EquipmentStatus.Malfunction, EquipmentStatus.UnderMaintenance, EquipmentStatus.Deactivated, EquipmentStatus.Stolen],
            [EquipmentStatus.Malfunction] =
                [EquipmentStatus.UnderMaintenance, EquipmentStatus.Operational, EquipmentStatus.Deactivated, EquipmentStatus.Stolen],
            [EquipmentStatus.UnderMaintenance] =
                [EquipmentStatus.Operational, EquipmentStatus.Malfunction, EquipmentStatus.Deactivated, EquipmentStatus.Stolen],
            [EquipmentStatus.Stolen] = [EquipmentStatus.UnderMaintenance, EquipmentStatus.Deactivated],
            [EquipmentStatus.Deactivated] = [EquipmentStatus.Operational],
        };

    /// <summary>Situações aceitas no cadastro (D-66): as demais exigem justificativa e não cabem num cadastro novo.</summary>
    public static IReadOnlyList<EquipmentStatus> InitialStatuses { get; } =
    [
        EquipmentStatus.NotInstalled,
        EquipmentStatus.Operational,
        EquipmentStatus.Malfunction,
        EquipmentStatus.UnderMaintenance,
    ];

    /// <summary>RN-05: a transição está na tabela. Ficar na mesma situação não é uma transição.</summary>
    public static bool IsAllowed(EquipmentStatus from, EquipmentStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>Destinos permitidos a partir de <paramref name="from"/>, na ordem da RN-05.</summary>
    public static IReadOnlyList<EquipmentStatus> TargetsFrom(EquipmentStatus from) =>
        Allowed.TryGetValue(from, out var targets) ? targets : [];

    /// <summary>Reativação: Desativado → Operacional, só pelo Administrador (RN-05; D-47).</summary>
    public static bool IsReactivation(EquipmentStatus from, EquipmentStatus to) =>
        from == EquipmentStatus.Deactivated && to == EquipmentStatus.Operational;

    /// <summary>RN-06: Furtado, Desativado e a reativação exigem justificativa.</summary>
    public static bool RequiresJustification(EquipmentStatus from, EquipmentStatus to) =>
        to is EquipmentStatus.Stolen or EquipmentStatus.Deactivated || IsReactivation(from, to);
}
