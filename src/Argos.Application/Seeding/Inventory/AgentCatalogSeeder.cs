using Argos.Application.Common.Abstractions;
using Argos.Application.Inventory.Agents;
using Argos.Application.Inventory.Equipments;
using Argos.Application.Inventory.Support;
using Argos.Application.Inventory.Types;
using Argos.Domain.Common;
using Argos.Domain.Inventory;
using Argos.Domain.Inventory.Agents;
using Argos.Domain.Inventory.Equipments;
using Argos.Domain.Inventory.Support;

namespace Argos.Application.Seeding.Inventory;

/// <summary>
/// Carga <c>inventory-agents-v1</c> (Fase 3; D-56; plan 15.11.3, 15.11.4): os agentes, todos Fora de serviço (RN-12),
/// e os vínculos com motos e viaturas, pelas mesmas operações do Domínio dos casos de uso (<c>Agent.Register</c> e
/// <see cref="AssignmentPolicy.Assign"/>, D-62), com os instantes relativos à âncora e o autor <c>simulação</c>
/// (Art. IX.2). Tudo, com o registro da versão, é gravado num só <c>SaveChanges</c>; rodar de novo com a mesma semente
/// não grava nada. Entra num banco que já tem o catálogo da Fase 2, sem tocar nele além dos vínculos.
/// <para>
/// Um vínculo que a política recusa por causa do estado atual do equipamento (por exemplo, uma viatura que alguém
/// passou a Desativado antes desta carga, D-62) ou cujo equipamento não existe mais com aquela marca e número de série
/// não é criado e entra na contagem <c>assignmentsSkipped</c>, para a carga não travar a subida de um volume já usado.
/// Num volume sem alterações manuais, com a semente 42, nenhum é pulado.
/// </para>
/// </summary>
public sealed class AgentCatalogSeeder(
    IOrganizationRepository organizations,
    IOperationalBaseRepository bases,
    IAgentRepository agents,
    IAgentAssignmentRepository assignments,
    IEquipmentRepository equipments,
    IEquipmentTypeRepository types,
    ISeedLedger ledger,
    IUnitOfWork unitOfWork)
{
    /// <param name="seed">Semente da simulação, registrada com a versão (plan 7.4).</param>
    /// <param name="anchor">Data-âncora da simulação (D-03).</param>
    /// <exception cref="InvalidOperationException">
    /// Catálogo de equipamentos ainda não aplicado, órgão ou base inexistentes, ou versão aplicada com outra semente.
    /// </exception>
    public async Task<SeedResult> SeedAsync(
        AgentCatalogSeed catalog, int seed, DateTimeOffset anchor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (!await InventorySeed.ShouldApplyAsync(ledger, InventorySeed.AgentsVersion, seed, cancellationToken))
        {
            return SeedResult.Skipped(InventorySeed.AgentsVersion);
        }

        if (await ledger.FindAppliedSeedAsync(InventorySeed.CatalogVersion, cancellationToken) is null)
        {
            throw new InvalidOperationException(
                $"Aplique a carga {InventorySeed.CatalogVersion} antes de {InventorySeed.AgentsVersion}.");
        }

        var references = new SeedReferences(organizations, bases);
        var created = new Dictionary<(Guid OrganizationId, string RegistrationKey), Agent>();

        foreach (var item in catalog.Agents)
        {
            var organization = await references.OrganizationAsync(item.OrganizationAcronym, cancellationToken);
            var operationalBase = await references.BaseAsync(organization, item.BaseName, cancellationToken);

            var agent = Agent.Register(
                item.FullName,
                item.DisplayName,
                item.RegistrationNumber,
                item.Role,
                organization,
                item.Rank,
                operationalBase,
                anchor - item.RegisteredBeforeAnchor,
                InventorySeed.Author);

            agents.Add(agent);
            created[(organization.Id, agent.RegistrationKey)] = agent;
        }

        var openByAgent = new Dictionary<Guid, AgentAssignment>();
        var assigned = 0;
        var skipped = 0;

        foreach (var item in catalog.Assignments)
        {
            var organization = await references.OrganizationAsync(item.AgentOrganizationAcronym, cancellationToken);
            var agent = created.GetValueOrDefault((organization.Id, TextNormalization.ToKey(item.AgentRegistrationNumber)))
                ?? throw new InvalidOperationException(
                    $"O vínculo cita o agente {item.AgentRegistrationNumber} ({item.AgentOrganizationAcronym}), que não está na carga.");

            var equipment = await FindEquipmentAsync(item.EquipmentBrand, item.EquipmentSerialNumber, cancellationToken);
            if (equipment is null)
            {
                skipped++;
                continue;
            }

            var type = await types.FindByIdAsync(equipment.TypeId, cancellationToken)
                ?? throw new InvalidOperationException($"Tipo {equipment.TypeId} do equipamento {equipment.Id} não encontrado.");

            try
            {
                var change = AssignmentPolicy.Assign(
                    agent,
                    equipment,
                    type,
                    openByAgent.GetValueOrDefault(agent.Id),
                    anchor - item.StartedBeforeAnchor,
                    InventorySeed.Author);
                assignments.Add(change.Started);
                openByAgent[agent.Id] = change.Started;
                assigned++;
            }
            catch (InventoryRuleException)
            {
                skipped++;
            }
        }

        ledger.Record(InventorySeed.AgentsVersion, seed);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SeedResult(
            InventorySeed.AgentsVersion,
            false,
            new Dictionary<string, int>
            {
                ["agents"] = created.Count,
                ["assignments"] = assigned,
                ["assignmentsSkipped"] = skipped,
            });
    }

    /// <summary>Equipamento pela marca e número de série (RN-01), com as manutenções abertas; nulo se não existir.</summary>
    private async Task<Equipment?> FindEquipmentAsync(string brand, string serialNumber, CancellationToken cancellationToken) =>
        await equipments.FindIdByBrandAndSerialAsync(
                TextNormalization.ToKey(brand), TextNormalization.ToKey(serialNumber), cancellationToken) is { } id
            ? await equipments.FindByIdAsync(id, cancellationToken)
            : null;
}

