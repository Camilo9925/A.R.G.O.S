using Argos.Application.Access;
using Argos.Application.Common.Abstractions;
using Argos.Infrastructure.Persistence;
using Argos.Infrastructure.Persistence.Interceptors;
using Argos.Infrastructure.Persistence.Repositories;
using Argos.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Argos.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registra o contexto, os interceptadores, o ponto de migração, a unidade de trabalho, os repositórios,
    /// o hash de senha e o health check do banco.
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
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        services.AddHealthChecks()
            .AddDbContextCheck<ArgosDbContext>("database");

        return services;
    }
}
