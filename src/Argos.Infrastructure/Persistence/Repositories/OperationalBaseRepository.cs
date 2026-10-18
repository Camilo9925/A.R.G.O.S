using Argos.Application.Inventory.Support;
using Argos.Domain.Inventory.Support;
using Microsoft.EntityFrameworkCore;

namespace Argos.Infrastructure.Persistence.Repositories;

internal sealed class OperationalBaseRepository(ArgosDbContext dbContext) : IOperationalBaseRepository
{
    public Task<OperationalBase?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.OperationalBases.SingleOrDefaultAsync(operationalBase => operationalBase.Id == id, cancellationToken);

    public Task<OperationalBase?> FindByNameKeyAsync(
        Guid organizationId, string nameKey, CancellationToken cancellationToken = default) =>
        dbContext.OperationalBases.SingleOrDefaultAsync(
            operationalBase => operationalBase.OrganizationId == organizationId && operationalBase.NameKey == nameKey,
            cancellationToken);

    public Task<bool> AnyActiveInNeighborhoodAsync(Guid neighborhoodId, CancellationToken cancellationToken = default) =>
        dbContext.OperationalBases.AnyAsync(
            operationalBase => operationalBase.IsActive && operationalBase.Location.NeighborhoodId == neighborhoodId,
            cancellationToken);

    public void Add(OperationalBase operationalBase) => dbContext.OperationalBases.Add(operationalBase);
}
