using System.Net;
using Argos.Contracts.Realtime;

namespace Argos.Simulator.Clients;

/// <summary>
/// Cliente HTTP tipado da API do ARGOS.
/// </summary>
internal sealed class ArgosApiClient(HttpClient httpClient)
{
    // Rota do endpoint de diagnóstico da API (T014), temporária como ele.
    private const string HeartbeatsPath = "api/diagnostics/heartbeats";

    public async Task<HttpStatusCode> SendHeartbeatAsync(TechnicalHeartbeat heartbeat, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(HeartbeatsPath, heartbeat, cancellationToken);
        return response.StatusCode;
    }
}
