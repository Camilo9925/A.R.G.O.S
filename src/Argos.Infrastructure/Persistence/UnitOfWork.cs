using Argos.Application.Common.Abstractions;

namespace Argos.Infrastructure.Persistence;

internal sealed class UnitOfWork(ArgosDbContext dbContext) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
