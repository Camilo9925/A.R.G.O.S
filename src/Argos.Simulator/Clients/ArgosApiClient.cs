using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Argos.Contracts.Telemetry;

namespace Argos.Simulator.Clients;

/// <summary>
/// Cliente HTTP tipado da API do ARGOS (plan 16.5): descoberta dos recursos e envio do lote de telemetria, sempre com a
/// chave de sistema (D-34). Enums pelo nome, como a API os serializa.
/// </summary>
internal sealed class ArgosApiClient(HttpClient httpClient)
{
    private const string ResourcesPath = "api/telemetry/resources";
    private const string BatchesPath = "api/telemetry/batches";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Recursos ativos, ou só o código HTTP quando a API recusa.</summary>
    public async Task<(HttpStatusCode Status, TelemetryResourceList? Resources)> GetResourcesAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(ResourcesPath, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return (response.StatusCode, null);
        }

        var resources = await response.Content.ReadFromJsonAsync<TelemetryResourceList>(Json, cancellationToken);
        return (response.StatusCode, resources);
    }

    /// <summary>Envia o lote; devolve o código HTTP e, no <c>202</c>, as contagens da API.</summary>
    public async Task<(HttpStatusCode Status, TelemetryBatchResult? Result)> SendBatchAsync(
        TelemetryBatchRequest batch, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(BatchesPath, batch, Json, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Accepted)
        {
            return (response.StatusCode, null);
        }

        var result = await response.Content.ReadFromJsonAsync<TelemetryBatchResult>(Json, cancellationToken);
        return (response.StatusCode, result);
    }
}
