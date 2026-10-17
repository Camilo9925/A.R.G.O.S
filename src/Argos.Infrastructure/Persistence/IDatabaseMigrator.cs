namespace Argos.Infrastructure.Persistence;

/// <summary>
/// Ponto de entrada público de migração, usado pelo Migrator e pelos testes de integração (plan 5.3, 7.4).
/// </summary>
public interface IDatabaseMigrator
{
    /// <summary>Aplica as migrations pendentes.</summary>
    Task MigrateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra no histórico do seed a versão e a semente aplicadas.
    /// Retorna <see langword="false"/> se o mesmo registro já existia, sem duplicá-lo.
    /// </summary>
    Task<bool> RecordSeedAsync(string version, int seed, CancellationToken cancellationToken = default);
}
