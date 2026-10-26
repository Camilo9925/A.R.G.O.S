using Argos.Application.Common.Abstractions;
using Argos.Application.Seeding.Inventory;
using Argos.Application.Seeding.Predictive;
using Argos.Infrastructure.Persistence;
using Argos.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Argos.IntegrationTests.Predictive;

/// <summary>
/// Carga <c>predictive-occurrences-v1</c> e persistência das ocorrências contra o PostgreSQL real (T133, T137; RF-025,
/// RN-23; Art. V.1, IX.2, IX.3, XII.7; D-56, D-86; plan 17.10): a carga entra sobre um banco com a referência, grava uma
/// vez com autor <c>simulação</c> e marcadas como simuladas, uma segunda execução não grava nada, e a ocorrência nunca é
/// alterada.
/// </summary>
public sealed class OccurrenceSeedingTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Load_RunTwice_WritesOnlyOnce_ArtIX3_ArtXII7()
    {
        var scenario = await PredictiveScenario.CreateAsync(Fixture, Cancellation);
        var first = await Fingerprint();

        var second = await PredictiveScenario.SeedAsync(Fixture, PredictiveScenario.History(), Cancellation);

        Assert.True(second.AlreadyApplied);
        Assert.Equal(first, await Fingerprint());
        Assert.Equal(scenario.TotalOccurrences, await Fixture.CountAsync("SELECT count(*) FROM occurrences", Cancellation));
        Assert.Equal(1, await Fixture.CountAsync(
            $"SELECT count(*) FROM seed_history WHERE version = '{PredictiveSeed.OccurrencesVersion}'", Cancellation));
    }

    [Fact]
    public async Task Load_MarksEveryOccurrenceAsSimulatedWithTheSimulationAuthor_ArtIX2()
    {
        await PredictiveScenario.CreateAsync(Fixture, Cancellation);

        Assert.Equal(0, await Fixture.CountAsync(
            "SELECT count(*) FROM occurrences WHERE NOT is_simulated OR author <> 'simulação'", Cancellation));
    }

    [Fact]
    public async Task Load_BeforeTheReference_StopsWithoutWriting()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => PredictiveScenario.SeedAsync(Fixture, PredictiveScenario.History(), Cancellation));

        Assert.Equal(
            $"Aplique a carga {InventorySeed.ReferenceVersion} antes de {PredictiveSeed.OccurrencesVersion}.", error.Message);
        Assert.Equal(0, await Fixture.CountAsync("SELECT count(*) FROM occurrences", Cancellation));
    }

    [Fact]
    public async Task Load_WithAnotherSeedAfterTheFirst_StopsWithAClearMessage()
    {
        await PredictiveScenario.CreateAsync(Fixture, Cancellation);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider
            .GetRequiredService<OccurrenceHistorySeeder>().SeedAsync(PredictiveScenario.History(), 7, Cancellation));

        Assert.Contains("já foi aplicada com a semente 42", error.Message);
    }

    [Fact]
    public async Task Occurrence_CannotBeChanged_ArtV1()
    {
        await PredictiveScenario.CreateAsync(Fixture, Cancellation);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = DbContextOf(scope.ServiceProvider);
        var occurrence = await dbContext.Set<Domain.Operations.Occurrences.Occurrence>().FirstAsync(Cancellation);
        dbContext.Entry(occurrence).Property(nameof(occurrence.Description)).CurrentValue = "Alterada.";

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync(Cancellation));
        Assert.Equal("Registros de histórico não podem ser alterados nem removidos (Occurrence).", refused.Message);
    }

    [Fact]
    public async Task MinimumHistory_DefaultsToFourWeeks_RN29_RN34()
    {
        await PredictiveScenario.CreateAsync(Fixture, Cancellation);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var parameters = scope.ServiceProvider.GetRequiredService<IBusinessParameters>();

        Assert.Equal(4, await parameters.GetAsync(Domain.Common.Parameters.BusinessParameter.PredictiveMinimumHistoryWeeks, Cancellation));
    }

    private Task<IReadOnlyDictionary<string, (long Rows, decimal XminSum)>> Fingerprint() =>
        DatabaseFingerprint.TakeAsync(Fixture.ConnectionString, Cancellation);

    private static DbContext DbContextOf(IServiceProvider services)
    {
        var type = typeof(IDatabaseMigrator).Assembly.GetType("Argos.Infrastructure.Persistence.ArgosDbContext", throwOnError: true)!;
        return (DbContext)services.GetRequiredService(type);
    }
}
