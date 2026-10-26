using Argos.Simulation.Geography;
using Argos.Simulation.Models;

namespace Argos.Simulation.Generators;

/// <summary>
/// Histórico simulado de ocorrências (RF-025, parcial; US-20; Art. IX; D-86, D-87, D-93; A-32, A-34; plan 17.3): função
/// pura de (semente, geografia, dia da carga). Percorre, nesta ordem fixa, região (pelo nome) → dia → faixa → tipo, com
/// <b>uma</b> instância de <see cref="Random"/>: em cada passo sorteia a quantidade por Poisson (algoritmo de Knuth) com a
/// taxa esperada, o instante uniforme dentro da faixa e o ponto sobre um logradouro da região. O ponto só é aceito dentro
/// do polígono da região: novo sorteio, até 10 tentativas no mesmo logradouro e, depois, em outro.
/// <para>
/// <b>Período:</b> as 12 semanas que terminam na meia-noite local que inicia o dia da carga, sem dia parcial (D-87). A
/// faixa 22h–02h do último dia é cortada nessa meia-noite (a taxa cai pela fração cortada).
/// <b>Padrões</b> (constantes abaixo): a taxa semanal de cada região, o perfil de cada tipo por faixa e por dia da semana,
/// normalizado para que a soma da semana bata com a taxa da região; a Zona Leste tem +30% nas últimas 3 semanas
/// (tendência) e a Zona Oeste só nas últimas 3 ("adesão recente ao programa", A-34).
/// </para>
/// </summary>
public static class OccurrenceHistoryGenerator
{
    public const int Weeks = 12;

    /// <summary>Semanas finais com a Zona Leste mais intensa e com a Zona Oeste presente.</summary>
    public const int RecentWeeks = 3;

    public const string TrendRegion = "Zona Leste";

    public const double TrendBoost = 0.3;

    public const string RecentOnlyRegion = "Zona Oeste";

    private const int BandHours = 4;
    private const int PointAttemptsPerStreet = 10;
    private const int StreetAttempts = 25;

    /// <summary>Ocorrências por semana de cada região (A-32).</summary>
    private static readonly IReadOnlyDictionary<string, double> WeeklyRates = new Dictionary<string, double>(StringComparer.Ordinal)
    {
        ["Centro"] = 110,
        ["Zona Leste"] = 70,
        ["Zona Norte"] = 60,
        ["Zona Sul"] = 55,
        ["Zona Oeste"] = 50,
    };

    /// <summary>Participação de cada tipo na taxa semanal (A-32), na ordem do enum.</summary>
    private static readonly double[] KindShares = [0.20, 0.20, 0.10, 0.30, 0.20];

    public static IReadOnlyList<SimulatedOccurrence> Generate(int seed, PrometeiaGeography geography, DateOnly endDate)
    {
        ArgumentNullException.ThrowIfNull(geography);

        var random = new Random(seed);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(geography.Municipality.TimeZoneId);
        var periodStart = endDate.AddDays(-7 * Weeks).ToDateTime(TimeOnly.MinValue);
        var periodEnd = endDate.ToDateTime(TimeOnly.MinValue);
        var recentStart = endDate.AddDays(-7 * RecentWeeks);
        var bands = new[] { 2, 6, 10, 14, 18, 22 };
        var normalization = Enum.GetValues<SimulatedOccurrenceKind>().ToDictionary(kind => kind, WeekWeight);
        var occurrences = new List<SimulatedOccurrence>();

        foreach (var region in geography.Regions.OrderBy(region => region.Name, StringComparer.Ordinal))
        {
            var rate = WeeklyRates.TryGetValue(region.Name, out var found)
                ? found
                : throw new InvalidOperationException($"Região sem taxa de ocorrências: {region.Name}.");
            var streets = geography.Streets.Where(street => geography.RegionOf(street) == region.Name).ToArray();

            for (var day = endDate.AddDays(-7 * Weeks); day < endDate; day = day.AddDays(1))
            {
                if (region.Name == RecentOnlyRegion && day < recentStart)
                {
                    continue;
                }

                var dayRate = region.Name == TrendRegion && day >= recentStart ? rate * (1 + TrendBoost) : rate;

                foreach (var startHour in bands)
                {
                    var bandStart = day.ToDateTime(new TimeOnly(startHour, 0));
                    var effectiveStart = bandStart < periodStart ? periodStart : bandStart;
                    var effectiveEnd = bandStart.AddHours(BandHours) > periodEnd ? periodEnd : bandStart.AddHours(BandHours);
                    var seconds = (int)(effectiveEnd - effectiveStart).TotalSeconds;

                    if (seconds <= 0)
                    {
                        continue;
                    }

                    var clipped = seconds / (BandHours * 3600.0);

                    foreach (var kind in Enum.GetValues<SimulatedOccurrenceKind>())
                    {
                        var lambda = dayRate * KindShares[(int)kind] * BandMultiplier(kind, startHour) * DayMultiplier(kind, day.DayOfWeek)
                            / normalization[kind] * clipped;

                        for (var count = Poisson(random, lambda); count > 0; count--)
                        {
                            var local = effectiveStart.AddSeconds(random.Next(seconds));
                            var (street, point) = PickPoint(random, region, streets);
                            occurrences.Add(new SimulatedOccurrence(
                                kind,
                                new DateTimeOffset(local, timeZone.GetUtcOffset(local)),
                                region.Name,
                                street.Neighborhood,
                                street.Name,
                                point.Latitude,
                                point.Longitude));
                        }
                    }
                }
            }
        }

        return occurrences;
    }

