namespace Argos.Domain.Inventory.Agents;

/// <summary>Função do agente (RF-014).</summary>
public enum AgentRole
{
    /// <summary>Agente de trânsito.</summary>
    TrafficAgent,

    /// <summary>Guarda municipal.</summary>
    MunicipalGuard,

    /// <summary>Policial militar.</summary>
    MilitaryPolice,
}

/// <summary>Rótulos em pt-BR das funções, usados nas mensagens e no registro cadastral (Art. XIII.2; D-49).</summary>
public static class AgentRoleLabels
{
    public static string For(AgentRole role) => role switch
    {
        AgentRole.TrafficAgent => "Agente de trânsito",
        AgentRole.MunicipalGuard => "Guarda municipal",
        AgentRole.MilitaryPolice => "Policial militar",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Função desconhecida."),
    };
}
