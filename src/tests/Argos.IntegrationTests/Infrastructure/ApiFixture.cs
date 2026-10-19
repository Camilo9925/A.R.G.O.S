using Argos.Contracts.Access;
using Argos.Contracts.Realtime;
using Argos.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Argos.IntegrationTests.Infrastructure;

/// <summary>
/// Sobe um PostgreSQL 17 descartável, aplica as migrations reais pelo mesmo ponto de entrada do Migrator
/// e hospeda a API em memória no ambiente <c>Testing</c> (plan 8.2). Compartilhada por toda a coleção.
/// Inclui as sondas de políticas (<see cref="PolicyProbeController"/>, D-44) e de erros (<see cref="ErrorProbeController"/>),
/// que só existem neste projeto.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private const string TestingEnvironment = "Testing";

    /// <summary>Chave de sistema de teste (D-34); fictícia, só existe neste projeto.</summary>
    public const string SystemKey = "chave-de-sistema-de-teste-com-32-caracteres-ou-mais";

    private const string JwtSigningKey = "chave-de-assinatura-de-teste-com-32-caracteres-ou-mais";

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17").Build();

    private WebApplicationFactory<Program>? _factory;
    private TestAccessTokens? _tokens;

    public WebApplicationFactory<Program> Factory =>
        _factory ?? throw new InvalidOperationException("A fixture ainda não foi inicializada.");

    /// <summary>Tokens por perfil, expirados e com assinatura inválida, emitidos pela porta da própria API (plan 14.12).</summary>
    public TestAccessTokens Tokens =>
        _tokens ?? throw new InvalidOperationException("A fixture ainda não foi inicializada.");

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment(TestingEnvironment)
            .UseSetting("ConnectionStrings:Argos", _database.GetConnectionString())
            .UseSetting("Jwt:SigningKey", JwtSigningKey)
            .UseSetting("SystemKey:Key", SystemKey)
            .ConfigureTestServices(services => services
                .AddControllers()
                .AddApplicationPart(typeof(PolicyProbeController).Assembly)));
        _tokens = new TestAccessTokens(_factory);

        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>().MigrateAsync();
    }

    /// <summary>
    /// Outra instância da API sobre o mesmo banco, com os próprios singletons. Serve aos testes que gravam parâmetros de
    /// negócio, cujo cache em memória é singleton (plan 15.10) e vazaria para os demais testes da instância comum.
    /// Quem cria descarta.
    /// </summary>
    public WebApplicationFactory<Program> CreateIsolatedFactory() => Factory.WithWebHostBuilder(_ => { });

    /// <summary>Conexão com o banco do teste, para verificações direto no PostgreSQL.</summary>
    public string ConnectionString => _database.GetConnectionString();

    /// <summary>Esvazia todas as tabelas, menos o histórico de migrations, antes de cada teste.</summary>
    public Task ResetDatabaseAsync() => DatabaseCleaner.ResetAsync(ConnectionString);

    /// <summary>Cliente HTTP de um usuário: <c>Authorization: Bearer</c> com o token informado.</summary>
    public HttpClient CreateUserClient(string accessToken)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);
        return client;
    }

    /// <summary>Cliente HTTP do PROMETHEUS: cabeçalho da chave de sistema, a de teste por padrão (D-34).</summary>
    public HttpClient CreateSystemClient(string systemKey = SystemKey)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add(SystemKeyContract.HeaderName, systemKey);
        return client;
    }

    /// <summary>
    /// Conexão com o hub da Central autenticada pelo token informado. Usa long polling porque o handler do
    /// <c>TestServer</c> não aceita WebSockets; o hub e as mensagens são os mesmos.
    /// </summary>
    public HubConnection CreateHubConnection(string accessToken) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(Factory.Server.BaseAddress, OperationsHubContract.Path), options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .Build();

    public async ValueTask DisposeAsync()
    {
        // As fábricas derivadas dos tokens são descartadas junto com a principal.
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _database.DisposeAsync();
    }
}