/// <summary>Órgãos pela sigla e bases pelo nome, lidos uma vez cada durante a carga dos agentes.</summary>
internal sealed class SeedReferences(IOrganizationRepository organizations, IOperationalBaseRepository bases)
{
    private readonly Dictionary<string, Organization> _organizations = new(StringComparer.Ordinal);
    private readonly Dictionary<(Guid, string), OperationalBase> _bases = [];

    /// <exception cref="InvalidOperationException">Órgão inexistente: falta a carga de referência.</exception>
    public async Task<Organization> OrganizationAsync(string acronym, CancellationToken cancellationToken)
    {
        if (!_organizations.TryGetValue(acronym, out var organization))
        {
            organization = await organizations.FindByAcronymKeyAsync(TextNormalization.Normalize(acronym), cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Órgão \"{acronym}\" não existe; aplique antes a carga {InventorySeed.ReferenceVersion}.");
            _organizations[acronym] = organization;
        }

        return organization;
    }

    /// <exception cref="InvalidOperationException">Base inexistente no órgão: falta a carga de referência.</exception>
    public async Task<OperationalBase> BaseAsync(Organization organization, string name, CancellationToken cancellationToken)
    {
        if (!_bases.TryGetValue((organization.Id, name), out var operationalBase))
        {
            operationalBase = await bases.FindByNameKeyAsync(organization.Id, TextNormalization.Normalize(name), cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Base \"{name}\" do órgão {organization.Acronym} não existe; aplique antes a carga {InventorySeed.ReferenceVersion}.");
            _bases[(organization.Id, name)] = operationalBase;
        }

        return operationalBase;
    }
}
