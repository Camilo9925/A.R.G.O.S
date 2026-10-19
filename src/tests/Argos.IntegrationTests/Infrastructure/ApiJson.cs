using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Argos.IntegrationTests.Infrastructure;

/// <summary>JSON como a API o produz e aceita: camelCase e enums como texto (plan 4.1).</summary>
internal static class ApiJson
{
    public static JsonSerializerOptions Options { get; } =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Lê o corpo da resposta, que deve existir.</summary>
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadFromJsonAsync<T>(Options, cancellationToken);
        Assert.NotNull(body);
        return body;
    }

    public static Task<HttpResponseMessage> PostJsonAsync(
        this HttpClient client, string uri, object body, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(uri, body, Options, cancellationToken);

    public static Task<HttpResponseMessage> PutJsonAsync(
        this HttpClient client, string uri, object body, CancellationToken cancellationToken) =>
        client.PutAsJsonAsync(uri, body, Options, cancellationToken);
}
