using System.Net;
using Argos.IntegrationTests.Infrastructure;

namespace Argos.IntegrationTests.HealthChecks;

/// <summary>
/// Prontidão da API com o banco do contêiner (plan 7.3).
/// </summary>
public sealed class HealthCheckTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Ready_WithDatabaseAvailable_ReturnsHealthy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = Fixture.Factory.CreateClient();

        using var response = await client.GetAsync("/health/ready", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(cancellationToken));
    }
}
