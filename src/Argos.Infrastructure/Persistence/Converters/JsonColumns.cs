using System.Globalization;
using System.Text.Json;
using Argos.Domain.Inventory;
using Argos.Domain.Inventory.Geography;
using Argos.Domain.Inventory.Types;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argos.Infrastructure.Persistence.Converters;

/// <summary>
/// Colunas <c>jsonb</c> do Inventário (D-48; plan 15.13), com <c>System.Text.Json</c> e sem pacote novo. Cada valor
/// tem conversor e comparador próprios: o comparador olha o conteúdo, para que o EF só grave a coluna quando o conteúdo
/// muda, e não quando o Domínio troca a instância por outra igual. Os objetos de valor são imutáveis, então o
/// instantâneo do rastreamento pode ser a própria instância.
/// </summary>
internal static class JsonColumns
{
    private const string Jsonb = "jsonb";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Contorno em GeoJSON (<c>Polygon</c>, posições em longitude e latitude; plan 15.13). Serve também ao contorno
    /// opcional da região: o EF não chama o conversor para nulo.
    /// </summary>
    public static PropertyBuilder<GeoPolygon> HasGeoJson(this PropertyBuilder<GeoPolygon> property) =>
        property
            .HasConversion(
                polygon => WritePolygon(polygon),
                json => ReadPolygon(json),
                new ValueComparer<GeoPolygon>(
                    (left, right) => SamePolygon(left, right),
                    polygon => polygon == null ? 0 : polygon.Rings.Count,
                    polygon => polygon))
            .HasColumnType(Jsonb);

    /// <summary>
    /// Valores das características: objeto com a chave de cada uma. Inteiros saem sem ponto e decimais sempre com ponto
    /// (<c>12.0</c>), para que a leitura devolva o mesmo tipo (<see cref="long"/> ou <see cref="decimal"/>) que o
    /// Domínio gravou; o <c>numeric</c> do <c>jsonb</c> preserva as casas.
    /// </summary>
    public static PropertyBuilder<AttributeValues> HasAttributeJson(this PropertyBuilder<AttributeValues> property) =>
        property
            .HasConversion(
                values => WriteAttributes(values),
                json => ReadAttributes(json),
                new ValueComparer<AttributeValues>(
                    (left, right) => left == null ? right == null : left.Equals(right),
                    values => values.GetHashCode(),
                    values => values))
            .HasColumnType(Jsonb);

    public static PropertyBuilder<IReadOnlyList<AttributeOption>> HasOptionsJson(
        this PropertyBuilder<IReadOnlyList<AttributeOption>> property) =>
        property.HasJsonList();

    public static PropertyBuilder<IReadOnlyList<FieldChange>> HasChangesJson(
        this PropertyBuilder<IReadOnlyList<FieldChange>> property) =>
        property.HasJsonList();

    private static PropertyBuilder<IReadOnlyList<T>> HasJsonList<T>(this PropertyBuilder<IReadOnlyList<T>> property) =>
        property
            .HasConversion(
                list => JsonSerializer.Serialize(list, Options),
                json => JsonSerializer.Deserialize<T[]>(json, Options) ?? Array.Empty<T>(),
                new ValueComparer<IReadOnlyList<T>>(
                    (left, right) => left == null ? right == null : right != null && left.SequenceEqual(right),
                    list => list.Count,
                    list => list.ToArray()))
            .HasColumnType(Jsonb);

    private static bool SamePolygon(GeoPolygon? left, GeoPolygon? right) =>
        left is null || right is null
            ? left is null && right is null
            : left.Rings.Count == right.Rings.Count
                && left.Rings.Zip(right.Rings).All(pair => pair.First.SequenceEqual(pair.Second));

    private static string WritePolygon(GeoPolygon polygon) =>
        JsonSerializer.Serialize(
            new GeoJsonPolygon(
                "Polygon",
                polygon.Rings.Select(ring => ring.Select(point => new[] { point.Longitude, point.Latitude }).ToArray()).ToArray()),
            Options);

    private static GeoPolygon ReadPolygon(string json)
    {
        var geoJson = JsonSerializer.Deserialize<GeoJsonPolygon>(json, Options)
            ?? throw new InvalidOperationException("Contorno GeoJSON vazio.");

        return GeoPolygon.Create(geoJson.Coordinates.Select(ring => ring.Select(position => GeoPoint.Create(position[1], position[0]))));
    }

    private static string WriteAttributes(AttributeValues values)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();

            foreach (var (key, value) in values.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(key);

                switch (value)
                {
                    case string text:
                        writer.WriteStringValue(text);
                        break;
                    case bool flag:
                        writer.WriteBooleanValue(flag);
                        break;
                    case long number:
                        writer.WriteNumberValue(number);
                        break;
                    case decimal number:
                        writer.WriteRawValue(number.ToString("0.0###########################", CultureInfo.InvariantCulture));
                        break;
                    case IEnumerable<string> keys:
                        writer.WriteStartArray();
                        foreach (var item in keys)
                        {
                            writer.WriteStringValue(item);
                        }

                        writer.WriteEndArray();
                        break;
                    default:
                        throw new InvalidOperationException($"Tipo de valor não suportado na característica \"{key}\".");
                }
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static AttributeValues ReadAttributes(string json)
    {
        using var document = JsonDocument.Parse(json);
        var values = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var property in document.RootElement.EnumerateObject())
        {
            var element = property.Value;
            values[property.Name] = element.ValueKind switch
            {
                JsonValueKind.String => element.GetString()!,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number when element.GetRawText().AsSpan().IndexOfAny('.', 'e', 'E') >= 0 => element.GetDecimal(),
                JsonValueKind.Number => element.GetInt64(),
                JsonValueKind.Array => element.EnumerateArray().Select(item => item.GetString()!).ToArray(),
                _ => throw new InvalidOperationException($"Valor inválido na característica \"{property.Name}\"."),
            };
        }

        return AttributeValues.Restore(values);
    }

    private sealed record GeoJsonPolygon(string Type, double[][][] Coordinates);
}
