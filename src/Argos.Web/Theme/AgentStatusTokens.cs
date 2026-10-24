using Argos.Contracts.Inventory;
using MudBlazor;

namespace Argos.Web.Theme;

/// <summary>
/// Aparência da situação do agente (RN-12): cor sempre com ícone e texto (Art. X.2, XI.2). As cores seguem a mesma
/// lógica semântica das situações de equipamento (verde em operação, amarelo em pausa, cinza fora de operação).
/// </summary>
public static class AgentStatusTokens
{
    public static StatusToken OnDuty { get; } =
        new("Em serviço", "#2EBD59", Icons.Material.Filled.CheckCircle);

    public static StatusToken Responding { get; } =
        new("Em atendimento", ArgosTheme.ElectricCyan, Icons.Material.Filled.NotificationsActive);

    public static StatusToken OnBreak { get; } =
        new("Em pausa", "#F5C518", Icons.Material.Filled.PauseCircle);

    public static StatusToken OffDuty { get; } =
        new("Fora de serviço", "#8A94A6", Icons.Material.Outlined.DoNotDisturbOn, IsOutlined: true);

    public static StatusToken OnLeave { get; } =
        new("Afastado", "#9B5DE5", Icons.Material.Filled.PersonOff);

    /// <summary>Todas, na ordem de uso (da mais ativa à menos), para a legenda e o filtro.</summary>
    public static IReadOnlyList<(AgentStatus Status, StatusToken Token)> All { get; } =
    [
        (AgentStatus.OnDuty, OnDuty),
        (AgentStatus.Responding, Responding),
        (AgentStatus.OnBreak, OnBreak),
        (AgentStatus.OffDuty, OffDuty),
        (AgentStatus.OnLeave, OnLeave),
    ];

    public static StatusToken For(AgentStatus status) => status switch
    {
        AgentStatus.OnDuty => OnDuty,
        AgentStatus.Responding => Responding,
        AgentStatus.OnBreak => OnBreak,
        AgentStatus.OffDuty => OffDuty,
        AgentStatus.OnLeave => OnLeave,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Situação de agente desconhecida."),
    };
}
