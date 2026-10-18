using Argos.Application.Common.Abstractions;
using Argos.Application.Common.Exceptions;
using Argos.Contracts.Inventory;
using Argos.Domain.Inventory;
using Argos.Domain.Inventory.Support;

namespace Argos.Application.Inventory.Support;

/// <summary>
/// Cadastro de órgãos responsáveis (RF-019, RN-22; plan 15.2.2). Nome e sigla únicos sem diferença de maiúsculas e
/// acentos, inclusive entre inativos (D-53). Um órgão inativo não recebe novos cadastros; os existentes continuam
/// ligados a ele.
/// </summary>
public sealed class OrganizationService(
    IOrganizationRepository organizations,
    IInventoryQueries queries,
    IInventoryChangeLog changeLog,
    IUnitOfWork unitOfWork,
    ICurrentActor actor,
    TimeProvider timeProvider)
{
    public Task<IReadOnlyList<OrganizationData>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default) =>
        queries.ListOrganizationsAsync(includeInactive, cancellationToken);

    /// <summary>Um cadastro pelo id, ativo ou não; inexistente é 404.</summary>
    public async Task<OrganizationData> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await queries.GetOrganizationAsync(id, cancellationToken) ?? throw new NotFoundException("Órgão não encontrado.");

    public async Task<OrganizationData> CreateAsync(OrganizationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var organization = Organization.Create(request.Name, request.Acronym);
        await EnsureUniqueAsync(organization, cancellationToken);

        organizations.Add(organization);
        changeLog.Add(InventoryChange.Created(
            InventoryEntityType.Organization, organization.Id, timeProvider.GetUtcNow(), actor.Name));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAsync(organization.Id, cancellationToken);
    }

    public async Task<OrganizationData> UpdateAsync(
        Guid id, OrganizationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var organization = await FindAsync(id, cancellationToken);
        var changes = organization.Update(request.Name, request.Acronym);

        if (changes.Count > 0)
        {
            await EnsureUniqueAsync(organization, cancellationToken);
            changeLog.Add(InventoryChange.Updated(
                InventoryEntityType.Organization, id, changes, timeProvider.GetUtcNow(), actor.Name));
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return await GetAsync(id, cancellationToken);
    }

    public async Task InactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var organization = await FindAsync(id, cancellationToken);
        var now = timeProvider.GetUtcNow();

        organization.Inactivate(now, actor.Name);
        changeLog.Add(InventoryChange.Inactivated(InventoryEntityType.Organization, id, null, now, actor.Name));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Organization> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await organizations.FindByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Órgão não encontrado.");

    private async Task EnsureUniqueAsync(Organization organization, CancellationToken cancellationToken)
    {
        var sameName = await organizations.FindByNameKeyAsync(organization.NameKey, cancellationToken);
        if (sameName is not null && sameName.Id != organization.Id)
        {
            throw new ConflictException("Já existe um órgão com esse nome.");
        }

        var sameAcronym = await organizations.FindByAcronymKeyAsync(organization.AcronymKey, cancellationToken);
        if (sameAcronym is not null && sameAcronym.Id != organization.Id)
        {
            throw new ConflictException("Já existe um órgão com essa sigla.");
        }
    }
}
