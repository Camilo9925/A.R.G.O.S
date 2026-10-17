using Microsoft.EntityFrameworkCore;

namespace Argos.Infrastructure.Persistence;

/// <summary>
/// Contexto único do ARGOS (plan 5.1). Interno à Infraestrutura: controllers não o enxergam (Art. III.3, III.5).
/// </summary>
internal sealed class ArgosDbContext(DbContextOptions<ArgosDbContext> options) : DbContext(options)
{
    public DbSet<SeedHistoryEntry> SeedHistory => Set<SeedHistoryEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ArgosDbContext).Assembly);
    }
}
