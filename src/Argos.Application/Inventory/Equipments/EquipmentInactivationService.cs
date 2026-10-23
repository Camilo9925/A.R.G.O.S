using Argos.Application.Common.Abstractions;
using Argos.Application.Common.Exceptions;
using Argos.Application.Inventory.Agents;
using Argos.Contracts.Inventory;
using Argos.Domain.Inventory.Agents;

namespace Argos.Application.Inventory.Equipments;

/// <summary>
/// Inativação de equipamento (US-09, RF-001, RN-23, RN-35; D-49, D-62; plan 15.2.6, 15.2.10, 15.12). As regras ficam
/// no Domínio: a <see cref="AssignmentPolicy"/> chama <c>Equipment.Inactivate</c> (situações da RN-35, cancelamento
/// das manutenções abertas) e só então encerra os vínculos abertos dos agentes. Aqui se carrega o necessário e se
/// grava tudo, com o registro cadastral e o motivo, num único <c>SaveChanges</c>. Não existe exclusão (RN-23).
/// <para>
/// Fica fora do <see cref="EquipmentService"/> porque depende da porta dos vínculos, implementada pela
/// Infraestrutura da Fase 3: assim o cadastro e a edição continuam resolvíveis antes dela.
/// </para>
/// </summary>
public sealed class EquipmentInactivationService(
    IEquipmentRepository equipments,
    IAgentAssignmentRepository assignments,
    IInventoryChangeLog changeLog,
    IUnitOfWork unitOfWork,
    ICurrentActor actor,
    TimeProvider timeProvider)
{
    /// <exception cref="NotFoundException">Equipamento inexistente.</exception>
    /// <exception cref="Argos.Domain.Inventory.InventoryRuleException">Já inativo ou situação fora da RN-35 (409).</exception>
    /// <exception cref="Argos.Domain.Common.DomainValidationException">Motivo longo demais, no campo <c>reason</c>.</exception>
    public async Task InactivateAsync(Guid id, InactivationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var equipment = await equipments.FindByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(EquipmentQueryService.NotFoundMessage);
        var openAssignments = await assignments.ListOpenByEquipmentAsync(id, cancellationToken);

        var inactivation = AssignmentPolicy.InactivateEquipment(
            equipment, openAssignments, request.Reason, timeProvider.GetUtcNow(), actor.Name);

        changeLog.Add(inactivation.Change);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
