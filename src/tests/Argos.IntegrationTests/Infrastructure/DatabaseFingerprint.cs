using Npgsql;

namespace Argos.IntegrationTests.Infrastructure;

/// <summary>
/// Retrato do banco para provar que uma operação não gravou nada: por tabela, a quantidade de linhas e a soma dos
/// <c>xmin</c> (que muda em toda inserção ou alteração de linha).
/// </summary>
internal static class DatabaseFingerprint
{
    public static async Task<IReadOnlyDictionary<string, (long Rows, decimal XminSum)>> TakeAsync(
        string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var tables = new List<string>();
        await using (var query = new NpgsqlCommand(
            "SELECT quote_ident(table_name) FROM information_schema.tables " +
            "WHERE table_schema = 'public' AND table_type = 'BASE TABLE' ORDER BY table_name",
            connection))
        await using (var reader = await query.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                tables.Add(reader.GetString(0));
            }
        }

        var fingerprint = new SortedDictionary<string, (long, decimal)>(StringComparer.Ordinal);
        foreach (var table in tables)
        {
            await using var command = new NpgsqlCommand(
                $"SELECT count(*), coalesce(sum(xmin::text::numeric), 0) FROM {table}", connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            fingerprint[table] = (reader.GetInt64(0), reader.GetDecimal(1));
        }

        return fingerprint;
    }
}
