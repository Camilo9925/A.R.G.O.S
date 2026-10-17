using System.Text.Json;
using System.Text.Json.Serialization;

namespace Argos.Api.Configuration;

/// <summary>
/// Convenções de JSON comuns ao HTTP e ao SignalR: camelCase, enums como texto e datas UTC (plan 4.1).
/// </summary>
internal static class JsonConfiguration
{
    public static void Apply(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new UtcDateTimeOffsetJsonConverter());
    }
}
