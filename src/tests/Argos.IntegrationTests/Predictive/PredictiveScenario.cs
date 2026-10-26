using Argos.Application.Inventory.Geography;
using Argos.Application.Seeding.Inventory;
using Argos.Application.Seeding.Predictive;
using Argos.Domain.Common;
using Argos.Domain.Operations.Occurrences;
using Argos.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Argos.IntegrationTests.Predictive;

/// <summary>
/// Cenário da Cidade Preditiva sobre o banco real: duas zonas no município quadrado de teste, o Centro com 12 semanas de
/// ocorrências e a Zona Oeste com 3 (A-34), terminando na meia-noite local do dia de hoje, como a carga real (D-87).
/// As ocorrências entram pelo mesmo <see cref="OccurrenceHistorySeeder"/> do Migrator.
/// </summary>
internal sealed record PredictiveScenario(Guid CentroId, Guid WestId, int TotalOccurrences)
{
    public const int Seed = 42;

    public const string Centro = "Centro";

    public const string West = "Zona Oeste";

    private const string CentroNeighborhood = "Vila Hespéria";

    private const string WestNeighborhood = "Jardim do Oeste";

    private static readonly SeedPoint[] Square =
    [
        new(-22.20, -49.70), new(-22.20, -49.60), new(-22.30, -49.60), new(-22.30, -49.70), new(-22.20, -49.70),
    ];

    public static async Task<PredictiveScenario> CreateAsync(ApiFixture fixture, CancellationToken cancellationToken)
    {
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<InventoryReferenceSeeder>().SeedAsync(
                new InventoryReferenceSeed(
                    new MunicipalitySeed("Prometeia", "SP", "America/Sao_Paulo", Square, new SeedPoint(-22.25, -49.65)),
                    [new RegionSeed(Centro, Square), new RegionSeed(West, Square)],
                    [
                        new NeighborhoodSeed(CentroNeighborhood, Centro, new SeedPoint(-22.24, -49.64)),
                        new NeighborhoodSeed(WestNeighborhood, West, new SeedPoint(-22.26, -49.66)),
                    ],
                    [new ZipCodeSeed("16985001", "Rua das Flores", CentroNeighborhood, new SeedPoint(-22.241, -49.641))],
                    [new OrganizationSeed(InventorySeedData.Organization, "Departamento Municipal de Trânsito")],
                    [
                        new OperationalBaseSeed(
                            InventorySeedData.Base,
                            InventorySeedData.Organization,
                            new LocationSeed("16985001", "Rua das Flores", CentroNeighborhood, "número 100", -22.241, -49.641)),
                    ]),
                Seed,
                InventorySeedData.Anchor.AddDays(-200),
                cancellationToken);
        }

        var history = History();
        var result = await SeedAsync(fixture, history, cancellationToken);
        Assert.False(result.AlreadyApplied);

        await using var check = fixture.Factory.Services.CreateAsyncScope();
        var regions = check.ServiceProvider.GetRequiredService<IRegionRepository>();
        var centro = await regions.FindByNameKeyAsync(TextNormalization.Normalize(Centro), cancellationToken);
        var west = await regions.FindByNameKeyAsync(TextNormalization.Normalize(West), cancellationToken);

        return new PredictiveScenario(centro!.Id, west!.Id, history.Occurrences.Count);
    }

    public static async Task<SeedResult> SeedAsync(
        ApiFixture fixture, OccurrenceHistorySeed history, CancellationToken cancellationToken)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<OccurrenceHistorySeeder>().SeedAsync(history, Seed, cancellationToken);
    }

    /// <summary>
    /// Quatro ocorrências por dia no Centro (84 dias) e na Zona Oeste (21 dias, a primeira ao meio-dia do primeiro dia,
    /// RN-29), em horários e tipos diferentes; mais furtos nas sextas à noite, para haver uma célula mais intensa.
    /// </summary>
    public static OccurrenceHistorySeed History()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);
        var items = new List<OccurrenceSeed>();

        Fill(items, zone, today, 84, CentroNeighborhood, 0);
        Fill(items, zone, today, 21, WestNeighborhood, 12);

        return new OccurrenceHistorySeed(items);
    }

    private static void Fill(
        List<OccurrenceSeed> items, TimeZoneInfo zone, DateOnly end, int days, string neighborhood, int firstDayFromHour)
    {
        var slots = new[]
        {
            (Hour: 6, Kind: OccurrenceKind.TrafficAccident),
            (Hour: 12, Kind: OccurrenceKind.Congestion),
            (Hour: 19, Kind: OccurrenceKind.TheftOrRobbery),
            (Hour: 23, Kind: OccurrenceKind.Vandalism),
        };

        for (var offset = days; offset >= 1; offset--)
        {
            var day = end.AddDays(-offset);

            foreach (var slot in slots.Where(slot => offset != days || slot.Hour >= firstDayFromHour))
            {
                var local = day.ToDateTime(new TimeOnly(slot.Hour, 30));
                var count = day.DayOfWeek == DayOfWeek.Friday && slot.Hour == 19 ? 3 : 1;

                for (var item = 0; item < count; item++)
                {
                    items.Add(new OccurrenceSeed(
                        slot.Kind,
                        new DateTimeOffset(local, zone.GetUtcOffset(local)),
                        neighborhood,
                        "Rua das Flores",
                        -22.24,
                        -49.64));
                }
            }
        }
    }
}
