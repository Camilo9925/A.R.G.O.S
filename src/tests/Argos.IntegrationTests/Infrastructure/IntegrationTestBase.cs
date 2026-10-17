namespace Argos.IntegrationTests.Infrastructure;

/// <summary>
/// Base dos testes de integração: cada teste começa com o banco vazio e cria os dados de que precisa (plan 8.2).
/// </summary>
[Collection(ApiCollection.Name)]
public abstract class IntegrationTestBase(ApiFixture fixture) : IAsyncLifetime
{
    protected ApiFixture Fixture { get; } = fixture;

    public async ValueTask InitializeAsync() => await Fixture.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
