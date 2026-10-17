using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Argos.Api.Configuration;

/// <summary>
/// Escreve datas sempre em ISO 8601 UTC com o sufixo <c>Z</c> (plan 4.1, Art. III.8).
/// </summary>
internal sealed class UtcDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTimeOffset();

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture));
}
