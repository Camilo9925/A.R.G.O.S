using System.Text.Json;
using System.Text.Json.Serialization;

namespace Argos.Web.Services.Api;

/// <summary>Convenções de JSON da API: camelCase e enums como texto (plan 4.1).</summary>
public static class ApiJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
