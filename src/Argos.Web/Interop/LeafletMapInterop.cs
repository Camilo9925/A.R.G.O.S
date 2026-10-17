using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Argos.Web.Interop;

/// <summary>
/// Wrapper C# do módulo <c>js/map.js</c>. Cada instância controla um mapa.
/// </summary>
public sealed class LeafletMapInterop(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private IJSObjectReference? _module;
    private int? _mapId;

    public async Task CreateAsync(ElementReference element, double latitude, double longitude, int zoom, bool isDarkMode)
    {
        _module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/map.js");
        _mapId = await _module.InvokeAsync<int>("createMap", element, latitude, longitude, zoom, isDarkMode);
    }

    public async Task SetDarkModeAsync(bool isDarkMode)
    {
        if (_module is not null && _mapId is { } id)
        {
            await _module.InvokeVoidAsync("setDarkMode", id, isDarkMode);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            if (_mapId is { } id)
            {
                await _module.InvokeVoidAsync("disposeMap", id);
            }

            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // A página já foi fechada; não há o que liberar no navegador.
        }
    }
}
