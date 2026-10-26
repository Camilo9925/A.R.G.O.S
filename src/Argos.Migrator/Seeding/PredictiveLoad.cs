using Argos.Application.Seeding.Predictive;
using Argos.Domain.Operations.Occurrences;
using Argos.Simulation.Configuration;
using Argos.Simulation.Generators;
using Argos.Simulation.Geography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Argos.Migrator.Seeding;

/// <summary>
/// Carga da Cidade Preditiva pelo Migrator (Fase 5; D-86; plan 17.3, 17.10), depois das cargas da Fase 4:
/// <c>predictive-occurrences-v1</c>, o histórico de 12 semanas de ocorrências simuladas pelo
/// <see cref="OccurrenceHistoryGenerator"/>, que termina na meia-noite local que inicia o dia da carga (D-87). Roda num
/// escopo próprio e grava num só <c>SaveChanges</c>, com o registro da versão; entra num volume da Fase 4 sem
/// <c>down -v</c>. Os parâmetros novos (<c>predictive.*</c>) são criados antes, pela provisão de parâmetros.
/// </summary>
internal static class PredictiveLoad
{
    public static async Task RunAsync(
        IServiceProvider services,
        SimulationOptions simulation,
        TimeProvider timeProvider,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var anchor = simulation.AnchorDate ?? timeProvider.GetUtcNow();

        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<OccurrenceHistorySeeder>().SeedAsync(
            History(simulation.Seed, anchor), simulation.Seed, cancellationToken);
        InventoryLoad.Log(logger, result, simulation.Seed);
    }

    /// <summary>
    /// Ocorrências das 12 semanas que terminam na meia-noite local do dia da âncora (D-87), no fuso do município, pelo
    /// gerador da Simulação, que a Aplicação não referencia.
    /// </summary>
    internal static OccurrenceHistorySeed History(int seed, DateTimeOffset anchor)
    {
        var geography = PrometeiaGeography.Load();
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(geography.Municipality.TimeZoneId);
        var endDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(anchor, timeZone).DateTime);

        return new OccurrenceHistorySeed(OccurrenceHistoryGenerator.Generate(seed, geography, endDate)
            .Select(occurrence => new OccurrenceSeed(
                Enum.Parse<OccurrenceKind>(occurrence.Kind.ToString()),
                occurrence.OccurredAt,
                occurrence.NeighborhoodName,
                occurrence.Street,
                occurrence.Latitude,
                occurrence.Longitude))
            .ToArray());
    }
}
