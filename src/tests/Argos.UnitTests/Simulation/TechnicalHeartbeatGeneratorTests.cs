using Argos.Simulation.Configuration;
using Argos.Simulation.Generators;
using Argos.Simulation.Models;

namespace Argos.UnitTests.Simulation;

/// <summary>
/// Reprodutibilidade da simulação: a mesma configuração, incluindo a semente, gera os mesmos dados (Art. IX.3).
/// </summary>
public sealed class TechnicalHeartbeatGeneratorTests
{
    private const int SampleSize = 100;

    [Fact]
    public void SameSeed_GeneratesSameSequence_ArtIX3()
    {
        var first = Generate(seed: 42);
        var second = Generate(seed: 42);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentSeeds_GenerateDifferentSequences_ArtIX3()
    {
        var first = Generate(seed: 42);
        var second = Generate(seed: 43);

        Assert.NotEqual(first, second);
    }

    private static SimulatedHeartbeat[] Generate(int seed)
    {
        var generator = new TechnicalHeartbeatGenerator(new SimulationOptions { Seed = seed });
        return Enumerable.Range(0, SampleSize).Select(_ => generator.Next()).ToArray();
    }
}
