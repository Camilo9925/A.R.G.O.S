using Argos.Application.Common;
using Argos.Application.Common.Abstractions;
using Argos.Application.Common.Exceptions;
using Argos.Contracts.Inventory;
using Argos.Domain.Common;
using Argos.Domain.Inventory.Equipments;
using DomainMaintenanceKind = Argos.Domain.Inventory.Equipments.MaintenanceKind;
using DomainMaintenanceResult = Argos.Domain.Inventory.Equipments.MaintenanceResult;

namespace Argos.Application.Inventory.Equipments;

/// <summary>
/// Registro e conclusão de manutenções (US-05, RF-009, RN-07 a RN-11; plan 15.4). O "hoje" e a data da conclusão são os
/// do fuso do município (D-54). A existência da manutenção (404) é conferida aqui, porque o agregado só carrega as
/// abertas; o estado aberto (409) é conferido pelo Domínio.
/// </summary>
public sealed class MaintenanceService(
    IEquipmentRepository equipments,
    MunicipalCalendar calendar,
    EquipmentQueryService equipmentQueries,
    IUnitOfWork unitOfWork,
    ICurrentActor actor,
    TimeProvider timeProvider)
{
    public async Task<MaintenanceData> RegisterAsync(
        Guid equipmentId, RegisterMaintenanceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var equipment = await FindEquipmentAsync(equipmentId, cancellationToken);
        var (today, window) = await equipmentQueries.MaintenanceCalendarAsync(cancellationToken);

        var maintenance = Register(equipment, request, today, timeProvider.GetUtcNow(), actor.Name);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return EquipmentMapping.ToData(maintenance, today, window);
    }

    /// <summary>
    /// Conclui a manutenção (US-05): a situação segue a RN-11 e a periódica programa a próxima (RN-09), tudo gravado
    /// junto.
    /// </summary>
    public async Task<MaintenanceCompletionData> CompleteAsync(
        Guid equipmentId, Guid maintenanceId, CompleteMaintenanceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var equipment = await FindEquipmentAsync(equipmentId, cancellationToken);
        if (!await equipments.MaintenanceExistsAsync(equipmentId, maintenanceId, cancellationToken))
        {
            throw new NotFoundException("Manutenção não encontrada.");
        }

        var errors = new Dictionary<string, string[]>();
        if (request.Result is null)
        {
            errors["result"] = ["Informe se a manutenção teve sucesso."];
        }

        if (request.CompletedAt is null)
        {
            errors["completedAt"] = ["Informe quando a manutenção foi concluída."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException(errors);
        }

        var municipality = await calendar.GetMunicipalityAsync(cancellationToken);
        var completion = equipment.CompleteMaintenance(
            maintenanceId,
            EnumNames.Convert<DomainMaintenanceResult>(request.Result!.Value),
            request.CompletedAt!.Value,
            request.Notes,
            municipality.TimeZone,
            timeProvider.GetUtcNow(),
            actor.Name);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var (today, window) = await equipmentQueries.MaintenanceCalendarAsync(cancellationToken);
        return new MaintenanceCompletionData
        {
            Completed = EquipmentMapping.ToData(completion.Completed, today, window),
            Status = EnumNames.Convert<Contracts.Inventory.EquipmentStatus>(completion.Status),
            NextPeriodic = completion.NextPeriodic is { } next ? EquipmentMapping.ToData(next, today, window) : null,
        };
    }

    /// <summary>
    /// Registra a manutenção pedida no agregado (RN-08, RN-07, RN-11). Usado também pela manutenção inicial do
    /// cadastro, que põe os erros sob <c>initialMaintenance</c>.
    /// </summary>
    internal static Maintenance Register(
        Equipment equipment, RegisterMaintenanceRequest request, DateOnly today, DateTimeOffset at, string by)
    {
        if (request.Kind is not { } kind)
        {
            throw new DomainValidationException("kind", "Escolha o tipo da manutenção.");
        }

        return equipment.RegisterMaintenance(
            EnumNames.Convert<DomainMaintenanceKind>(kind),
            request.DueDate,
            request.PeriodicityDays,
            request.Description,
            today,
            at,
            by);
    }

    private async Task<Equipment> FindEquipmentAsync(Guid id, CancellationToken cancellationToken) =>
        await equipments.FindByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(EquipmentQueryService.NotFoundMessage);
}
