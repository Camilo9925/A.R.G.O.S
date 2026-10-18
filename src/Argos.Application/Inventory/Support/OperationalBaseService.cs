using Argos.Application.Common.Abstractions;
using Argos.Application.Common.Exceptions;
using Argos.Application.Inventory.Equipments;
using Argos.Contracts.Inventory;
using Argos.Domain.Common;
using Argos.Domain.Inventory;
using Argos.Domain.Inventory.Geography;
using Argos.Domain.Inventory.Support;

namespace Argos.Application.Inventory.Support;

/// <summary>
/// Cadastro de bases operacionais (RF-007, RF-019; D-57; plan 15.2.3): nome único por órgão, órgão ativo e
/// localização válida (RN-02, RN-03), com os erros da localização em <c>location.*</c> (D-46).
/// </summary>
public sealed class OperationalBaseService(
    IOperationalBaseRepository bases,
    IOrganizationRepository organizations,
    IEquipmentRepository equipments,
    InventoryLocations locations,
    InventoryReferenceNames referenceNames,
    IInventoryQueries queries,
    IInventoryChangeLog changeLog,
    IUnitOfWork unitOfWork,
    ICurrentActor actor,
    TimeProvider timeProvider)
{
    public Task<IReadOnlyList<OperationalBaseData>> ListAsync(
        Guid? organizationId, bool includeInactive, CancellationToken cancellationToken = default) =>
        queries.ListOperationalBasesAsync(organizationId, includeInactive, cancellationToken);

    public async Task<OperationalBaseData> CreateAsync(
        OperationalBaseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var organization = await FindOrganizationAsync(request.OrganizationId, cancellationToken);
        var location = await BuildLocationAsync(request.Location, cancellationToken);
        var operationalBase = OperationalBase.Create(request.Name, organization, location);
        await EnsureUniqueNameAsync(operationalBase, cancellationToken);

        bases.Add(operationalBase);
        changeLog.Add(InventoryChange.Created(
            InventoryEntityType.OperationalBase, operationalBase.Id, timeProvider.GetUtcNow(), actor.Name));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAsync(operationalBase.Id, cancellationToken);
    }

    public async Task<OperationalBaseData> UpdateAsync(
        Guid id, OperationalBaseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var operationalBase = await FindAsync(id, cancellationToken);
        var organization = await FindOrganizationAsync(request.OrganizationId, cancellationToken);
        var location = await BuildLocationAsync(request.Location, cancellationToken);
        var changes = operationalBase.Update(request.Name, organization, location);

        if (changes.Count > 0)
        {
            await EnsureUniqueNameAsync(operationalBase, cancellationToken);
            changeLog.Add(InventoryChange.Updated(
                InventoryEntityType.OperationalBase,
                id,
                await referenceNames.ResolveAsync(changes, cancellationToken),
                timeProvider.GetUtcNow(),
                actor.Name));
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Inativa a base, recusada se houver equipamento ativo nela (plan 15.2.3; agentes na Fase 3).</summary>
    public async Task InactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var operationalBase = await FindAsync(id, cancellationToken);
        var now = timeProvider.GetUtcNow();

        operationalBase.Inactivate(await equipments.AnyActiveInBaseAsync(id, cancellationToken), now, actor.Name);
        changeLog.Add(InventoryChange.Inactivated(InventoryEntityType.OperationalBase, id, null, now, actor.Name));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Location> BuildLocationAsync(
        LocationInput? input, CancellationToken cancellationToken) =>
        input is null
            ? throw new DomainValidationException(InventoryLocations.Field, "Informe a localização da base.")
            : await locations.BuildAsync(input, cancellationToken);

    /// <summary>Órgão informado, ou nulo para que o Domínio aponte o campo (RN-22).</summary>
    private async Task<Organization?> FindOrganizationAsync(Guid? organizationId, CancellationToken cancellationToken) =>
        organizationId is { } id
            ? await organizations.FindByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Órgão não encontrado.")
            : null;

    private async Task<OperationalBase> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await bases.FindByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Base operacional não encontrada.");

    private async Task<OperationalBaseData> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await queries.GetOperationalBaseAsync(id, cancellationToken)
            ?? throw new NotFoundException("Base operacional não encontrada.");

    private async Task EnsureUniqueNameAsync(OperationalBase operationalBase, CancellationToken cancellationToken)
    {
        var existing = await bases.FindByNameKeyAsync(operationalBase.OrganizationId, operationalBase.NameKey, cancellationToken);
        if (existing is not null && existing.Id != operationalBase.Id)
        {
            throw new ConflictException("Já existe uma base com esse nome neste órgão.");
        }
    }
}
