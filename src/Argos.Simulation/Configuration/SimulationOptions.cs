namespace Argos.Simulation.Configuration;

/// <summary>
/// Parâmetros que tornam a simulação reproduzível: a mesma semente e a mesma data-âncora geram os mesmos dados (Art. IX.3, D-03).
/// </summary>
public sealed class SimulationOptions
{
    public const string SectionName = "Simulation";

    public int Seed { get; set; } = 42;

    /// <summary>
    /// Instante em que o histórico simulado termina. Vazio significa "agora" (demonstração); fixo nos testes.
    /// </summary>
    public DateTimeOffset? AnchorDate { get; set; }
}
