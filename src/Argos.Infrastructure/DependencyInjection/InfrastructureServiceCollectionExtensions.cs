using Argos.Infrastructure.Persistence;
using Argos.Infrastructure.Persistence.Interceptors;
using Microsoft.Extensions.DependencyInjection;

namespace Argos.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registra o contexto, os interceptadores, o ponto de migração e o health check do banco.
    /// Espera que <see cref="TimeProvider"/> e <c>ICurrentActor</c> já estejam registrados pelo host.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddScoped<AuditingInterceptor>();
        services.AddSingleton<DeleteBlockingInterceptor>();
        services.AddSingleton<AppendOnlyInterceptor>();

        services.AddDbContext<ArgosDbContext>((serviceProvider, options) => options
            .UseArgosNpgsql(connectionString)
            .AddInterceptors(
                serviceProvider.GetRequiredService<DeleteBlockingInterceptor>(),
                serviceProvider.GetRequiredService<AppendOnlyInterceptor>(),
                serviceProvider.GetRequiredService<AuditingInterceptor>()));

        services.AddScoped<IDatabaseMigrator, DatabaseMigrator>();

        services.AddHealthChecks()
            .AddDbContextCheck<ArgosDbContext>("database");

        return services;
    }
}
