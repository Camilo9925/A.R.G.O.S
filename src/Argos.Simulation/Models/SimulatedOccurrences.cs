namespace Argos.Simulation.Models;

/// <summary>Tipos de ocorrência simulada, com os mesmos nomes do enum do Domínio (A-32; plan 17.2), na ordem do sorteio.</summary>
public enum SimulatedOccurrenceKind
{
    TrafficAccident,
    TheftOrRobbery,
    Vandalism,
    Congestion,
    SevereTrafficViolation,
}

/// <summary>
/// Uma ocorrência simulada (Art. IX.2): o instante, o bairro e o ponto sobre um logradouro da região, que o gerador
/// aceita só se estiver dentro do polígono dela (Art. IX.4; plan 17.3). A carga cria a ocorrência pelas operações do
/// Domínio, com a descrição e o autor <c>simulação</c>.
/// </summary>
public sealed record SimulatedOccurrence(
    SimulatedOccurrenceKind Kind,
    DateTimeOffset OccurredAt,
    string RegionName,
    string NeighborhoodName,
    string Street,
    double Latitude,
    double Longitude);
