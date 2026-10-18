using Argos.Domain.Common;

namespace Argos.Domain.Inventory.Geography;

/// <summary>
/// Polígono simples (plan 15.2.4): o primeiro anel é o contorno e os demais, buracos. Cada anel tem ao menos 4
/// vértices e é fechado, com o último igual ao primeiro (15.2.1). <see cref="Contains"/> usa ponto em polígono
/// (ray casting, plan 5.1 e 9.5); um ponto sobre uma aresta, do contorno ou de um buraco, conta como dentro.
/// </summary>
public sealed class GeoPolygon
{
    public const int MinRingVertices = 4;

    private GeoPolygon(IReadOnlyList<IReadOnlyList<GeoPoint>> rings) => Rings = rings;

    /// <summary>Anéis fechados; o primeiro é o contorno.</summary>
    public IReadOnlyList<IReadOnlyList<GeoPoint>> Rings { get; }

    public IReadOnlyList<GeoPoint> Outer => Rings[0];

    /// <summary>Valida e cria o polígono. Erro no campo <paramref name="field"/>.</summary>
    public static GeoPolygon Create(IEnumerable<IEnumerable<GeoPoint>> rings, string field = "boundary")
    {
        var list = rings.Select(ring => (IReadOnlyList<GeoPoint>)ring.ToArray()).ToArray();

        if (list.Length == 0 || list.Any(ring => ring.Count < MinRingVertices || ring[0] != ring[^1]))
        {
            throw new DomainValidationException(
                field, $"O contorno deve ser fechado e ter ao menos {MinRingVertices} vértices.");
        }

        return new GeoPolygon(list);
    }

    /// <summary>Polígono sem buracos.</summary>
    public static GeoPolygon FromRing(IEnumerable<GeoPoint> ring, string field = "boundary") => Create([ring], field);

    public bool Contains(GeoPoint point)
    {
        if (Rings.Any(ring => IsOnBoundary(ring, point)))
        {
            return true;
        }

        return IsInside(Outer, point) && !Rings.Skip(1).Any(hole => IsInside(hole, point));
    }

    private static bool IsInside(IReadOnlyList<GeoPoint> ring, GeoPoint point)
    {
        var inside = false;

        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            var (yi, xi) = (ring[i].Latitude, ring[i].Longitude);
            var (yj, xj) = (ring[j].Latitude, ring[j].Longitude);

            if ((yi > point.Latitude) != (yj > point.Latitude)
                && point.Longitude < ((xj - xi) * (point.Latitude - yi) / (yj - yi)) + xi)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static bool IsOnBoundary(IReadOnlyList<GeoPoint> ring, GeoPoint point)
    {
        // Tolerância bem abaixo do arredondamento de 6 casas dos pontos.
        const double Tolerance = 1e-9;

        for (var i = 0; i < ring.Count - 1; i++)
        {
            var (a, b) = (ring[i], ring[i + 1]);
            var cross = ((b.Longitude - a.Longitude) * (point.Latitude - a.Latitude))
                - ((b.Latitude - a.Latitude) * (point.Longitude - a.Longitude));

            if (Math.Abs(cross) > Tolerance)
            {
                continue;
            }

            if (point.Longitude >= Math.Min(a.Longitude, b.Longitude) - Tolerance
                && point.Longitude <= Math.Max(a.Longitude, b.Longitude) + Tolerance
                && point.Latitude >= Math.Min(a.Latitude, b.Latitude) - Tolerance
                && point.Latitude <= Math.Max(a.Latitude, b.Latitude) + Tolerance)
            {
                return true;
            }
        }

        return false;
    }
}
