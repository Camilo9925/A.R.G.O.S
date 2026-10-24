using Argos.Application.Common.Exceptions;
using Argos.Application.Seeding;
using Argos.Application.Seeding.Inventory;
using Argos.Simulation.Configuration;
using Argos.Simulation.Generators;
using Argos.Simulation.Geography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Argos.Migrator.Seeding;

/// <summary>
/// Carga do Inventário pelo Migrator (D-55, D-56; plan 15.10, 15.11.4), depois das migrations e do registro de
/// fundação: parâmetros de negócio (a cada execução, só os que faltam), referência, catálogo de equipamentos e, na
/// Fase 3, agentes com vínculos e inativações de demonstração, que entram num volume da Fase 2 sem <c>down -v</c>. Cada
/// etapa roda num escopo próprio e grava num só <c>SaveChanges</c>, junto com o seu registro no histórico do seed; uma
/// falha no meio não deixa linhas nem registro daquela versão.
/// </summary>
internal static class InventoryLoad
{
    /// <returns><see langword="false"/> se a configuração dos parâmetros de negócio for inválida (já registrado no log).</returns>
    public static async Task<bool> RunAsync(
        IServiceProvider services,
        IConfiguration configuration,
        SimulationOptions simulation,
        TimeProvider timeProvider,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (!await ProvisionParametersAsync(services, configuration, logger, cancellationToken))
        {
            return false;
        }

        // Âncora relativa a "agora" na demonstração e fixa quando configurada, como nos testes (D-03).
        var anchor = simulation.AnchorDate ?? timeProvider.GetUtcNow();
        var geography = PrometeiaGeography.Load();
        var catalog = InventoryCatalogGenerator.Generate(simulation.Seed, geography);
        var agents = AgentCatalogGenerator.Generate(simulation.Seed, catalog);

        await using (var scope = services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<InventoryReferenceSeeder>().SeedAsync(
                InventorySeedInput.Reference(geography, catalog),
                simulation.Seed,
                InventorySeedInput.ReferenceRegisteredAt(catalog, anchor),
                cancellationToken);
            Log(logger, result, simulation.Seed);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<EquipmentCatalogSeeder>().SeedAsync(
                InventorySeedInput.Catalog(catalog), simulation.Seed, anchor, cancellationToken);
            Log(logger, result, simulation.Seed);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<AgentCatalogSeeder>().SeedAsync(
                InventorySeedInput.Agents(agents), simulation.Seed, anchor, cancellationToken);
            Log(logger, result, simulation.Seed);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<InactivationSeeder>().SeedAsync(
                InventorySeedInput.Inactivations(agents), simulation.Seed, anchor, cancellationToken);
            Log(logger, result, simulation.Seed);
        }

        return true;
    }

    private static async Task<bool> ProvisionParametersAsync(
        IServiceProvider services, IConfiguration configuration, ILogger logger, CancellationToken cancellationToken)
    {
        var configured = configuration.GetSection(BusinessParameterProvisioningService.SectionName).Get<Dictionary<string, int>>();

        try
        {
            await using var scope = services.CreateAsyncScope();
            var created = await scope.ServiceProvider.GetRequiredService<BusinessParameterProvisioningService>()
                .ProvisionAsync(configured, cancellationToken);
            logger.LogInformation(
                "Parâmetros de negócio reconciliados. Criados: {Created}.",
                created.Count == 0 ? "nenhum" : string.Join(", ", created));
            return true;
        }
        catch (ValidationFailedException exception)
        {
            foreach (var (field, messages) in exception.Errors)
            {
                logger.LogCritical("Configuração inválida em {Field}: {Message}", field, string.Join(" ", messages));
            }

            return false;
        }
    }

    private static void Log(ILogger logger, SeedResult result, int seed)
    {
        if (result.AlreadyApplied)
        {
            logger.LogInformation("Carga {Version} com a semente {Seed} já aplicada; nada a fazer.", result.Version, seed);
            return;
        }

        var counts = string.Join(", ", result.Counts.Select(count => $"{count.Key}={count.Value}"));

        // Um item pulado ("*Skipped") só acontece num volume alterado depois da semente: a carga segue, mas avisa.
        if (result.Counts.Any(count => count.Key.EndsWith("Skipped", StringComparison.Ordinal) && count.Value > 0))
        {
            logger.LogWarning(
                "Carga {Version} aplicada com a semente {Seed}, com itens pulados: {Counts}. O volume foi alterado " +
                "depois da semente; docker compose down -v recria o estado da semente.",
                result.Version,
                seed,
                counts);
            return;
        }

        logger.LogInformation("Carga {Version} aplicada com a semente {Seed}: {Counts}.", result.Version, seed, counts);
    }
}
