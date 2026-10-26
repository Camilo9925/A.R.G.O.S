using Argos.Application.Common.Abstractions;
using Argos.Application.Inventory.Agents;
using Argos.Application.Inventory.Support;
using Argos.Application.Seeding.Inventory;
using Argos.Domain.Common;
using Argos.Domain.Operations;

namespace Argos.Application.Seeding.Operations;

/// <summary>
/// Carga <c>operations-duty-roster-v1</c> (Fase 4; A-22; D-75 revista, D-94; plan 16.5, 16.9): grava o turno de cada
/// agente da escala de referência simulada, calculada pelo Migrator com o <c>DutyRosterGenerator</c> sobre os agentes
/// ativos da carga e os vínculos deles. Autor <c>simulação</c> (Art. IX.2). Exige <c>inventory-inactivations-v1</c>,
/// para a escala partir só dos agentes que continuam ativos; tudo, com o registro da versão, num só
/// <c>SaveChanges</c>, e rodar de novo com a mesma semente não grava nada.
/// <para>
/// Um agente que não existe mais ou que foi inativado antes desta carga não recebe turno e entra na contagem
/// <c>shiftsSkipped</c>, como nas cargas do Inventário. Agente cadastrado depois da carga fica sem escala e não é posto
/// em serviço pelo Simulador (plan 16.2).
/// </para>
/// </summary>
public sealed class DutyRosterSeeder(
    IOrganizationRepository organizations,
    IOperationalBaseRepository bases,
    IAgentRepository agents,
    IOperationsSeedStore store,
    ISeedLedger ledger,
    IUnitOfWork unitOfWork)
{
    /// <param name="seed">Semente da simulação, registrada com a versão (plan 7.4).</param>
    /// <param name="assignedAt">Instante da atribuição: a data-âncora da carga (D-03).</param>
    /// <exception cref="InvalidOperationException">
    /// Inativações ainda não aplicadas, órgão inexistente ou versão aplicada com outra semente.
    /// </exception>
    public async Task<SeedResult> SeedAsync(
        DutyRosterSeed roster, int seed, DateTimeOffset assignedAt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roster);

        if (!await InventorySeed.ShouldApplyAsync(ledger, OperationsSeed.DutyRosterVersion, seed, cancellationToken))
        {
            return SeedResult.Skipped(OperationsSeed.DutyRosterVersion);
        }

        if (await ledger.FindAppliedSeedAsync(InventorySeed.InactivationsVersion, cancellationToken) is null)
        {
            throw new InvalidOperationException(
                $"Aplique a carga {InventorySeed.InactivationsVersion} antes de {OperationsSeed.DutyRosterVersion}.");
        }

        var references = new SeedReferences(organizations, bases);
        var counts = Enum.GetValues<DutyShift>().ToDictionary(shift => shift, _ => 0);
        var skipped = 0;

        foreach (var item in roster.Shifts)
        {
            var organization = await references.OrganizationAsync(item.OrganizationAcronym, cancellationToken);
            var id = await agents.FindIdByRegistrationAsync(
                organization.Id, TextNormalization.ToKey(item.RegistrationNumber), cancellationToken);
            var agent = id is { } found ? await agents.FindByIdAsync(found, cancellationToken) : null;

            if (agent is null || !agent.IsActive)
            {
                skipped++;
                continue;
            }

            store.AddDutyShift(AgentDutyShift.Assign(agent.Id, item.Shift, assignedAt, InventorySeed.Author));
            counts[item.Shift]++;
        }

        ledger.Record(OperationsSeed.DutyRosterVersion, seed);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SeedResult(
            OperationsSeed.DutyRosterVersion,
            false,
            new Dictionary<string, int>
            {
                ["morning"] = counts[DutyShift.Morning],
                ["afternoon"] = counts[DutyShift.Afternoon],
                ["night"] = counts[DutyShift.Night],
                ["shiftsSkipped"] = skipped,
            });
    }
}
