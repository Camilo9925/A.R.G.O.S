using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Argos.Application.Access;
using Argos.Contracts.Access;
using Argos.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authorization;

namespace Argos.IntegrationTests.Access;

/// <summary>
/// Autenticação e autorização por HTTP (RN-26; D-32 a D-34; plan 14.7, 14.12): <c>401</c> e <c>403</c> com
/// <c>ProblemDetails</c> em pt-BR e sem detalhes internos (Art. VIII.6, XII.4), e a matriz perfil × política
/// registrada na API, pela sonda de políticas.
/// </summary>
public sealed class AuthorizationTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    private static readonly string ProtectedPath = PolicyProbeController.PathFor(Permissions.OperationsView);

    public static TheoryData<string> Roles => [.. UserRoles.All];

    public static TheoryData<string> AllPermissions => [.. Permissions.All];

    public static TheoryData<string, string, bool> Rn26Cases => Rn26Matrix.Cases();

    // 401

    [Fact]
    public async Task UserEndpoint_WithoutCredentials_ReturnsUnauthorizedProblemWithBearerChallenge()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = Fixture.Factory.CreateClient();

        using var response = await client.GetAsync(ProtectedPath, cancellationToken);

        await ProblemAssert.IsUnauthenticatedAsync(response, cancellationToken);
        Assert.Contains(response.Headers.WwwAuthenticate, challenge => challenge.Scheme == "Bearer");
    }

    [Fact]
    public async Task UserEndpoint_WithExpiredToken_ReturnsUnauthorizedProblem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = Fixture.CreateUserClient(Fixture.Tokens.ExpiredFor(UserRoles.Administrator));

        using var response = await client.GetAsync(ProtectedPath, cancellationToken);

        await ProblemAssert.IsUnauthenticatedAsync(response, cancellationToken);
    }

    [Fact]
    public async Task UserEndpoint_WithInvalidSignature_ReturnsUnauthorizedProblem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = Fixture.CreateUserClient(Fixture.Tokens.WithInvalidSignatureFor(UserRoles.Administrator));

        using var response = await client.GetAsync(ProtectedPath, cancellationToken);

        await ProblemAssert.IsUnauthenticatedAsync(response, cancellationToken);
    }

    [Fact]
    public async Task SystemEndpoint_WithoutCredentials_ReturnsUnauthorizedProblem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = Fixture.Factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            TelemetryHttp.BatchesPath, TelemetryHttp.EmptyBatch(), cancellationToken);

        await ProblemAssert.IsUnauthenticatedAsync(response, cancellationToken);
    }

    [Fact]
    public async Task SystemEndpoint_WithWrongSystemKey_ReturnsUnauthorizedProblem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = Fixture.CreateSystemClient("chave-de-sistema-errada-com-32-caracteres-ou-mais");

        using var response = await client.PostAsJsonAsync(
            TelemetryHttp.BatchesPath, TelemetryHttp.EmptyBatch(), cancellationToken);

        var problem = await ProblemAssert.IsUnauthenticatedAsync(response, cancellationToken);
        Assert.DoesNotContain("chave-de-sistema", problem.GetRawText());
    }

    // 403 e matriz da RN-26

    [Theory]
    [MemberData(nameof(Rn26Cases))]
    public async Task PolicyProbe_EachRoleAndPolicy_FollowsRn26(string role, string policy, bool allowed)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = Fixture.CreateUserClient(Fixture.Tokens.For(role));

        using var response = await client.GetAsync(PolicyProbeController.PathFor(policy), cancellationToken);

        if (allowed)
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
        else
        {
            await ProblemAssert.IsForbiddenAsync(response, cancellationToken);
        }
    }

    [Fact]
    public void PolicyProbe_CoversEveryCatalogPolicyAndRn26CoversEveryPermission()
    {
        var probedPolicies = typeof(PolicyProbeController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.GetCustomAttribute<AuthorizeAttribute>()?.Policy)
            .OfType<string>()
            .Order();

        var catalogPolicies = AccessPolicyCatalog.Policies.Select(policy => policy.Name).Order().ToArray();
        Assert.Equal(catalogPolicies, probedPolicies);
        Assert.Equal(catalogPolicies, Rn26Matrix.AllPermissions.Order());
    }

    [Theory]
    [MemberData(nameof(Roles))]
    public async Task SystemEndpoint_WithUserToken_ReturnsForbiddenProblem(string role)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = Fixture.CreateUserClient(Fixture.Tokens.For(role));

        using var response = await client.PostAsJsonAsync(
            TelemetryHttp.BatchesPath, TelemetryHttp.EmptyBatch(), cancellationToken);

        await ProblemAssert.IsForbiddenAsync(response, cancellationToken);
    }

    [Theory]
    [MemberData(nameof(AllPermissions))]
    public async Task UserPolicy_WithSystemKey_ReturnsForbiddenProblem(string policy)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = Fixture.CreateSystemClient();

        using var response = await client.GetAsync(PolicyProbeController.PathFor(policy), cancellationToken);

        await ProblemAssert.IsForbiddenAsync(response, cancellationToken);
    }
}
