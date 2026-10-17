namespace Argos.IntegrationTests.Infrastructure;

/// <summary>
/// Coleção que compartilha um único contêiner PostgreSQL e uma única API entre os testes (plan 8.2).
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "API";
}
