using Microsoft.EntityFrameworkCore;

namespace Argos.Infrastructure.Persistence;

internal sealed class DatabaseMigrator(ArgosDbContext dbContext, TimeProvider timeProvider) : IDatabaseMigrator
{
    public Task MigrateAsync(CancellationToken cancellationToken = default) =>
        dbContext.Database.MigrateAsync(cancellationToken);

    public async Task<bool> RecordSeedAsync(string version, int seed, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var alreadyRecorded = await dbContext.SeedHistory
            .AnyAsync(entry => entry.Version == version && entry.Seed == seed, cancellationToken);

        if (alreadyRecorded)
        {
            return false;
        }

        dbContext.SeedHistory.Add(new SeedHistoryEntry(version, seed, timeProvider.GetUtcNow()));
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
