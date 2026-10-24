using Argos.Application.Common.Abstractions;
using Argos.Application.Inventory;
using Argos.Application.Inventory.Agents;
using Argos.Application.Inventory.Equipments;
using Argos.Application.Inventory.Support;
using Argos.Domain.Common;
using Argos.Domain.Inventory;
using Argos.Domain.Inventory.Agents;

namespace Argos.Application.Seeding.Inventory;

/// <summary>
/// Carga <c>inventory-inactivations-v1</c> (Fase 3; D-56; plan 15.11.3, 15.11.4): as inativações de demonstração, para
/// o filtro "inativos" (US-09, RN-35). Equipamentos Desativados ou Furtados do catálogo são inativados pela
/// <see cref="AssignmentPolicy.InactivateEquipment"/> (manutenções abertas canceladas, vínculos encerrados); agentes
/// passam a Afastado (RN-12) e são inativados pela <see cref="AssignmentPolicy.InactivateAgent"/>. Cada inativação grava
/// o registro cadastral com o motivo (D-49), com o autor <c>simulação</c> (Art. IX.2). Tudo, com o registro da versão,
/// num só <c>SaveChanges</c>, depois da carga dos agentes; rodar de novo com a mesma semente não grava nada.
/// <para>
/// Uma inativação que a RN-35 recusa no estado atual (o equipamento saiu de Desativado ou Furtado antes desta carga,
/// por exemplo) ou cujo recurso não existe mais não é feita e entra na contagem <c>inactivationsSkipped</c>, como na
/// carga dos agentes. Num volume sem alterações manuais, com a semente 42, nenhuma é pulada.
/// </para>
/// </summary>
public sealed class InactivationSeeder(
    IOrganizationRepository organizations,
    IOperationalBaseRepository bases,
    IAgentRepository agents,
    IAgentAssignmentRepository assignments,
    IEquipmentRepository equipments,
    IInventoryChangeLog changeLog,
    ISeedLedger ledger,
    IUnitOfWork unitOfWork)
{
    /// <param name="seed">Semente da simulação, registrada com a versão (plan 7.4).</param>
    /// <param name="anchor">Data-âncora da simulação (D-03).</param>
    /// <exception cref="InvalidOperationException">
    /// Carga dos agentes ainda não aplicada, órgão inexistente ou versão aplicada com outra semente.
    /// </exception>
    public async Task<SeedResult> SeedAsync(
        InactivationSeed inactivations, int seed, DateTimeOffset anchor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inactivations);

        if (!await InventorySeed.ShouldApplyAsync(ledger, InventorySeed.InactivationsVersion, seed, cancellationToken))
        {
            return SeedResult.Skipped(InventorySeed.InactivationsVersion);
        }

        if (await ledger.FindAppliedSeedAsync(InventorySeed.AgentsVersion, cancellationToken) is null)
        {
            throw new InvalidOperationException(
                $"Aplique a carga {InventorySeed.AgentsVersion} antes de {InventorySeed.InactivationsVersion}.");
        }

        var equipmentCount = 0;
        var agentCount = 0;
        var skipped = 0;

        foreach (var item in inactivations.Equipment)
        {
            if (await InactivateEquipmentAsync(item, anchor, cancellationToken))
            {
                equipmentCount++;
            }
            else
            {
                skipped++;
            }
        }

        var references = new SeedReferences(organizations, bases);
        foreach (var item in inactivations.Agents)
        {
            if (await InactivateAgentAsync(item, references, anchor, cancellationToken))
            {
                agentCount++;
            }
            else
            {
                skipped++;
            }
        }

        ledger.Record(InventorySeed.InactivationsVersion, seed);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SeedResult(
            InventorySeed.InactivationsVersion,
            false,
            new Dictionary<string, int>
            {
                ["equipment"] = equipmentCount,
                ["agents"] = agentCount,
                ["inactivationsSkipped"] = skipped,
            });
    }

    private async Task<bool> InactivateEquipmentAsync(
        EquipmentInactivationSeed item, DateTimeOffset anchor, CancellationToken cancellationToken)
    {
        var id = await equipments.FindIdByBrandAndSerialAsync(
            TextNormalization.ToKey(item.Brand), TextNormalization.ToKey(item.SerialNumber), cancellationToken);
        var equipment = id is { } found ? await equipments.FindByIdAsync(found, cancellationToken) : null;
        if (equipment is null)
        {
            return false;
        }

        var openAssignments = await assignments.ListOpenByEquipmentAsync(equipment.Id, cancellationToken);

        try
        {
            var inactivation = AssignmentPolicy.InactivateEquipment(
                equipment, openAssignments, item.Reason, anchor - item.BeforeAnchor, InventorySeed.Author);
            changeLog.Add(inactivation.Change);
            return true;
        }
        catch (InventoryRuleException)
        {
            return false;
        }
    }

    private async Task<bool> InactivateAgentAsync(
        AgentInactivationSeed item, SeedReferences references, DateTimeOffset anchor, CancellationToken cancellationToken)
    {
        var organization = await references.OrganizationAsync(item.OrganizationAcronym, cancellationToken);
        var id = await agents.FindIdByRegistrationAsync(
            organization.Id, TextNormalization.ToKey(item.RegistrationNumber), cancellationToken);
        var agent = id is { } found ? await agents.FindByIdAsync(found, cancellationToken) : null;

        // Agente inexistente ou já inativo: não há o que inativar, e nada muda.
        if (agent is null || !agent.IsActive)
        {
            return false;
        }

        if (agent.Status != AgentStatus.OnLeave)
        {
            agent.ChangeStatus(AgentStatus.OnLeave, anchor - item.OnLeaveBeforeAnchor, InventorySeed.Author);
        }

        var openAssignment = await assignments.FindOpenByAgentAsync(agent.Id, cancellationToken);
        var inactivation = AssignmentPolicy.InactivateAgent(
            agent, openAssignment, item.Reason, anchor - item.InactivatedBeforeAnchor, InventorySeed.Author);
        changeLog.Add(inactivation.Change);
        return true;
    }
}
