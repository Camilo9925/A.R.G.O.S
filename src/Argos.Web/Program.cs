using System.Globalization;
using Argos.Web;
using Argos.Web.Components.Map;
using Argos.Web.Services;
using Argos.Web.Services.Realtime;
using Argos.Web.Theme;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Exibição em pt-BR e no fuso da cidade da demonstração (Art. III.8, X.1; D-19).
var culture = CultureInfo.GetCultureInfo(RequiredSetting("Display:Culture"));
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;
builder.Services.AddSingleton(new DisplayTime(TimeZoneInfo.FindSystemTimeZoneById(RequiredSetting("Display:TimeZone"))));

// Endereço relativo atrás do proxy do nginx; em Development, a URL da API local (D-29).
var apiEndpoint = new ApiEndpoint(new Uri(
    builder.Configuration["Api:BaseUrl"] is { Length: > 0 } apiBaseUrl ? apiBaseUrl : builder.HostEnvironment.BaseAddress));
builder.Services.AddSingleton(apiEndpoint);
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = apiEndpoint.BaseAddress });

builder.Services.AddSingleton(new MapOptions(
    double.Parse(RequiredSetting("Map:Center:Latitude"), CultureInfo.InvariantCulture),
    double.Parse(RequiredSetting("Map:Center:Longitude"), CultureInfo.InvariantCulture),
    int.Parse(RequiredSetting("Map:Zoom"), CultureInfo.InvariantCulture)));

builder.Services.AddMudServices();
builder.Services.AddScoped<ThemePreference>();
builder.Services.AddSingleton<RealtimeConnection>();

await builder.Build().RunAsync();

string RequiredSetting(string key) =>
    builder.Configuration[key] is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException($"Configuração obrigatória ausente no appsettings da Web: {key}.");
