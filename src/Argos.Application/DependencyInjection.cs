using Argos.Application.Access;
using Argos.Application.Inventory;
using Argos.Application.Inventory.Agents;
using Argos.Application.Inventory.Equipments;
using Argos.Application.Inventory.Geography;
using Argos.Application.Inventory.Support;
using Argos.Application.Inventory.Timeline;
using Argos.Application.Inventory.Types;
using Argos.Application.Operations;
using Argos.Application.Predictive;
using Argos.Application.Seeding;
using Argos.Application.Seeding.Inventory;
using Argos.Application.Seeding.Operations;
using Argos.Application.Seeding.Predictive;
using Argos.Application.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace Argos.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registra os serviços de aplicação. As portas são implementadas e registradas pela API e pela Infraestrutura.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
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

        // Fase 3 (plan 15.12): entram com as implementações das portas dos agentes, dos vínculos e da linha do tempo.
        services.AddScoped<EquipmentInactivationService>();
        services.AddScoped<AgentQueryService>();
        services.AddScoped<AgentService>();
        services.AddScoped<AgentStatusService>();
        services.AddScoped<AssignmentService>();
        services.AddScoped<TimelineService>();

        // Fase 4 (plan 16.8): telemetria, alertas e Central, com as portas implementadas pela Infraestrutura (T113).
        services.AddScoped<EquipmentAlertRaiser>();
        services.AddScoped<TelemetryResourceService>();
        services.AddScoped<TelemetryIngestionService>();
        services.AddScoped<AlertService>();
        services.AddScoped<OperationsSnapshotService>();
        services.AddScoped<OperationsDetailsService>();

        // Fase 5 (plan 17.9): índice de risco e explicação, com as portas implementadas pela Infraestrutura.
        services.AddScoped<RiskService>();

        // Carga inicial (plan 15.10, 15.11.4).
        services.AddScoped<BusinessParameterProvisioningService>();
        services.AddScoped<InventoryReferenceSeeder>();
        services.AddScoped<EquipmentCatalogSeeder>();
        services.AddScoped<AgentCatalogSeeder>();
        services.AddScoped<InactivationSeeder>();
        services.AddScoped<DutyRosterSeeder>();
        services.AddScoped<OperationsAlertSeeder>();
        services.AddScoped<OccurrenceHistorySeeder>();

        return services;
    }
}
