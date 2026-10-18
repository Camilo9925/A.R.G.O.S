using Argos.Application.Inventory.Geography;
using Argos.Domain.Inventory.Geography;
using Microsoft.EntityFrameworkCore;

namespace Argos.Infrastructure.Persistence.Repositories;

internal sealed class RegionRepository(ArgosDbContext dbContext) : IRegionRepository
{
    public Task<Region?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Regions.SingleOrDefaultAsync(region => region.Id == id, cancellationToken);

    public Task<Region?> FindByNameKeyAsync(string nameKey, CancellationToken cancellationToken = default) =>
        dbContext.Regions.SingleOrDefaultAsync(region => region.NameKey == nameKey, cancellationToken);

    public void Add(Region region) => dbContext.Regions.Add(region);
}
