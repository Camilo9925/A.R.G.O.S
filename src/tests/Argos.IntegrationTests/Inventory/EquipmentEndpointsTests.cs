using System.Net;
using System.Net.Http.Json;
using Argos.Contracts.Access;
using Argos.IntegrationTests.Infrastructure;

namespace Argos.IntegrationTests.Inventory;

/// <summary>
/// Endpoints de equipamentos (plan 15.14): caminho feliz e erros que importam (Art. XII.3, XII.4; plan 15.16).
/// </summary>
public sealed class EquipmentEndpointsTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    private const string InitialStatusesRoute = "api/inventory/equipment/initial-statuses";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task InitialStatuses_ListsTheDomainInitialStatusesInOrder_D66()
    {
        using var client = Fixture.CreateUserClient(Fixture.Tokens.For(UserRoles.Administrator));

        var response = await client.GetAsync(InitialStatusesRoute, Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var statuses = await response.Content.ReadFromJsonAsync<string[]>(Cancellation);
        Assert.NotNull(statuses);
        Assert.Equal(["NotInstalled", "Operational", "Malfunction", "UnderMaintenance"], statuses);
    }

    [Theory]
    [InlineData(UserRoles.Operator)]
    [InlineData(UserRoles.Manager)]
    public async Task InitialStatuses_WithoutInventoryManage_IsForbidden_D58(string role)
    {
        using var client = Fixture.CreateUserClient(Fixture.Tokens.For(role));

        var response = await client.GetAsync(InitialStatusesRoute, Cancellation);

        await ProblemAssert.IsForbiddenAsync(response, Cancellation);
    }
}
