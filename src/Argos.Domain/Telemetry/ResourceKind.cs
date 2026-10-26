namespace Argos.Domain.Telemetry;

/// <summary>
/// Tipo do recurso a que uma leitura, um estado atual ou um alerta se refere (plan 16.2). As tabelas da telemetria e
/// dos alertas apontam o recurso por tipo + id, sem chave estrangeira, como o registro cadastral.
/// </summary>
public enum ResourceKind
{
    Equipment,
    Agent,
}

/// <summary>O que uma leitura de telemetria traz (plan 16.2).</summary>
public enum TelemetryReadingKind
{
    /// <summary>Posição de um equipamento móvel ou de um agente em atividade.</summary>
    Position,

    /// <summary>Sinal de vida de um equipamento fixo, sem coordenadas.</summary>
    Heartbeat,
}
