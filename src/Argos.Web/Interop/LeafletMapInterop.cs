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

    /// <summary>
    /// Mapa de seleção de ponto (plan 15.9): contorno do município, marcador arrastável e, fora do modo somente
    /// leitura, o ponto devolvido a <paramref name="receiver"/> pelo método <c>OnPointSelected(latitude, longitude)</c>.
    /// </summary>
    public async Task CreatePickerAsync<TReceiver>(
        ElementReference element,
        PickerOptions options,
        DotNetObjectReference<TReceiver>? receiver)
        where TReceiver : class
    {
        _module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/map.js");
        _mapId = await _module.InvokeAsync<int>("createPicker", element, options, receiver);
    }

    public async Task SetPickerPointAsync(double latitude, double longitude)
    {
        if (_module is not null && _mapId is { } id)
        {
            await _module.InvokeVoidAsync("setPickerPoint", id, latitude, longitude);
        }
    }

    /// <summary>Desenha a lista completa de marcadores da Central; o clique volta em <c>OnMarkerClicked(key)</c> (plan 16.12).</summary>
    public async Task SetMarkersAsync<TReceiver>(IReadOnlyList<MapMarker> markers, DotNetObjectReference<TReceiver> receiver)
        where TReceiver : class
    {
        if (_module is not null && _mapId is { } id)
        {
            await _module.InvokeVoidAsync("setMarkers", id, markers, receiver);
        }
    }

    /// <summary>
    /// Desenha a lista completa de regiões da Preditiva (polígono colorido, contorno tracejado para dados insuficientes,
    /// rótulo fixo); o clique volta em <c>OnRegionClicked(key)</c> (plan 17.12).
    /// </summary>
    public async Task SetRegionsAsync<TReceiver>(IReadOnlyList<MapRegion> regions, DotNetObjectReference<TReceiver> receiver)
        where TReceiver : class
    {
        if (_module is not null && _mapId is { } id)
        {
            await _module.InvokeVoidAsync("setRegions", id, regions, receiver);
        }
    }

    public async Task UpdatePositionsAsync(IReadOnlyList<MapPositionUpdate> updates)
    {
        if (_module is not null && _mapId is { } id && updates.Count > 0)
        {
            await _module.InvokeVoidAsync("updatePositions", id, updates);
        }
    }

    public async Task FocusOnAsync(double latitude, double longitude, int zoom, string? key = null)
    {
        if (_module is not null && _mapId is { } id)
        {
            await _module.InvokeVoidAsync("focusOn", id, latitude, longitude, zoom, key);
        }
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
