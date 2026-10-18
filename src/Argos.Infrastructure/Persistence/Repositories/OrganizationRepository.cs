using Argos.Application.Inventory.Support;
using Argos.Domain.Inventory.Support;
using Microsoft.EntityFrameworkCore;

namespace Argos.Infrastructure.Persistence.Repositories;

internal sealed class OrganizationRepository(ArgosDbContext dbContext) : IOrganizationRepository
{
    public Task<Organization?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Organizations.SingleOrDefaultAsync(organization => organization.Id == id, cancellationToken);

    public Task<Organization?> FindByNameKeyAsync(string nameKey, CancellationToken cancellationToken = default) =>
        dbContext.Organizations.SingleOrDefaultAsync(organization => organization.NameKey == nameKey, cancellationToken);

    public Task<Organization?> FindByAcronymKeyAsync(string acronymKey, CancellationToken cancellationToken = default) =>
        dbContext.Organizations.SingleOrDefaultAsync(organization => organization.AcronymKey == acronymKey, cancellationToken);

    public void Add(Organization organization) => dbContext.Organizations.Add(organization);
}
