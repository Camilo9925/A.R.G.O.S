using Argos.Application.Common;
using Argos.Application.Common.Abstractions;
using Argos.Application.Common.Exceptions;
using Argos.Application.Inventory.Support;
using Argos.Application.Inventory.Types;
using Argos.Contracts.Inventory;
using Argos.Domain.Inventory;
using Argos.Domain.Inventory.Equipments;
using Argos.Domain.Inventory.Geography;
using Argos.Domain.Inventory.Support;
using DomainEquipmentStatus = Argos.Domain.Inventory.Equipments.EquipmentStatus;

namespace Argos.Application.Inventory.Equipments;

/// <summary>
/// Cadastro e edição de equipamentos (US-01, US-02, RF-001 a RF-007, RN-01, RN-22; plan 15.2.6, 15.12). Os erros de
/// campo saem todos de uma vez, com o caminho do contrato (D-46); a duplicidade de marca e número de série aponta o
/// equipamento existente (RN-01), inclusive inativo. A inativação fica em <see cref="EquipmentInactivationService"/>,
/// porque também encerra os vínculos de agentes (RN-35; D-62).
/// </summary>
public sealed class EquipmentService(
    IEquipmentRepository equipments,
    IEquipmentTypeRepository types,
    IOrganizationRepository organizations,
    IOperationalBaseRepository bases,
    InventoryLocations locations,
    MunicipalCalendar calendar,
    InventoryReferenceNames referenceNames,
    EquipmentQueryService equipmentQueries,
    IInventoryChangeLog changeLog,
    IUnitOfWork unitOfWork,
    ICurrentActor actor,
    TimeProvider timeProvider)
{
    /// <summary>Mensagem da RN-01, a mesma quando a duplicidade é pega pelo índice único do banco.</summary>
    public const string DuplicateBrandAndSerialMessage = "Já existe um equipamento com essa marca e esse número de série.";

    /// <summary>Extensão do <c>ProblemDetails</c> com o id do equipamento existente (US-01; plan 15.7).</summary>
    public const string ExistingEquipmentIdExtension = "existingEquipmentId";

    private const string InitialMaintenanceField = "initialMaintenance";

    /// <summary>
    /// Situações aceitas no cadastro, na ordem do Domínio (D-66), para o formulário listar só essas sem repetir a regra
    /// (D-59).
    /// </summary>
    public static IReadOnlyList<Contracts.Inventory.EquipmentStatus> InitialStatuses() =>
        EquipmentStatusTransitions.InitialStatuses
            .Select(status => EnumNames.Convert<Contracts.Inventory.EquipmentStatus>(status))
            .ToArray();

    /// <summary>
    /// Cadastra o equipamento com o registro inicial de situação (US-01) e, se informada, a manutenção inicial (RF-002).
    /// Não gera registro cadastral de criação: o registro inicial de situação já o representa (plan 15.2.11).
    /// </summary>
    public async Task<EquipmentDetails> CreateAsync(CreateEquipmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new DomainErrorCollector();
        if (request.TypeId is null)
        {
            errors.Add("typeId", "Escolha o tipo do equipamento.");
        }

        if (request.Status is null)
        {
            errors.Add("status", "Escolha a situação inicial.");
        }

        errors.ThrowIfAny();

        var type = await types.FindByIdAsync(request.TypeId!.Value, cancellationToken)
            ?? throw new NotFoundException("Tipo de equipamento não encontrado.");
        var (organization, location, operationalBase) = await ReferencesAsync(request, errors, cancellationToken);
        var now = timeProvider.GetUtcNow();

        var equipment = errors.Try(
            () => Equipment.Register(
                type,
                request.SerialNumber,
                request.Brand,
                request.Model,
                organization,
                location,
                operationalBase,
                EquipmentMapping.AttributeInput(request.Attributes),
                EnumNames.Convert<DomainEquipmentStatus>(request.Status!.Value),
                now,
                actor.Name),
            skip: field => field == InventoryLocations.Field && errors.HasUnder(InventoryLocations.Field));
        errors.ThrowIfAny();

        await EnsureUniqueBrandAndSerialAsync(equipment!, cancellationToken);

        if (request.InitialMaintenance is { } initialMaintenance)
        {
            var today = await calendar.TodayAsync(cancellationToken);
            DomainFields.Under(
                InitialMaintenanceField,
                () => MaintenanceService.Register(equipment!, initialMaintenance, today, now, actor.Name));
        }

        equipments.Add(equipment!);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await equipmentQueries.GetDetailsAsync(equipment!.Id, cancellationToken);
    }

    /// <summary>
    /// Edita o cadastro (RF-001) com a versão lida pelo cliente (D-60). Sem alteração, nada é gravado; com alteração,
    /// grava o registro cadastral com os nomes dos cadastros de referência (D-49).
    /// </summary>
    public async Task<EquipmentDetails> UpdateAsync(
        Guid id, UpdateEquipmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var equipment = await equipments.FindByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(EquipmentQueryService.NotFoundMessage);

        if (request.Version is not { } version)
        {
            throw new ValidationFailedException("version", "Informe a versão do cadastro lida.");
        }

        if (equipments.VersionOf(equipment) != version)
        {
            throw new ConflictException(
                "O equipamento foi alterado por outra pessoa depois que você o abriu. Recarregue os dados e tente de novo.");
        }

        var type = await types.FindByIdAsync(equipment.TypeId, cancellationToken)
            ?? throw new InvalidOperationException($"Tipo {equipment.TypeId} do equipamento {id} não encontrado.");
        var errors = new DomainErrorCollector();
        var (organization, location, operationalBase) = await ReferencesAsync(request, errors, cancellationToken);

        var changes = errors.Try(
            () => equipment.UpdateRegistration(
                type,
                request.SerialNumber,
                request.Brand,
                request.Model,
                organization,
                location,
                operationalBase,
                EquipmentMapping.AttributeInput(request.Attributes)),
            skip: field => field == InventoryLocations.Field && errors.HasUnder(InventoryLocations.Field));
        errors.ThrowIfAny();

        if (changes!.Count > 0)
        {
            await EnsureUniqueBrandAndSerialAsync(equipment, cancellationToken);
            changeLog.Add(InventoryChange.Updated(
                InventoryEntityType.Equipment,
                id,
                await referenceNames.ResolveAsync(changes, cancellationToken),
                timeProvider.GetUtcNow(),
                actor.Name));
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return await equipmentQueries.GetDetailsAsync(id, cancellationToken);
    }

    /// <summary>
    /// Órgão, localização e base informados. Um id inexistente é 404 (plan 15.7); os erros de campo da localização
    /// ficam no coletor, para saírem junto com os do cadastro.
    /// </summary>
    private async Task<(Organization?, Location?, OperationalBase?)> ReferencesAsync(
        EquipmentRegistrationRequest request, DomainErrorCollector errors, CancellationToken cancellationToken)
    {
        var organization = request.OrganizationId is { } organizationId
            ? await organizations.FindByIdAsync(organizationId, cancellationToken)
                ?? throw new NotFoundException("Órgão não encontrado.")
            : null;

        var location = request.Location is { } input
            ? await errors.TryAsync(() => locations.BuildAsync(input, cancellationToken))
            : null;

        var operationalBase = request.BaseId is { } baseId
            ? await bases.FindByIdAsync(baseId, cancellationToken)
                ?? throw new NotFoundException("Base operacional não encontrada.")
            : null;

        return (organization, location, operationalBase);
    }

    /// <summary>RN-01: marca + número de série únicos entre todos, inclusive inativos; o 409 aponta o existente (US-01).</summary>
    private async Task EnsureUniqueBrandAndSerialAsync(Equipment equipment, CancellationToken cancellationToken)
    {
        var existingId = await equipments.FindIdByBrandAndSerialAsync(equipment.BrandKey, equipment.SerialKey, cancellationToken);

        if (existingId is { } id && id != equipment.Id)
        {
            throw new ConflictException(
                DuplicateBrandAndSerialMessage,
                new Dictionary<string, object?> { [ExistingEquipmentIdExtension] = id });
        }
    }
}
