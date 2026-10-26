using Argos.Contracts.Access;
using Argos.Simulation.Configuration;
using Argos.Simulator.Clients;
using Argos.Simulator.Configuration;
using Argos.Simulator.Workers;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
});

builder.Services.AddOptions<SimulatorOptions>()
    .Bind(builder.Configuration.GetSection(SimulatorOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<TelemetryOptions>()
    .Bind(builder.Configuration.GetSection(TelemetryOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => options.SpeedsKmh is { Vehicle: > 0, Motorcycle: > 0, Scooter: > 0, OnFoot: > 0 },
        "As velocidades de Simulator:Telemetry:SpeedsKmh devem ser maiores que zero.")
    .ValidateOnStart();

builder.Services.AddOptions<SimulationOptions>()
    .Bind(builder.Configuration.GetSection(SimulationOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddHttpClient<ArgosApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<SimulatorOptions>>().Value;
    client.BaseAddress = new Uri(options.ApiBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add(SystemKeyContract.HeaderName, options.SystemKey);
});

builder.Services.AddHostedService<TelemetryWorker>();

builder.Services.AddHealthChecks();

var app = builder.Build();

// Vivo: o processo responde (plan 7.3, D-31).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

app.Run();
