namespace Argos.Simulation.Models;

/// <summary>
/// Sinal de vida gerado: número de sequência (a partir de 1) e atraso simulado antes do envio.
/// </summary>
public sealed record SimulatedHeartbeat(long Sequence, TimeSpan Jitter);
