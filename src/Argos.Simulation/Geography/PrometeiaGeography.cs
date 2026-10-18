using System.Reflection;
using System.Text.Json;

namespace Argos.Simulation.Geography;

/// <summary>Coordenada geográfica dos dados de Prometeia.</summary>
public sealed record GeoCoordinate(double Latitude, double Longitude);

/// <summary>Classe de desenho da via, como no <c>streets.json</c> (D-08).</summary>
public enum StreetClass
{
    Local,
    Secondary,
    Major,
}

public sealed record PrometeiaMunicipality(
    string Name, string State, string TimeZoneId, GeoCoordinate Center, IReadOnlyList<GeoCoordinate> Boundary);

public sealed record PrometeiaRegion(string Name, IReadOnlyList<GeoCoordinate> Boundary);

public sealed record PrometeiaNeighborhood(string Name, string Region, GeoCoordinate Center);

/// <summary>Um logradouro dentro de um bairro, com o seu CEP (D-52): os trechos da malha e o ponto de referência.</summary>
public sealed record PrometeiaStreet(
    string ZipCode,
    string Name,
    StreetClass Class,
    string Neighborhood,
    GeoCoordinate ReferencePoint,
    IReadOnlyList<IReadOnlyList<GeoCoordinate>> Geometry);

/// <summary>
/// Geografia da cidade fictícia Prometeia/SP (D-07, D-51; plan 15.11.1), lida do <c>prometeia.json</c> embutido no
/// assembly. O arquivo guarda os contornos como anéis abertos; a leitura os devolve fechados, com o primeiro vértice
/// repetido no fim, como exige o invariante de 15.2.1.
/// </summary>
public sealed class PrometeiaGeography
{
    private const string ResourceName = "Argos.Simulation.Data.prometeia.json";

    private static readonly Lazy<PrometeiaGeography> Embedded = new(LoadEmbedded);

    private readonly Dictionary<string, PrometeiaNeighborhood> _neighborhoodsByName;

    private PrometeiaGeography(
        PrometeiaMunicipality municipality,
        string zipCodePrefix,
        IReadOnlyList<PrometeiaRegion> regions,
        IReadOnlyList<PrometeiaNeighborhood> neighborhoods,
        IReadOnlyList<PrometeiaStreet> streets)
    {
        Municipality = municipality;
        ZipCodePrefix = zipCodePrefix;
        Regions = regions;
        Neighborhoods = neighborhoods;
        Streets = streets;
        _neighborhoodsByName = neighborhoods.ToDictionary(neighborhood => neighborhood.Name, StringComparer.Ordinal);
    }

    public PrometeiaMunicipality Municipality { get; }

    /// <summary>Prefixo de 5 dígitos, exclusivo de Prometeia, de todos os CEPs.</summary>
    public string ZipCodePrefix { get; }

    /// <summary>As 5 zonas: Centro, Zona Norte, Zona Sul, Zona Leste e Zona Oeste.</summary>
    public IReadOnlyList<PrometeiaRegion> Regions { get; }

    public IReadOnlyList<PrometeiaNeighborhood> Neighborhoods { get; }

    public IReadOnlyList<PrometeiaStreet> Streets { get; }

    /// <summary>A geografia embutida, lida uma vez.</summary>
    public static PrometeiaGeography Load() => Embedded.Value;

    /// <summary>Lê a geografia de um JSON no formato do <c>prometeia.json</c> (descrito no <c>VERSION</c>).</summary>
    public static PrometeiaGeography Parse(Stream json)
    {
        var data = JsonSerializer.Deserialize<FileData>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("Arquivo de geografia vazio.");

        var municipality = new PrometeiaMunicipality(
            data.Municipality.Name,
            data.Municipality.State,
            data.Municipality.TimeZoneId,
            Coordinate(data.Municipality.Center),
            ClosedRing(data.Municipality.Boundary));

        var regions = data.Regions.Select(region => new PrometeiaRegion(region.Name, ClosedRing(region.Boundary))).ToArray();
        var neighborhoods = data.Neighborhoods
            .Select(neighborhood => new PrometeiaNeighborhood(neighborhood.Name, neighborhood.Region, Coordinate(neighborhood.Center)))
            .ToArray();
        var streets = data.Streets.Select(street => new PrometeiaStreet(
                street.ZipCode,
                street.Street,
                ParseClass(street.Class),
                street.Neighborhood,
                Coordinate(street.ReferencePoint),
                street.Geometry.Select(Polyline).ToArray()))
            .ToArray();

        return new PrometeiaGeography(municipality, data.ZipCodePrefix, regions, neighborhoods, streets);
    }

    public PrometeiaNeighborhood Neighborhood(string name) =>
        _neighborhoodsByName.TryGetValue(name, out var neighborhood)
            ? neighborhood
            : throw new KeyNotFoundException($"Bairro desconhecido: {name}.");

    /// <summary>Região (zona) do logradouro, pela região do seu bairro (RN-22).</summary>
    public string RegionOf(PrometeiaStreet street) => Neighborhood(street.Neighborhood).Region;

    private static PrometeiaGeography LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Recurso {ResourceName} não encontrado no assembly.");
        return Parse(stream);
    }

    private static GeoCoordinate Coordinate(double[] pair) =>
        pair.Length == 2 ? new GeoCoordinate(pair[0], pair[1]) : throw new InvalidDataException("Coordenada inválida.");

    /// <summary>Fecha o anel, repetindo o primeiro vértice no fim, se o arquivo o trouxer aberto (plan 15.11.1).</summary>
    private static GeoCoordinate[] ClosedRing(double[][] ring)
    {
        var points = ring.Select(Coordinate).ToList();

        if (points.Count > 0 && points[0] != points[^1])
        {
            points.Add(points[0]);
        }

        return points.ToArray();
    }

    /// <summary>Trecho no formato compacto <c>[lat, lon, lat, lon, …]</c>.</summary>
    private static GeoCoordinate[] Polyline(double[] flat)
    {
        if (flat.Length < 4 || flat.Length % 2 != 0)
        {
            throw new InvalidDataException("Trecho de logradouro inválido.");
        }

        return Enumerable.Range(0, flat.Length / 2).Select(i => new GeoCoordinate(flat[2 * i], flat[(2 * i) + 1])).ToArray();
    }

    private static StreetClass ParseClass(string value) => value switch
    {
        "local" => StreetClass.Local,
        "secondary" => StreetClass.Secondary,
        "major" => StreetClass.Major,
        _ => throw new InvalidDataException($"Classe de via desconhecida: {value}."),
    };

    private sealed record FileData(
        MunicipalityData Municipality,
        string ZipCodePrefix,
        RegionData[] Regions,
        NeighborhoodData[] Neighborhoods,
        StreetData[] Streets);

    private sealed record MunicipalityData(string Name, string State, string TimeZoneId, double[] Center, double[][] Boundary);

    private sealed record RegionData(string Name, double[][] Boundary);

    private sealed record NeighborhoodData(string Name, string Region, double[] Center);

    private sealed record StreetData(
        string ZipCode, string Street, string Class, string Neighborhood, double[] ReferencePoint, double[][] Geometry);
}
