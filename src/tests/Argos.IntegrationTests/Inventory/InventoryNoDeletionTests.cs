using System.Net;
using Argos.Contracts.Access;
using Argos.Contracts.Inventory;
using Argos.Infrastructure.Persistence;
using Argos.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Argos.IntegrationTests.Infrastructure.InventoryHttp;
using DomainEquipment = Argos.Domain.Inventory.Equipments.Equipment;
using DomainOrganization = Argos.Domain.Inventory.Support.Organization;

namespace Argos.IntegrationTests.Inventory;

/// <summary>
/// Nada é excluído no Inventário (RN-23; Art. V.2; D-21; plan 15.14, 15.16): nenhuma rota <c>DELETE</c> registrada sob
/// <c>/api/inventory</c>, a requisição <c>DELETE</c> é recusada e o interceptador barra a exclusão no banco mesmo
/// quando o código tenta.
/// </summary>
public sealed class InventoryNoDeletionTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    private const string InventoryPrefix = "api/inventory";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void RegisteredInventoryRoutes_HaveNoDelete_RN23()
    {
        var routes = InventoryRoutes();

        // Prova de que a varredura enxerga as rotas: sem isso, uma lista vazia passaria no teste.
        Assert.True(routes.Count >= 30, $"Só {routes.Count} rotas encontradas sob /{InventoryPrefix}.");
        Assert.Contains(routes, route => route.Methods.Contains("GET") && route.Pattern == "api/inventory/equipment");
        Assert.Contains(routes, route => route.Methods.Contains("POST") && route.Pattern == "api/inventory/equipment");
        Assert.Contains(routes, route => route.Methods.Contains("PUT") && route.Pattern == "api/inventory/equipment/{id:guid}");
        Assert.Contains(routes, route => route.Methods.Contains("POST") && route.Pattern.EndsWith("/inactivation", StringComparison.Ordinal));

        Assert.All(routes, route => Assert.NotEmpty(route.Methods));
        Assert.DoesNotContain(routes, route => route.Methods.Contains("DELETE"));
    }

    [Fact]
    public async Task DeleteRequest_IsRefused_AndTheEquipmentStays_RN23()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Fixture.CreateUserClient(Fixture.Tokens.For(UserRoles.Administrator));
        var created = await client.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);

        var equipment = await client.DeleteAsync($"{Equipment}/{created.Id}", Cancellation);
        var organization = await client.DeleteAsync($"{InventoryPrefix}/organizations/{scenario.OrganizationId}", Cancellation);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, equipment.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, organization.StatusCode);
        Assert.Equal(created.Id, (await client.GetEquipmentAsync(created.Id, Cancellation)).Id);
    }

    [Fact]
    public async Task Interceptor_RefusesDeletingAnEquipment_RN23()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Fixture.CreateUserClient(Fixture.Tokens.For(UserRoles.Administrator));
        var created = await client.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);

        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var dbContext = DbContextOf(scope.ServiceProvider);
            var equipment = await dbContext.Set<DomainEquipment>().SingleAsync(item => item.Id == created.Id, Cancellation);
            dbContext.Remove(equipment);

            var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync(Cancellation));
            Assert.Equal("Exclusão física não é permitida (Equipment). Inative o registro.", refused.Message);
        }

        Assert.Equal(created.Id, (await client.GetEquipmentAsync(created.Id, Cancellation)).Id);
    }

    [Fact]
    public async Task Interceptor_RefusesDeletingASupportRegister_RN23()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);

        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var dbContext = DbContextOf(scope.ServiceProvider);
            var organization = await dbContext.Set<DomainOrganization>()
                .SingleAsync(item => item.Id == scenario.OrganizationId, Cancellation);
            dbContext.Remove(organization);

            var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync(Cancellation));
            Assert.Equal("Exclusão física não é permitida (Organization). Inative o registro.", refused.Message);
        }

        using var client = Fixture.CreateUserClient(Fixture.Tokens.For(UserRoles.Administrator));
        var response = await client.GetAsync($"{InventoryPrefix}/organizations/{scenario.OrganizationId}", Cancellation);
        Assert.Equal(scenario.OrganizationId, (await response.ReadAsync<OrganizationData>(Cancellation)).Id);
    }

    /// <summary>Rotas de controller registradas sob <c>/api/inventory</c>, com os métodos HTTP de cada uma.</summary>
    private List<(string Pattern, IReadOnlyList<string> Methods)> InventoryRoutes() =>
        Fixture.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.TrimStart('/')
                .StartsWith(InventoryPrefix, StringComparison.OrdinalIgnoreCase) == true)
            .Select(endpoint => (
                endpoint.RoutePattern.RawText!.TrimStart('/'),
                endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? (IReadOnlyList<string>)[]))
            .ToList();

    /// <summary>
    /// O <c>DbContext</c> é interno da Infraestrutura (Art. III.5); o teste o obtém do contêiner pelo tipo, sem mudar a
    /// visibilidade no código de produção.
    /// </summary>
    private static DbContext DbContextOf(IServiceProvider services)
    {
        var type = typeof(IDatabaseMigrator).Assembly.GetType("Argos.Infrastructure.Persistence.ArgosDbContext", throwOnError: true)!;
        return (DbContext)services.GetRequiredService(type);
    }
}
