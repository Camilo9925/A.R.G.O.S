using Argos.Contracts.Inventory;
using MudBlazor;

namespace Argos.Web.Theme;

/// <summary>
/// Aparência da função do agente no mapa (RF-028): ícone e texto, sempre no contorno ciano de seleção e interação
/// (Art. X.2, XI.1). A situação do agente continua em <see cref="AgentStatusTokens"/>.
/// </summary>
public static class AgentRoleTokens
{
    public static StatusToken TrafficAgent { get; } =
        new("Agente de trânsito", ArgosTheme.ElectricCyan, Icons.Material.Filled.Traffic);

    public static StatusToken MunicipalGuard { get; } =
        new("Guarda municipal", ArgosTheme.ElectricCyan, Icons.Material.Filled.Security);

    public static StatusToken MilitaryPolice { get; } =
        new("Policial militar", ArgosTheme.ElectricCyan, Icons.Material.Filled.LocalPolice);

    public static IReadOnlyList<(AgentRole Role, StatusToken Token)> All { get; } =
    [
        (AgentRole.TrafficAgent, TrafficAgent),
        (AgentRole.MunicipalGuard, MunicipalGuard),
        (AgentRole.MilitaryPolice, MilitaryPolice),
    ];

    public static StatusToken For(AgentRole role) => role switch
    {
        AgentRole.TrafficAgent => TrafficAgent,
        AgentRole.MunicipalGuard => MunicipalGuard,
        AgentRole.MilitaryPolice => MilitaryPolice,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Função desconhecida."),
    };
}
