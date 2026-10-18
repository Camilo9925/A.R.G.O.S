using System.Net;
using System.Net.Http.Json;
using Argos.Contracts.Access;
using Argos.IntegrationTests.Infrastructure;

namespace Argos.IntegrationTests.Access;

/// <summary>
/// Autor da operação (D-43, D-47; plan 14.3): o nome e o perfil vêm do token do usuário; o PROMETHEUS, autenticado
/// pela chave de sistema, não tem perfil de usuário (D-34).
/// </summary>
public sealed class CurrentActorTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    public static TheoryData<string> Roles => [.. UserRoles.All];

    [Theory]
    [MemberData(nameof(Roles))]
    public async Task UserToken_ActorHasLoginAndRole_D47(string role)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = Fixture.CreateUserClient(Fixture.Tokens.For(role));

        var actor = await GetActorAsync(client, cancellationToken);

        Assert.Equal(TestAccessTokens.LoginFor(role), actor.Name);
        Assert.Equal(role, actor.Role);
    }

    [Fact]
    public async Task SystemKey_ActorIsPrometheusWithoutRole_D34()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = Fixture.CreateSystemClient();

        var actor = await GetActorAsync(client, cancellationToken);

        Assert.Equal("PROMETHEUS", actor.Name);
        Assert.Null(actor.Role);
    }

    private static async Task<ActorProbeResult> GetActorAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync($"/{ActorProbeController.Path}", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var actor = await response.Content.ReadFromJsonAsync<ActorProbeResult>(cancellationToken);
        Assert.NotNull(actor);
        return actor;
    }
}
