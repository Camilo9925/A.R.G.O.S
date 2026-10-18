using Argos.Application.Inventory.Geography;
using Argos.Domain.Inventory.Geography;
using Microsoft.EntityFrameworkCore;

namespace Argos.Infrastructure.Persistence.Repositories;

internal sealed class MunicipalityRepository(ArgosDbContext dbContext) : IMunicipalityRepository
{
    public Task<Municipality?> GetAsync(CancellationToken cancellationToken = default) =>
        dbContext.Municipalities.SingleOrDefaultAsync(cancellationToken);

    public void Add(Municipality municipality) => dbContext.Municipalities.Add(municipality);
}
