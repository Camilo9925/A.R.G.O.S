using Argos.Application.Common.Abstractions;
using Argos.Application.Common.Exceptions;
using Argos.Contracts.Inventory;
using Argos.Domain.Inventory;
using Argos.Domain.Inventory.Geography;

namespace Argos.Application.Inventory.Geography;

/// <summary>
/// Cadastro de regiões (RF-020, RN-22; plan 15.2.1). Nome único sem diferença de maiúsculas e acentos, inclusive entre
/// inativas (D-53). Cada alteração vai para o registro cadastral, com autor e instante (D-49).
/// </summary>
public sealed class RegionService(
    IRegionRepository regions,
    INeighborhoodRepository neighborhoods,
    IInventoryQueries queries,
    IInventoryChangeLog changeLog,
    IUnitOfWork unitOfWork,
    ICurrentActor actor,
    TimeProvider timeProvider)
{
    public Task<IReadOnlyList<RegionData>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default) =>
        queries.ListRegionsAsync(includeInactive, cancellationToken);

    public async Task<RegionData> CreateAsync(RegionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var region = Region.Create(request.Name);
        await EnsureUniqueNameAsync(region, cancellationToken);

        regions.Add(region);
        changeLog.Add(InventoryChange.Created(InventoryEntityType.Region, region.Id, timeProvider.GetUtcNow(), actor.Name));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAsync(region.Id, cancellationToken);
    }

    public async Task<RegionData> UpdateAsync(Guid id, RegionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var region = await FindAsync(id, cancellationToken);
        var changes = region.Rename(request.Name);

        if (changes.Count > 0)
        {
            await EnsureUniqueNameAsync(region, cancellationToken);
            changeLog.Add(InventoryChange.Updated(InventoryEntityType.Region, id, changes, timeProvider.GetUtcNow(), actor.Name));
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Inativa a região, recusada se houver bairro ativo nela (plan 15.2.1).</summary>
    public async Task InactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var region = await FindAsync(id, cancellationToken);
        var now = timeProvider.GetUtcNow();

        region.Inactivate(await neighborhoods.AnyActiveInRegionAsync(id, cancellationToken), now, actor.Name);
        changeLog.Add(InventoryChange.Inactivated(InventoryEntityType.Region, id, null, now, actor.Name));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Region> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await regions.FindByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Região não encontrada.");

    private async Task<RegionData> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await queries.GetRegionAsync(id, cancellationToken) ?? throw new NotFoundException("Região não encontrada.");

    private async Task EnsureUniqueNameAsync(Region region, CancellationToken cancellationToken)
    {
        var existing = await regions.FindByNameKeyAsync(region.NameKey, cancellationToken);
        if (existing is not null && existing.Id != region.Id)
        {
            throw new ConflictException("Já existe uma região com esse nome.");
        }
    }
}
