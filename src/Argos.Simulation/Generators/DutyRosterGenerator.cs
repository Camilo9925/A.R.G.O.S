using Argos.Simulation.Models;

namespace Argos.Simulation.Generators;

/// <summary>Agente ativo para a escala, com o veículo do vínculo aberto, se houver.</summary>
/// <param name="VehicleKey">Marca + "/" + número de série da viatura ou moto vinculada (RN-01); nulo sem vínculo.</param>
public sealed record RosterAgent(string OrganizationAcronym, string RegistrationNumber, string? VehicleKey);

/// <summary>Turno atribuído a um agente.</summary>
public sealed record RosterEntry(string OrganizationAcronym, string RegistrationNumber, SimulatedDutyShift Shift);

/// <summary>
/// Escala de referência simulada (A-22; D-75 revista, D-94; plan 16.5). Função pura de (semente, agentes ativos com os
/// vínculos) → turno de cada agente, rodada só pelo Migrator (carga <c>operations-duty-roster-v1</c>).
/// <para>
/// <b>Unidades:</b> a tripulação inteira de cada veículo vinculado e cada agente sem vínculo. Dentro de cada órgão, as
/// unidades ordenadas pela chave (veículo: marca + número de série; agente: matrícula) recebem os turnos em rodízio,
/// começando no turno <c>semente mod 3</c>; assim os turnos ficam equilibrados por órgão (no máximo uma unidade de
/// diferença) e a tripulação fica no turno do veículo.
/// </para>
/// </summary>
public static class DutyRosterGenerator
{
    private static readonly SimulatedDutyShift[] Shifts = Enum.GetValues<SimulatedDutyShift>();

    public static IReadOnlyList<RosterEntry> Generate(int seed, IEnumerable<RosterAgent> agents)
    {
        ArgumentNullException.ThrowIfNull(agents);

        var first = ((seed % Shifts.Length) + Shifts.Length) % Shifts.Length;

        // A unidade de um veículo pertence ao órgão da tripulação; se a tripulação misturar órgãos, ao primeiro em
        // ordem alfabética, para o resultado não depender da ordem de entrada.
        var units = agents
            .GroupBy(agent => agent.VehicleKey is { } vehicle ? $"vehicle:{vehicle}" : $"agent:{agent.OrganizationAcronym}/{agent.RegistrationNumber}")
            .Select(unit => new
            {
                Organization = unit.Select(agent => agent.OrganizationAcronym).Min(StringComparer.Ordinal)!,
                Key = unit.First().VehicleKey ?? unit.First().RegistrationNumber,
                Members = unit.ToArray(),
            });

        return units
            .GroupBy(unit => unit.Organization, StringComparer.Ordinal)
            .OrderBy(organization => organization.Key, StringComparer.Ordinal)
            .SelectMany(organization => organization
                .OrderBy(unit => unit.Key, StringComparer.Ordinal)
                .SelectMany((unit, position) => unit.Members
                    .OrderBy(member => member.RegistrationNumber, StringComparer.Ordinal)
                    .Select(member => new RosterEntry(
                        member.OrganizationAcronym, member.RegistrationNumber, Shifts[(first + position) % Shifts.Length]))))
            .ToArray();
    }
}
