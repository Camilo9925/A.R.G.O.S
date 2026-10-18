using Argos.Application.Common.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Argos.Infrastructure.Persistence.Repositories;

/// <summary>
/// Histórico das versões da carga sobre a tabela <c>seed_history</c> (plan 7.4, 15.11.4). O registro entra no mesmo
/// <c>SaveChanges</c> da carga, numa só transação.
/// </summary>
internal sealed class SeedLedger(ArgosDbContext dbContext, TimeProvider timeProvider) : ISeedLedger
{
    public Task<int?> FindAppliedSeedAsync(string version, CancellationToken cancellationToken = default) =>
        dbContext.SeedHistory
            .Where(entry => entry.Version == version)
            .OrderBy(entry => entry.AppliedAt)
            .Select(entry => (int?)entry.Seed)
            .FirstOrDefaultAsync(cancellationToken);

    public void Record(string version, int seed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        dbContext.SeedHistory.Add(new SeedHistoryEntry(version, seed, timeProvider.GetUtcNow()));
    }
}
