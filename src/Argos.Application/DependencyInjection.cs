using Argos.Application.Access;
using Argos.Application.Diagnostics;
using Argos.Application.Inventory;
using Argos.Application.Inventory.Equipments;
using Argos.Application.Inventory.Geography;
using Argos.Application.Inventory.Support;
using Argos.Application.Inventory.Types;
using Argos.Application.Seeding;
using Argos.Application.Seeding.Inventory;
using Microsoft.Extensions.DependencyInjection;

namespace Argos.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registra os serviços de aplicação. As portas são implementadas e registradas pela API e pela Infraestrutura.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<TechnicalHeartbeatService>();
        services.AddScoped<LoginService>();
        services.AddScoped<DemoUserProvisioningService>();

        // Inventário (plan 15.12). Os auxiliares são por caso de uso: o calendário lê o município uma vez por escopo.
        services.AddScoped<MunicipalCalendar>();
        services.AddScoped<InventoryLocations>();
        services.AddScoped<InventoryReferenceNames>();
        services.AddScoped<MunicipalityService>();
        services.AddScoped<RegionService>();
        services.AddScoped<NeighborhoodService>();
        services.AddScoped<OrganizationService>();
        services.AddScoped<OperationalBaseService>();
        services.AddScoped<EquipmentTypeService>();
        services.AddScoped<EquipmentQueryService>();
        services.AddScoped<EquipmentService>();
        services.AddScoped<EquipmentStatusService>();
        services.AddScoped<MaintenanceService>();

        // Carga inicial (plan 15.10, 15.11.4).
        services.AddScoped<BusinessParameterProvisioningService>();
        services.AddScoped<InventoryReferenceSeeder>();
        services.AddScoped<EquipmentCatalogSeeder>();

        return services;
    }
}
