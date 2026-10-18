using Argos.Application.Inventory.Geography;
using Argos.Domain.Inventory.Geography;
using Microsoft.EntityFrameworkCore;

namespace Argos.Infrastructure.Persistence.Repositories;

internal sealed class NeighborhoodRepository(ArgosDbContext dbContext) : INeighborhoodRepository
{
    public Task<Neighborhood?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Neighborhoods.SingleOrDefaultAsync(neighborhood => neighborhood.Id == id, cancellationToken);

    public Task<Neighborhood?> FindByNameKeyAsync(string nameKey, CancellationToken cancellationToken = default) =>
        dbContext.Neighborhoods.SingleOrDefaultAsync(neighborhood => neighborhood.NameKey == nameKey, cancellationToken);

    public Task<bool> AnyActiveInRegionAsync(Guid regionId, CancellationToken cancellationToken = default) =>
        dbContext.Neighborhoods.AnyAsync(
            neighborhood => neighborhood.IsActive && neighborhood.RegionId == regionId, cancellationToken);

    public void Add(Neighborhood neighborhood) => dbContext.Neighborhoods.Add(neighborhood);
}
