using System.Net.Http.Json;

namespace Argos.Web.Services.Api;

/// <summary>
/// Base dos clientes tipados: JSON da API e erro como <see cref="ApiProblemException"/>. Falha de rede continua como
/// <see cref="HttpRequestException"/>, para a tela dizer que não conseguiu falar com o servidor.
/// </summary>
public abstract class ApiClient(HttpClient http)
{
    protected async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(path, cancellationToken);
        return await ReadAsync<T>(response, cancellationToken);
    }

    protected async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var response = await SendCoreAsync(method, path, body, cancellationToken);
        return await ReadAsync<T>(response, cancellationToken);
    }

    /// <summary>Operação sem corpo de resposta, como a inativação (<c>204</c>).</summary>
    protected async Task SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var response = await SendCoreAsync(method, path, body, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await ApiProblemException.ReadAsync(response, cancellationToken);
        }
    }

    private Task<HttpResponseMessage> SendCoreAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = body is null ? null : JsonContent.Create(body, body.GetType(), options: ApiJson.Options),
        };

        return http.SendAsync(request, cancellationToken);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw await ApiProblemException.ReadAsync(response, cancellationToken);
        }

        return await response.Content.ReadFromJsonAsync<T>(ApiJson.Options, cancellationToken)
            ?? throw new InvalidOperationException("Resposta vazia da API.");
    }
}