    /// <summary>Soma dos multiplicadores das 42 células de um tipo: a taxa semanal é repartida na proporção deles.</summary>
    private static double WeekWeight(SimulatedOccurrenceKind kind)
    {
        var total = 0.0;

        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            foreach (var startHour in new[] { 2, 6, 10, 14, 18, 22 })
            {
                total += BandMultiplier(kind, startHour) * DayMultiplier(kind, day);
            }
        }

        return total;
    }

    /// <summary>Perfil do tipo por faixa horária, pela hora de início da faixa (A-32; plan 17.3).</summary>
    private static double BandMultiplier(SimulatedOccurrenceKind kind, int startHour) => kind switch
    {
        SimulatedOccurrenceKind.TrafficAccident => startHour switch { 6 or 14 or 18 => 1.6, 2 => 0.4, _ => 1.0 },
        SimulatedOccurrenceKind.Congestion => startHour switch { 6 => 2.0, 14 or 18 => 1.8, 22 or 2 => 0.1, _ => 1.0 },
        SimulatedOccurrenceKind.TheftOrRobbery => startHour switch { 18 or 22 => 1.8, 2 => 1.2, 6 => 0.5, _ => 1.0 },
        SimulatedOccurrenceKind.Vandalism => startHour switch { 22 => 2.0, 2 => 1.4, 6 or 10 or 14 => 0.4, _ => 1.0 },
        SimulatedOccurrenceKind.SevereTrafficViolation => startHour switch { 6 or 10 or 14 or 18 => 1.2, _ => 0.6 },
        _ => 1.0,
    };

    /// <summary>Perfil do tipo por dia da semana (A-32; plan 17.3).</summary>
    private static double DayMultiplier(SimulatedOccurrenceKind kind, DayOfWeek day) => kind switch
    {
        SimulatedOccurrenceKind.TrafficAccident => day is DayOfWeek.Friday or DayOfWeek.Saturday ? 1.2 : 1.0,
        SimulatedOccurrenceKind.Congestion => day switch { DayOfWeek.Saturday => 0.7, DayOfWeek.Sunday => 0.4, _ => 1.0 },
        SimulatedOccurrenceKind.TheftOrRobbery => day is DayOfWeek.Friday or DayOfWeek.Saturday ? 1.4 : 1.0,
        SimulatedOccurrenceKind.Vandalism => day is DayOfWeek.Friday or DayOfWeek.Saturday ? 1.5 : 1.0,
        _ => 1.0,
    };

    /// <summary>Poisson pelo algoritmo de Knuth, com o mesmo <see cref="Random"/> do gerador.</summary>
    private static int Poisson(Random random, double lambda)
    {
        if (lambda <= 0)
        {
            return 0;
        }

        var limit = Math.Exp(-lambda);
        var product = 1.0;
        var count = 0;

        do
        {
            count++;
            product *= random.NextDouble();
        }
        while (product > limit);

        return count - 1;
    }

    private static (PrometeiaStreet Street, GeoCoordinate Point) PickPoint(
        Random random, PrometeiaRegion region, PrometeiaStreet[] streets)
    {
        var street = streets[random.Next(streets.Length)];

        for (var attempt = 0; attempt < StreetAttempts; attempt++)
        {
            for (var tries = 0; tries < PointAttemptsPerStreet; tries++)
            {
                var segment = street.Geometry[random.Next(street.Geometry.Count)];
                var point = GeoMath.PointAlong(segment, random.NextDouble());

                if (GeoMath.Contains(region.Boundary, point))
                {
                    return (street, point);
                }
            }

            street = streets[random.Next(streets.Length)];
        }

        return (street, street.ReferencePoint);
    }
}
