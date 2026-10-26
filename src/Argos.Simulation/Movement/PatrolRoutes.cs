using System.Globalization;
using Argos.Simulation.Geography;

namespace Argos.Simulation.Movement;

/// <summary>Rota de um recurso: o maior trecho de um logradouro da região da base, percorrido em vai e volta.</summary>
/// <param name="PhaseMeters">Deslocamento inicial no percurso, para recursos na mesma rua não se sobreporem.</param>
public sealed record PatrolRoute(
    string ZipCode, string Street, string Region, IReadOnlyList<GeoCoordinate> Path, double LengthMeters, double PhaseMeters);

/// <summary>
/// Movimento simulado sem estado (D-74; plan 16.5; Art. IX.3, IX.4): posição = f(semente, chave de negócio, instante).
/// <para>
/// <b>Candidatos</b> de uma região: logradouros cujo bairro pertence a ela e cujo maior trecho tem pelo menos
/// <see cref="MinimumSegmentMeters"/> e todos os vértices dentro do polígono da região (RN-02), ordenados pelo CEP.
/// A rota de um recurso é o candidato de índice <c>hash(semente, chave) mod n</c>, com a fase também tirada do hash
/// (FNV-1a, <see cref="StableHash"/>).
/// </para>
/// </summary>
public sealed class PatrolRoutes
{
    public const double MinimumSegmentMeters = 300;

    private readonly Dictionary<string, IReadOnlyList<Candidate>> _candidatesByRegion;

    public PatrolRoutes(PrometeiaGeography geography)
    {
        ArgumentNullException.ThrowIfNull(geography);

        _candidatesByRegion = geography.Regions.ToDictionary(
            region => region.Name,
            region => (IReadOnlyList<Candidate>)geography.Streets
                .Where(street => geography.RegionOf(street) == region.Name)
                .Select(street => (Street: street, Segment: LongestSegment(street)))
                .Where(item => item.Segment.Length >= MinimumSegmentMeters
                    && item.Segment.Path.All(point => GeoMath.Contains(region.Boundary, point)))
                .OrderBy(item => item.Street.ZipCode, StringComparer.Ordinal)
                .Select(item => new Candidate(item.Street, item.Segment.Path, item.Segment.Length))
                .ToArray(),
            StringComparer.Ordinal);
    }

    /// <summary>Os CEPs dos candidatos da região, na ordem usada pelo sorteio.</summary>
    public IReadOnlyList<string> CandidateZipCodes(string region) =>
        CandidatesOf(region).Select(candidate => candidate.Street.ZipCode).ToArray();

    /// <summary>Rota do recurso de chave <paramref name="key"/> na região da sua base.</summary>
    /// <exception cref="ArgumentException">Região desconhecida ou sem candidatos.</exception>
    public PatrolRoute RouteFor(int seed, string key, string region)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var candidates = CandidatesOf(region);
        var seedText = seed.ToString(CultureInfo.InvariantCulture);
        var index = (int)(StableHash.Fnv1a64($"{seedText}|{key}") % (ulong)candidates.Count);
        var candidate = candidates[index];
        var phase = StableHash.Fraction($"{seedText}|{key}|phase") * 2 * candidate.LengthMeters;

        return new PatrolRoute(
            candidate.Street.ZipCode, candidate.Street.Name, region, candidate.Path, candidate.LengthMeters, phase);
    }

    /// <summary>
    /// Ponto da rota no instante (plan 16.5): distância = velocidade × segundos desde a época Unix + fase, módulo
    /// 2 × comprimento; até o comprimento vai, depois volta.
    /// </summary>
    public static GeoCoordinate PositionAt(PatrolRoute route, double speedKmh, DateTimeOffset instant)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentOutOfRangeException.ThrowIfNegative(speedKmh);

        if (route.LengthMeters <= 0)
        {
            return route.Path[0];
        }

        var seconds = instant.ToUnixTimeMilliseconds() / 1000.0;
        var roundTrip = 2 * route.LengthMeters;
        var distance = (((speedKmh / 3.6 * seconds) + route.PhaseMeters) % roundTrip + roundTrip) % roundTrip;
        var fraction = distance <= route.LengthMeters
            ? distance / route.LengthMeters
            : (roundTrip - distance) / route.LengthMeters;

        return GeoMath.PointAlong(route.Path, fraction);
    }

    private IReadOnlyList<Candidate> CandidatesOf(string region) =>
        _candidatesByRegion.TryGetValue(region, out var candidates) && candidates.Count > 0
            ? candidates
            : throw new ArgumentException($"Região sem logradouros candidatos: {region}.", nameof(region));

    private static (IReadOnlyList<GeoCoordinate> Path, double Length) LongestSegment(PrometeiaStreet street) =>
        street.Geometry
            .Where(path => path.Count >= 2)
            .Select(path => (Path: path, Length: LengthOf(path)))
            .OrderByDescending(item => item.Length)
            .DefaultIfEmpty((Path: (IReadOnlyList<GeoCoordinate>)[], Length: 0))
            .First();

    private static double LengthOf(IReadOnlyList<GeoCoordinate> path)
    {
        var length = 0.0;
        for (var i = 0; i < path.Count - 1; i++)
        {
            length += GeoMath.DistanceMeters(path[i], path[i + 1]);
        }

        return length;
    }

    private sealed record Candidate(PrometeiaStreet Street, IReadOnlyList<GeoCoordinate> Path, double LengthMeters);
}
