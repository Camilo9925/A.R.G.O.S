namespace Argos.Domain.Inventory.Agents;

/// <summary>Situação de um agente (RN-12). A inicial é <see cref="OffDuty"/> (US-06).</summary>
public enum AgentStatus
{
    /// <summary>Em serviço.</summary>
    OnDuty,

    /// <summary>Em atendimento.</summary>
    Responding,

    /// <summary>Em pausa.</summary>
    OnBreak,

    /// <summary>Fora de serviço.</summary>
    OffDuty,

    /// <summary>Afastado.</summary>
    OnLeave,
}

/// <summary>De onde veio uma mudança de situação do agente (plan 15.2.9, 16.2).</summary>
public enum AgentStatusChangeOrigin
{
    /// <summary>Registro inicial, no cadastro (US-06).</summary>
    Registration,

    /// <summary>Mudança feita por uma pessoa (RN-12).</summary>
    Manual,

    /// <summary>Situação reportada pelo PROMETHEUS no lote de telemetria (RF-023, RN-12; D-71).</summary>
    Telemetry,
}

/// <summary>Rótulos em pt-BR das situações do agente, usados nas mensagens do Domínio (Art. XIII.2).</summary>
public static class AgentStatusLabels
{
    public static string For(AgentStatus status) => status switch
    {
        AgentStatus.OnDuty => "Em serviço",
        AgentStatus.Responding => "Em atendimento",
        AgentStatus.OnBreak => "Em pausa",
        AgentStatus.OffDuty => "Fora de serviço",
        AgentStatus.OnLeave => "Afastado",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Situação desconhecida."),
    };
}
