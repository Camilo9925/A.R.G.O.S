using Argos.Application.Seeding.Inventory;
using Argos.Application.Seeding.Operations;
using Argos.Domain.Operations;
using Argos.Simulation.Configuration;
using Argos.Simulation.Generators;
using Argos.Simulation.Geography;
using Argos.Simulation.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Argos.Migrator.Seeding;

/// <summary>
/// Carga da Central pelo Migrator (Fase 4; D-78, D-94; plan 16.9), depois das cargas do Inventário e nesta ordem:
/// <c>operations-duty-roster-v1</c> (escala de referência pelo <see cref="DutyRosterGenerator"/>) e
/// <c>operations-alerts-v1</c> (alertas dos equipamentos ativos em Mau funcionamento ou Furtado). Cada etapa roda num
/// escopo próprio e grava num só <c>SaveChanges</c>, com o seu registro no histórico do seed; entram num volume da
/// Fase 3 sem <c>down -v</c>.
/// </summary>
internal static class OperationsLoad
{
    public static async Task RunAsync(
        IServiceProvider services,
        SimulationOptions simulation,
        TimeProvider timeProvider,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var anchor = simulation.AnchorDate ?? timeProvider.GetUtcNow();

        await using (var scope = services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<DutyRosterSeeder>().SeedAsync(
                Roster(simulation.Seed), simulation.Seed, anchor, cancellationToken);
            InventoryLoad.Log(logger, result, simulation.Seed);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<OperationsAlertSeeder>().SeedAsync(
                simulation.Seed, cancellationToken);
            InventoryLoad.Log(logger, result, simulation.Seed);
        }
    }

    /// <summary>
    /// Escala de referência (plan 16.5): os agentes da carga que continuam ativos depois das inativações de
    /// demonstração, com o veículo do vínculo da carga (marca + "/" + número de série), em rodízio por órgão.
    /// </summary>
    internal static DutyRosterSeed Roster(int seed)
    {
        var catalog = InventoryCatalogGenerator.Generate(seed, PrometeiaGeography.Load());
        var agents = AgentCatalogGenerator.Generate(seed, catalog);

        var inactivated = agents.AgentInactivations
            .Select(item => (item.OrganizationAcronym, item.RegistrationNumber))
            .ToHashSet();
        var vehicles = agents.Assignments.ToDictionary(
            assignment => (assignment.AgentOrganizationAcronym, assignment.AgentRegistrationNumber),
            assignment => $"{assignment.EquipmentBrand}/{assignment.EquipmentSerialNumber}");

        var roster = DutyRosterGenerator.Generate(
            seed,
            agents.Agents
                .Where(agent => !inactivated.Contains((agent.OrganizationAcronym, agent.RegistrationNumber)))
                .Select(agent => new RosterAgent(
                    agent.OrganizationAcronym,
                    agent.RegistrationNumber,
                    vehicles.GetValueOrDefault((agent.OrganizationAcronym, agent.RegistrationNumber)))));

        return new DutyRosterSeed(roster
            .Select(entry => new DutyShiftSeed(entry.OrganizationAcronym, entry.RegistrationNumber, ToDomain(entry.Shift)))
            .ToArray());
    }

    private static DutyShift ToDomain(SimulatedDutyShift shift) => Enum.Parse<DutyShift>(shift.ToString());
}
