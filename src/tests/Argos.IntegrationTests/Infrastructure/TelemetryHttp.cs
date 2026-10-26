using Argos.Contracts.Telemetry;
using Npgsql;

namespace Argos.IntegrationTests.Infrastructure;

/// <summary>Rotas e lotes da telemetria usados pelos testes (plan 16.4, 16.10).</summary>
internal static class TelemetryHttp
{
    public const string ResourcesPath = "/api/telemetry/resources";

    public const string BatchesPath = "/api/telemetry/batches";

    /// <summary>
    /// Lote válido e vazio: só os campos obrigatórios. A API o aceita com <c>202</c> e publica
    /// <c>TelemetryBatchProcessed</c> mesmo sem posições (plan 16.11), por isso serve às provas de autenticação e do hub.
    /// </summary>
    public static TelemetryBatchRequest EmptyBatch() => new()
    {
        SentAt = DateTimeOffset.UtcNow.AddSeconds(-1),
        IsSimulated = true,
    };

    /// <summary>Contagem direto no PostgreSQL, para conferir o que a API gravou.</summary>
    public static async Task<long> CountAsync(this ApiFixture fixture, string sql, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }
}
