using Argos.Application.Access;
using Argos.Application.Common.Abstractions;
using Argos.Application.Inventory;
using Argos.Application.Inventory.Equipments;
using Argos.Application.Inventory.Geography;
using Argos.Application.Inventory.Support;
using Argos.Application.Inventory.Types;
using Argos.Infrastructure.Persistence;
using Argos.Infrastructure.Persistence.Interceptors;
using Argos.Infrastructure.Persistence.Queries;
using Argos.Infrastructure.Persistence.Repositories;
using Argos.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Argos.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registra o contexto, os interceptadores, o ponto de migração, a unidade de trabalho, os repositórios, as consultas,
    /// os parâmetros de negócio, o histórico do seed, o hash de senha e o health check do banco.
    /// Espera que <see cref="TimeProvider"/> e <c>ICurrentActor</c> já estejam registrados pelo host.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddScoped<AuditingInterceptor>();
        services.AddSingleton<DeleteBlockingInterceptor>();
        services.AddSingleton<AppendOnlyInterceptor>();
        services.AddSingleton<SearchTextInterceptor>();

        services.AddDbContext<ArgosDbContext>((serviceProvider, options) => options
            .UseArgosNpgsql(connectionString)
            .AddInterceptors(
                serviceProvider.GetRequiredService<DeleteBlockingInterceptor>(),
                serviceProvider.GetRequiredService<AppendOnlyInterceptor>(),
                serviceProvider.GetRequiredService<SearchTextInterceptor>(),
                serviceProvider.GetRequiredService<AuditingInterceptor>()));

        services.AddScoped<IDatabaseMigrator, DatabaseMigrator>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        services.AddScoped<IMunicipalityRepository, MunicipalityRepository>();
        services.AddScoped<IRegionRepository, RegionRepository>();
        services.AddScoped<INeighborhoodRepository, NeighborhoodRepository>();
        services.AddScoped<IZipCodeRepository, ZipCodeRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IOperationalBaseRepository, OperationalBaseRepository>();
        services.AddScoped<IEquipmentTypeRepository, EquipmentTypeRepository>();
        services.AddScoped<IEquipmentRepository, EquipmentRepository>();
        services.AddScoped<IInventoryChangeLog, InventoryChangeLog>();
        services.AddScoped<IInventoryQueries, InventoryQueries>();

        services.AddSingleton<BusinessParameterCache>();
        services.AddScoped<IBusinessParameters, BusinessParameterStore>();
        services.AddScoped<ISeedLedger, SeedLedger>();

        services.AddHealthChecks()
            .AddDbContextCheck<ArgosDbContext>("database");

        return services;
    }
}
