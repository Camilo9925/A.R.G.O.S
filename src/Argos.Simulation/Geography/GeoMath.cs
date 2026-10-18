namespace Argos.Simulation.Geography;

/// <summary>
/// Cálculos geográficos simples da simulação: ponto em polígono (ray casting, como o Domínio; plan 5.1) e distâncias
/// numa projeção equirretangular local, precisa o bastante na escala de uma cidade.
/// </summary>
public static class GeoMath
{
    private const double MetersPerDegree = 111_320;

    /// <summary>Ponto dentro do anel fechado; um ponto sobre a aresta pode cair de qualquer lado.</summary>
    public static bool Contains(IReadOnlyList<GeoCoordinate> ring, GeoCoordinate point)
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

    /// <summary>Menor distância, em metros, do ponto às arestas do anel fechado.</summary>
    public static double DistanceToRingMeters(IReadOnlyList<GeoCoordinate> ring, GeoCoordinate point)
    {
        var best = double.MaxValue;

        for (var i = 0; i < ring.Count - 1; i++)
        {
            best = Math.Min(best, DistanceToSegmentMeters(ring[i], ring[i + 1], point));
        }

        return best;
    }

    /// <summary>Ponto a uma fração do comprimento do trecho (0 = início, 1 = fim).</summary>
    public static GeoCoordinate PointAlong(IReadOnlyList<GeoCoordinate> polyline, double fraction)
    {
        var lengths = new double[polyline.Count - 1];
        for (var i = 0; i < lengths.Length; i++)
        {
            lengths[i] = DistanceMeters(polyline[i], polyline[i + 1]);
        }

        var target = lengths.Sum() * Math.Clamp(fraction, 0, 1);

        for (var i = 0; i < lengths.Length; i++)
        {
            if (target <= lengths[i] && lengths[i] > 0)
            {
                var t = target / lengths[i];
                var (a, b) = (polyline[i], polyline[i + 1]);
                return new GeoCoordinate(
                    a.Latitude + ((b.Latitude - a.Latitude) * t),
                    a.Longitude + ((b.Longitude - a.Longitude) * t));
            }

            target -= lengths[i];
        }

        return polyline[^1];
    }

    public static double DistanceMeters(GeoCoordinate a, GeoCoordinate b)
    {
        var (x, y) = Project(a, b);
        return Math.Sqrt((x * x) + (y * y));
    }

    private static double DistanceToSegmentMeters(GeoCoordinate a, GeoCoordinate b, GeoCoordinate point)
    {
        var (bx, by) = Project(a, b);
        var (px, py) = Project(a, point);
        var lengthSquared = (bx * bx) + (by * by);
        var t = lengthSquared == 0 ? 0 : Math.Clamp(((px * bx) + (py * by)) / lengthSquared, 0, 1);
        var (dx, dy) = (px - (t * bx), py - (t * by));
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>Vetor de <paramref name="origin"/> a <paramref name="point"/>, em metros (x = leste, y = norte).</summary>
    private static (double X, double Y) Project(GeoCoordinate origin, GeoCoordinate point) =>
        ((point.Longitude - origin.Longitude) * MetersPerDegree * Math.Cos(origin.Latitude * Math.PI / 180),
            (point.Latitude - origin.Latitude) * MetersPerDegree);
}
