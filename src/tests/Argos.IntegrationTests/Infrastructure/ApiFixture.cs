using Argos.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Argos.IntegrationTests.Infrastructure;

/// <summary>
/// Sobe um PostgreSQL 17 descartável, aplica as migrations reais pelo mesmo ponto de entrada do Migrator
/// e hospeda a API em memória no ambiente <c>Testing</c> (plan 8.2). Compartilhada por toda a coleção.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private const string TestingEnvironment = "Testing";

    /// <summary>Chave de sistema de teste (D-34); fictícia, só existe neste projeto.</summary>
    public const string SystemKey = "chave-de-sistema-de-teste-com-32-caracteres-ou-mais";

    private const string JwtSigningKey = "chave-de-assinatura-de-teste-com-32-caracteres-ou-mais";

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17").Build();

    private WebApplicationFactory<Program>? _factory;

    public WebApplicationFactory<Program> Factory =>
        _factory ?? throw new InvalidOperationException("A fixture ainda não foi inicializada.");

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment(TestingEnvironment)
            .UseSetting("ConnectionStrings:Argos", _database.GetConnectionString())
            .UseSetting("Jwt:SigningKey", JwtSigningKey)
            .UseSetting("SystemKey:Key", SystemKey));

        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>().MigrateAsync();
    }

    /// <summary>Esvazia todas as tabelas, menos o histórico de migrations, antes de cada teste.</summary>
    public Task ResetDatabaseAsync() => DatabaseCleaner.ResetAsync(_database.GetConnectionString());

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _database.DisposeAsync();
    }
}
