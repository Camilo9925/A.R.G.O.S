using System.Text.Json;
using Argos.Contracts.Access;
using Microsoft.JSInterop;

namespace Argos.Web.Auth;

/// <summary>
/// Sessão do usuário (a <see cref="LoginResponse"/> recebida no login) guardada no <c>sessionStorage</c>: vale só para
/// a aba e some ao fechá-la (D-09, D-42). Sessão vencida é descartada. Se o armazenamento não estiver disponível,
/// a sessão vale só enquanto a página estiver aberta.
/// </summary>
public sealed class SessionStore(IJSRuntime jsRuntime, TimeProvider timeProvider)
{
    private const string StorageKey = "argos.session";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private LoginResponse? _session;
    private bool _loaded;

    /// <summary>Sessão vigente, ou <c>null</c> se não houver ou se já tiver vencido.</summary>
    public LoginResponse? Current => _session is { } session && !IsExpired(session) ? session : null;

    /// <summary>Indica se há uma sessão guardada, mesmo que vencida.</summary>
    public bool HasSession => _session is not null;

    /// <summary>Lê a sessão do navegador na primeira chamada e descarta a sessão vencida.</summary>
    public async Task<LoginResponse?> GetAsync()
    {
        if (!_loaded)
        {
            _session = await ReadAsync();
            _loaded = true;
        }

        if (_session is { } session && IsExpired(session))
        {
            await ClearAsync();
        }

        return _session;
    }

    public async Task SaveAsync(LoginResponse session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        _loaded = true;

        try
        {
            await jsRuntime.InvokeVoidAsync("sessionStorage.setItem", StorageKey, JsonSerializer.Serialize(session, JsonOptions));
        }
        catch (JSException)
        {
            // Sem sessionStorage: a sessão vale só enquanto a página estiver aberta.
        }
    }

    public async Task ClearAsync()
    {
        _session = null;
        _loaded = true;

        try
        {
            await jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", StorageKey);
        }
        catch (JSException)
        {
            // Nada guardado no navegador.
        }
    }

    private bool IsExpired(LoginResponse session) => session.ExpiresAt <= timeProvider.GetUtcNow();

    private async Task<LoginResponse?> ReadAsync()
    {
        try
        {
            var json = await jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", StorageKey);
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<LoginResponse>(json, JsonOptions);
        }
        catch (Exception exception) when (exception is JSException or JsonException)
        {
            return null;
        }
    }
}
