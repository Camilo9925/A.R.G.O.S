using System.Globalization;
using Argos.Contracts.Access;
using Argos.Web;
using Argos.Web.Auth;
using Argos.Web.Components.Map;
using Argos.Web.Services;
using Argos.Web.Services.Api;
using Argos.Web.Services.Realtime;
using Argos.Web.Theme;
using Microsoft.AspNetCore.Components.Authorization;
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

// Sessão, usuário da interface e políticas por permissão (D-42; plan 14.11). No WebAssembly há um só usuário por
// aplicação, por isso os serviços de sessão são singletons.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<SessionStore>();
builder.Services.AddSingleton<ArgosAuthenticationStateProvider>();
builder.Services.AddSingleton<AuthenticationStateProvider>(services =>
    services.GetRequiredService<ArgosAuthenticationStateProvider>());
builder.Services.AddSingleton<SessionService>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorizationCore(options =>
{
    // Sem matriz no cliente: cada política só confere a permissão recebida no login.
    foreach (var permission in Permissions.All)
    {
        options.AddPolicy(permission, policy => policy.RequireClaim(AccessClaimTypes.Permission, permission));
    }
});

// O token vai em toda requisição à API; um 401 com token encerra a sessão.
builder.Services.AddScoped(services => new HttpClient(new AuthorizationMessageHandler(
    services.GetRequiredService<SessionStore>(),
    services.GetRequiredService<SessionService>())
{
    InnerHandler = new HttpClientHandler(),
})
{
    BaseAddress = apiEndpoint.BaseAddress,
});

// Clientes tipados do Inventário sobre o mesmo HttpClient autenticado (plan 15.15).
builder.Services.AddScoped<InventoryReferenceClient>();
builder.Services.AddScoped<EquipmentClient>();
builder.Services.AddScoped<AgentClient>();
builder.Services.AddScoped<OperationsClient>();

// Preenchimento rápido da tela de login, só quando o nginx do ambiente Demo habilita (D-41).
builder.Services.AddSingleton(DemoLoginOptions.From(builder.Configuration));

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
