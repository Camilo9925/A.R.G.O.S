using Argos.Application.Common;
using Argos.Application.Common.Abstractions;
using Argos.Application.Common.Exceptions;
using Argos.Contracts.Inventory;
using Argos.Domain.Inventory.Equipments;
using DomainEquipmentStatus = Argos.Domain.Inventory.Equipments.EquipmentStatus;

namespace Argos.Application.Inventory.Equipments;

/// <summary>
/// Mudança de situação de equipamento (US-04, RF-008, RN-05 a RN-07, RN-11; plan 15.3). A tabela de transições, a
/// justificativa e os efeitos ficam no Domínio; aqui se decide se o autor pode reativar (D-47) e se traduz a recusa
/// do Domínio em 403.
/// </summary>
public sealed class EquipmentStatusService(
    IEquipmentRepository equipments,
    EquipmentQueryService equipmentQueries,
    IUnitOfWork unitOfWork,
    ICurrentActor actor,
    TimeProvider timeProvider)
{
    /// <exception cref="ForbiddenOperationException">Reativação por quem não tem <c>resources.reactivate</c>.</exception>
    public async Task<EquipmentDetails> ChangeAsync(
        Guid id, ChangeEquipmentStatusRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var equipment = await equipments.FindByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(EquipmentQueryService.NotFoundMessage);

        if (request.Status is not { } status)
        {
            throw new ValidationFailedException("status", "Escolha a nova situação.");
        }

        try
        {
            equipment.ChangeStatus(
                EnumNames.Convert<DomainEquipmentStatus>(status),
                request.Justification,
                EquipmentPermissions.CanReactivate(actor),
                timeProvider.GetUtcNow(),
                actor.Name);
        }
        catch (ReactivationNotAllowedException exception)
        {
            throw new ForbiddenOperationException(exception.Message);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await equipmentQueries.GetDetailsAsync(id, cancellationToken);
    }
}
