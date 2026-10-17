using Npgsql;

namespace Argos.IntegrationTests.Infrastructure;

/// <summary>
/// Isola os testes esvaziando as tabelas com <c>TRUNCATE ... RESTART IDENTITY CASCADE</c>,
/// preservando o histórico de migrations (plan 8.2).
/// </summary>
internal static class DatabaseCleaner
{
    private const string MigrationsHistoryTable = "__EFMigrationsHistory";

    public static async Task ResetAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var tables = new List<string>();
        await using (var query = new NpgsqlCommand(
            "SELECT quote_ident(table_name) FROM information_schema.tables " +
            "WHERE table_schema = 'public' AND table_type = 'BASE TABLE' AND table_name <> @history",
            connection))
        {
            query.Parameters.AddWithValue("history", MigrationsHistoryTable);
            await using var reader = await query.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        if (tables.Count == 0)
        {
            return;
        }

        await using var truncate = new NpgsqlCommand(
            $"TRUNCATE TABLE {string.Join(", ", tables)} RESTART IDENTITY CASCADE", connection);
        await truncate.ExecuteNonQueryAsync();
    }
}
