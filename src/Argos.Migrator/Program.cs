using Argos.Application.Common.Abstractions;
using Argos.Application.Common.Exceptions;
using Argos.Application.Seeding;
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

// Só o caso de uso de que o Migrator precisa; o resto da Aplicação depende de portas que só a API implementa.
builder.Services.AddScoped<DemoUserProvisioningService>();

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

    // Os usuários de demonstração são reconciliados a cada execução, independentemente do seed (D-40).
    var demoUsers = builder.Configuration.GetSection(DemoUsersOptions.SectionName).Get<DemoUsersOptions>();
    if (!HasCredentials(demoUsers))
    {
        if (host.Services.GetRequiredService<IHostEnvironment>().IsEnvironment(ArgosEnvironments.Demo))
        {
            logger.LogCritical(
                "Usuários de demonstração não configurados. No ambiente Demo eles são obrigatórios: "
                    + "defina no .env os logins e as senhas DEMO_ADMIN_*, DEMO_OPERATOR_* e DEMO_MANAGER_*.");
            return 1;
        }

        logger.LogInformation("Usuários de demonstração não configurados; passo ignorado fora do ambiente Demo.");
    }
    else
    {
        var provisioning = scope.ServiceProvider.GetRequiredService<DemoUserProvisioningService>();
        var result = await provisioning.ProvisionAsync(demoUsers!);
        logger.LogInformation(
            "Usuários de demonstração reconciliados. Criados: {Created}. Atualizados: {Updated}.",
            result.Created.Count == 0 ? "nenhum" : string.Join(", ", result.Created),
            result.Updated.Count == 0 ? "nenhum" : string.Join(", ", result.Updated));
    }

    logger.LogInformation("Migrator concluído.");
    return 0;
}
catch (ValidationFailedException exception)
{
    // Só os campos e as regras; nunca os valores, que podem ser senhas.
    foreach (var (field, messages) in exception.Errors)
    {
        logger.LogCritical("Configuração inválida em DemoUsers ({Field}): {Message}", field, string.Join(" ", messages));
    }

    return 1;
}
catch (Exception exception)
{
    logger.LogCritical(exception, "Falha no Migrator.");
    return 1;
}

// Configurado quando algum perfil tem login ou senha; os nomes vêm sempre do appsettings.json.
static bool HasCredentials(DemoUsersOptions? options) =>
    options is not null
    && new[] { options.Administrator, options.Operator, options.Manager }.Any(user =>
        !string.IsNullOrWhiteSpace(user?.Login) || !string.IsNullOrWhiteSpace(user?.Password));
