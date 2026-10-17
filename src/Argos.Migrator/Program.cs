using Argos.Application.Common.Abstractions;
using Argos.Infrastructure.DependencyInjection;
using Argos.Infrastructure.Persistence;
using Argos.Migrator.Configuration;
using Argos.Simulation.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
});

var connectionString = builder.Configuration.GetConnectionString("Argos");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Configuração obrigatória ausente: ConnectionStrings:Argos (variável de ambiente ConnectionStrings__Argos).");
}

builder.Services.AddOptions<SimulationOptions>()
    .Bind(builder.Configuration.GetSection(SimulationOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ICurrentActor, SystemCurrentActor>();
builder.Services.AddInfrastructure(connectionString);

using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Argos.Migrator");

try
{
    var simulation = host.Services.GetRequiredService<IOptions<SimulationOptions>>().Value;

    await using var scope = host.Services.CreateAsyncScope();
    var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();

    logger.LogInformation("Aplicando migrations.");
    await migrator.MigrateAsync();

    var recorded = await migrator.RecordSeedAsync(SeedVersions.Foundation, simulation.Seed);
    if (recorded)
    {
        logger.LogInformation("Seed {Version} registrado com a semente {Seed}.", SeedVersions.Foundation, simulation.Seed);
    }
    else
    {
        logger.LogInformation("Seed {Version} com a semente {Seed} já estava aplicado; nada a fazer.", SeedVersions.Foundation, simulation.Seed);
    }

    logger.LogInformation("Migrator concluído.");
    return 0;
}
catch (Exception exception)
{
    logger.LogCritical(exception, "Falha no Migrator.");
    return 1;
}
