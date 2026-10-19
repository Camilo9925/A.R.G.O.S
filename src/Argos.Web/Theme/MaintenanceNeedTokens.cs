using Argos.Contracts.Inventory;
using MudBlazor;

namespace Argos.Web.Theme;

/// <summary>
/// Aparência da necessidade de manutenção (RF-002, RN-10): cor sempre com ícone e texto (Art. X.2, XI.2). A
/// necessidade vem pronta da API (D-54); aqui só se escolhe como mostrá-la.
/// </summary>
public static class MaintenanceNeedTokens
{
    public static StatusToken None { get; } =
        new("Nenhuma", "#8A94A6", Icons.Material.Outlined.CheckCircle, IsOutlined: true);

    public static StatusToken Scheduled { get; } =
        new("Programada", ArgosTheme.ElectricCyan, Icons.Material.Filled.Event);

    public static StatusToken Upcoming { get; } =
        new("Próxima", "#F5C518", Icons.Material.Filled.Schedule);

    public static StatusToken Overdue { get; } =
        new("Vencida", ArgosTheme.Amber, Icons.Material.Filled.EventBusy);

    public static StatusToken Immediate { get; } =
        new("Imediata", "#E5484D", Icons.Material.Filled.PriorityHigh);

    /// <summary>Todas, da mais grave para a menos grave, para a legenda e o filtro.</summary>
    public static IReadOnlyList<(MaintenanceNeed Need, StatusToken Token)> All { get; } =
    [
        (MaintenanceNeed.Immediate, Immediate),
        (MaintenanceNeed.Overdue, Overdue),
        (MaintenanceNeed.Upcoming, Upcoming),
        (MaintenanceNeed.Scheduled, Scheduled),
        (MaintenanceNeed.None, None),
    ];

    public static StatusToken For(MaintenanceNeed need) => need switch
    {
        MaintenanceNeed.None => None,
        MaintenanceNeed.Scheduled => Scheduled,
        MaintenanceNeed.Upcoming => Upcoming,
        MaintenanceNeed.Overdue => Overdue,
        MaintenanceNeed.Immediate => Immediate,
        _ => throw new ArgumentOutOfRangeException(nameof(need), need, "Necessidade desconhecida."),
    };
}
