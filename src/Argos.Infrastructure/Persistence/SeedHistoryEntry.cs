using Argos.Domain.Common;

namespace Argos.Infrastructure.Persistence;

/// <summary>
/// Registro técnico de cada carga de dados aplicada pelo Migrator, base da sua idempotência (plan 7.4).
/// </summary>
internal sealed class SeedHistoryEntry : IAppendOnly
{
    public const int VersionMaxLength = 50;

    private SeedHistoryEntry()
    {
    }

    public SeedHistoryEntry(string version, int seed, DateTimeOffset appliedAt)
    {
        Version = version;
        Seed = seed;
        AppliedAt = appliedAt;
    }

    public int Id { get; private set; }

    public string Version { get; private set; } = string.Empty;

    public int Seed { get; private set; }

    public DateTimeOffset AppliedAt { get; private set; }
}
