using Argos.Simulation.Configuration;

namespace Argos.Simulation.Generators;

/// <summary>
/// Gera a sequência determinística de sinais de vida técnicos da Fase 0 a partir da semente (Art. IX.3).
/// Cada sinal traz um pequeno atraso de rede simulado, para que o envio não seja perfeitamente regular.
/// </summary>
public sealed class TechnicalHeartbeatGenerator
{
    private static readonly TimeSpan MaxJitter = TimeSpan.FromMilliseconds(500);

    private readonly Random _random;
    private long _sequence;

    public TechnicalHeartbeatGenerator(SimulationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _random = new Random(options.Seed);
    }

    public SimulatedHeartbeat Next()
    {
        _sequence++;
        var jitter = TimeSpan.FromMilliseconds(_random.Next((int)MaxJitter.TotalMilliseconds));
        return new SimulatedHeartbeat(_sequence, jitter);
    }
}

/// <summary>
/// Sinal de vida gerado: número de sequência (a partir de 1) e atraso simulado antes do envio.
/// </summary>
public sealed record SimulatedHeartbeat(long Sequence, TimeSpan Jitter);
