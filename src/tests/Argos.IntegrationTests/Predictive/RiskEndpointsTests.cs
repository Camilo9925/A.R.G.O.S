using System.Net;
using Argos.Contracts.Access;
using Argos.Contracts.Predictive;
using Argos.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Argos.IntegrationTests.Predictive;

/// <summary>
/// Rotas do risco pela API (T137, reduzido ao essencial; US-20, US-25; RN-26, RN-28 a RN-30; CS-06; Art. V.3, XII.3; plan
/// 17.11, 17.13): grade de 42 células, Zona Oeste com dados insuficientes, 404, previsão nos dois horizontes com o índice
/// de cada região igual ao de uma célula da grade, 400 para horizonte inválido, 403 para o Operador e nenhuma rota
/// <c>DELETE</c>. A taxa de acerto (RN-32, CS-09) não foi entregue.
/// </summary>
public sealed class RiskEndpointsTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    private const string RiskPrefix = "api/predictive/risk";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static string GridPath(Guid regionId) => $"{RiskPrefix}/regions/{regionId}/grid";

    [Theory]
    [InlineData(UserRoles.Manager)]
    [InlineData(UserRoles.Administrator)]
    public async Task Grid_OfARegionWithTwelveWeeks_HasFortyTwoCellsWithTheCompleteExplanation_US20_CS06(string role)
    {
        var scenario = await PredictiveScenario.CreateAsync(Fixture, Cancellation);
        using var client = Fixture.CreateUserClient(Fixture.Tokens.For(role));

        var response = await client.GetAsync(GridPath(scenario.CentroId), Cancellation);
        var grid = await response.ReadAsync<RiskGrid>(Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((scenario.CentroId, "Centro", false), (grid.RegionId, grid.RegionName, grid.InsufficientData));
        Assert.Equal(42, grid.Cells.Count);
        Assert.Equal(6, grid.Bands.Count);
        Assert.Equal(4, grid.Levels.Count);
        Assert.Contains(grid.Cells, cell => cell.Index == 100);
        Assert.All(grid.Cells.Select(cell => cell.Explanation).Append(grid.Explanation), explanation =>
        {
            Assert.NotEmpty(explanation.Subject);
            Assert.NotEmpty(explanation.HorizonLabel);
            Assert.True(explanation.DataConsidered.IsSimulated);
            Assert.NotEmpty(explanation.Factors);
            Assert.NotNull(explanation.Confidence);
            Assert.NotEmpty(explanation.Recommendation);
            Assert.False(explanation.InsufficientData);
        });
    }

    [Fact]
    public async Task Grid_OfTheWestZoneWithThreeWeeks_HasInsufficientDataAndNoCells_RN29()
    {
        var scenario = await PredictiveScenario.CreateAsync(Fixture, Cancellation);
        using var client = Fixture.CreateUserClient(Fixture.Tokens.For(UserRoles.Manager));

        var grid = await (await client.GetAsync(GridPath(scenario.WestId), Cancellation)).ReadAsync<RiskGrid>(Cancellation);

        Assert.True(grid.InsufficientData);
        Assert.Empty(grid.Cells);
        Assert.Equal(
            "Dados insuficientes: a Zona Oeste tem 3 semanas de histórico, e a previsão exige pelo menos 4.",
            grid.Explanation.InsufficientDataMessage);
    }

    [Fact]
    public async Task Grid_OfUnknownRegion_Is404()
    {
        await PredictiveScenario.CreateAsync(Fixture, Cancellation);
        using var client = Fixture.CreateUserClient(Fixture.Tokens.For(UserRoles.Manager));

        var response = await client.GetAsync(GridPath(Guid.NewGuid()), Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(ForecastHorizon.Next24Hours)]
    [InlineData(ForecastHorizon.Next7Days)]
    public async Task Forecast_InBothHorizons_ColorsTheRegionsWithTheIndexOfACellOfTheGrid_US20_ArtV3(ForecastHorizon horizon)
    {
        var scenario = await PredictiveScenario.CreateAsync(Fixture, Cancellation);
        using var client = Fixture.CreateUserClient(Fixture.Tokens.For(UserRoles.Manager));

        var response = await client.GetAsync($"{RiskPrefix}/forecast?horizon={horizon}", Cancellation);
        var forecast = await response.ReadAsync<RiskForecast>(Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(horizon, forecast.Horizon);
        Assert.Equal(2, forecast.Regions.Count);
        Assert.Equal(horizon == ForecastHorizon.Next24Hours ? TimeSpan.FromHours(24) : TimeSpan.FromDays(7), forecast.End - forecast.Start);

        var centro = forecast.Regions.Single(region => region.RegionId == scenario.CentroId);
        var grid = await (await client.GetAsync(GridPath(scenario.CentroId), Cancellation)).ReadAsync<RiskGrid>(Cancellation);
        var cell = grid.Cells.Single(item => item.DayOfWeek == centro.PeakDayOfWeek && item.Band == centro.PeakBand);
        Assert.Equal((cell.Index, cell.Level), (centro.Index, centro.Level));
        Assert.False(centro.InsufficientData);
        Assert.NotEmpty(centro.Explanation.Factors);
        Assert.NotNull(centro.Explanation.Confidence);
        Assert.Equal(horizon == ForecastHorizon.Next24Hours ? "Próximas 24 horas" : "Próximos 7 dias", centro.Explanation.HorizonLabel);

        var west = forecast.Regions.Single(region => region.RegionId == scenario.WestId);
        Assert.True(west.InsufficientData);
        Assert.Null(west.Index);
        Assert.Contains("a Zona Oeste tem 3 semanas de histórico", west.Explanation.InsufficientDataMessage);
    }

    [Fact]
    public async Task Forecast_WithoutTheHorizon_UsesTheNext24Hours()
    {
        await PredictiveScenario.CreateAsync(Fixture, Cancellation);
        using var client = Fixture.CreateUserClient(Fixture.Tokens.For(UserRoles.Manager));

        var forecast = await (await client.GetAsync($"{RiskPrefix}/forecast", Cancellation)).ReadAsync<RiskForecast>(Cancellation);

        Assert.Equal(ForecastHorizon.Next24Hours, forecast.Horizon);
    }

    [Theory]
    [InlineData("Next30Days")]
    [InlineData("99")]
    public async Task Forecast_WithAnInvalidHorizon_Is400(string horizon)
    {
        using var client = Fixture.CreateUserClient(Fixture.Tokens.For(UserRoles.Manager));

        var response = await client.GetAsync($"{RiskPrefix}/forecast?horizon={horizon}", Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Operator_IsForbiddenOnBothRoutes_RN26()
    {
        var scenario = await PredictiveScenario.CreateAsync(Fixture, Cancellation);
        using var client = Fixture.CreateUserClient(Fixture.Tokens.For(UserRoles.Operator));

        var grid = await client.GetAsync(GridPath(scenario.CentroId), Cancellation);
        var forecast = await client.GetAsync($"{RiskPrefix}/forecast?horizon=Next7Days", Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, grid.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forecast.StatusCode);
    }

    [Fact]
    public async Task WithoutToken_BothRoutesAre401()
    {
        using var client = Fixture.Factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"{RiskPrefix}/forecast", Cancellation)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(GridPath(Guid.NewGuid()), Cancellation)).StatusCode);
    }

    [Fact]
    public void RegisteredPredictiveRoutes_HaveNoDelete_RN23()
    {
        var routes = Fixture.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.TrimStart('/')
                .StartsWith("api/predictive", StringComparison.OrdinalIgnoreCase) == true)
            .Select(endpoint => (
                Pattern: endpoint.RoutePattern.RawText!.TrimStart('/'),
                Methods: endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? (IReadOnlyList<string>)[]))
            .ToList();

        // Prova de que a varredura enxerga as rotas: sem isso, uma lista vazia passaria no teste.
        Assert.Equal(2, routes.Count);
        Assert.Contains(routes, route => route.Pattern == "api/predictive/risk/forecast" && route.Methods.Contains("GET"));
        Assert.DoesNotContain(routes, route => route.Methods.Contains("DELETE"));
    }
}
