using Argos.Application.Inventory;
using Argos.Application.Seeding.Inventory;
using Argos.Contracts.Inventory;
using Microsoft.Extensions.DependencyInjection;
using EquipmentTypeCatalog = Argos.Domain.Inventory.Types.EquipmentTypeCatalog;

namespace Argos.IntegrationTests.Infrastructure;

/// <summary>
/// Cenário mínimo do Inventário para os testes (plan 15.16): a referência pequena de <see cref="InventorySeedData"/>
/// (município quadrado, uma região, os bairros Vila Hespéria e Centro, um órgão, uma base no Centro e os dez tipos),
/// criada pelo caminho da Aplicação, a carga <c>inventory-reference-v1</c>. Monta também os pedidos de cadastro de
/// equipamento usados pelos testes, pelo contrato da API.
/// </summary>
internal sealed record InventoryScenario(
    Guid RegionId,
    Guid HesperiaId,
    Guid DowntownId,
    Guid OrganizationId,
    Guid BaseId,
    Guid CameraTypeId,
    Guid RadarTypeId,
    Guid MotorcycleTypeId,
    Guid PatrolTypeId,
    Guid ScooterTypeId,
    Guid TaxiTypeId)
{
    /// <summary>CEP da tabela de teste, da Rua das Flores na Vila Hespéria.</summary>
    public const string ZipCode = "16985-001";

    public static async Task<InventoryScenario> CreateAsync(ApiFixture fixture, CancellationToken cancellationToken)
    {
        await using (var seedScope = fixture.Factory.Services.CreateAsyncScope())
        {
            await seedScope.ServiceProvider.GetRequiredService<InventoryReferenceSeeder>().SeedAsync(
                InventorySeedData.Reference(), seed: 42, registeredAt: DateTimeOffset.UtcNow.AddDays(-1), cancellationToken);
        }

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IInventoryQueries>();
        var neighborhoods = await queries.ListNeighborhoodsAsync(regionId: null, includeInactive: false, cancellationToken);
        var types = await queries.ListEquipmentTypesAsync(includeInactive: false, cancellationToken);
        var hesperia = neighborhoods.Single(neighborhood => neighborhood.Name == InventorySeedData.Hesperia);

        return new InventoryScenario(
            hesperia.RegionId,
            hesperia.Id,
            neighborhoods.Single(neighborhood => neighborhood.Name == InventorySeedData.Downtown).Id,
            (await queries.ListOrganizationsAsync(includeInactive: false, cancellationToken)).Single().Id,
            (await queries.ListOperationalBasesAsync(organizationId: null, includeInactive: false, cancellationToken)).Single().Id,
            types.Single(type => type.Key == EquipmentTypeCatalog.SurveillanceCamera).Id,
            types.Single(type => type.Key == EquipmentTypeCatalog.TrafficRadar).Id,
            types.Single(type => type.Key == EquipmentTypeCatalog.TrafficAgentMotorcycle).Id,
            types.Single(type => type.Key == EquipmentTypeCatalog.PatrolVehicle).Id,
            types.Single(type => type.Key == EquipmentTypeCatalog.ElectricScooter).Id,
            types.Single(type => type.Key == EquipmentTypeCatalog.PassengerTransportVehicle).Id);
    }

    /// <summary>Câmera fixa na Vila Hespéria ou, com <paramref name="downtown"/>, no Centro.</summary>
    public CreateEquipmentRequest Camera(
        string serialNumber,
        EquipmentStatus status = EquipmentStatus.Operational,
        RegisterMaintenanceRequest? initialMaintenance = null,
        bool downtown = false) =>
        new()
        {
            TypeId = CameraTypeId,
            SerialNumber = serialNumber,
            Brand = "Ômega Vision",
            Model = "OV-200",
            OrganizationId = OrganizationId,
            Location = downtown ? DowntownLocation() : HesperiaLocation(),
            Attributes = CameraAttributes("full-hd-1080p"),
            Status = status,
            InitialMaintenance = initialMaintenance,
        };

    /// <summary>Radar fixo na Vila Hespéria; <paramref name="maxSpeedKmh"/> nulo omite a característica.</summary>
    public CreateEquipmentRequest Radar(string serialNumber, string modality, string? maxSpeedKmh) =>
        new()
        {
            TypeId = RadarTypeId,
            SerialNumber = serialNumber,
            Brand = "Tecnovia",
            Model = "TV-9",
            OrganizationId = OrganizationId,
            Location = HesperiaLocation(),
            Attributes = maxSpeedKmh is null
                ? new() { ["modality"] = modality, ["monitoredLanes"] = "2" }
                : new() { ["modality"] = modality, ["monitoredLanes"] = "2", ["maxSpeedKmh"] = maxSpeedKmh },
            Status = EquipmentStatus.Operational,
        };

    /// <summary>Moto de agente de trânsito, móvel, na base do Centro.</summary>
    public CreateEquipmentRequest Motorcycle(string serialNumber, RegisterMaintenanceRequest? initialMaintenance = null) =>
        new()
        {
            TypeId = MotorcycleTypeId,
            SerialNumber = serialNumber,
            Brand = "Moto Sul",
            Model = "MS 300",
            OrganizationId = OrganizationId,
            BaseId = BaseId,
            Attributes = new() { ["licensePlate"] = "ABC1D23", ["prefix"] = $"M-{serialNumber}" },
            Status = EquipmentStatus.Operational,
            InitialMaintenance = initialMaintenance,
        };

    /// <summary>Viatura, móvel e com tripulação (D-62), na base do Centro.</summary>
    public CreateEquipmentRequest Patrol(string serialNumber) =>
        new()
        {
            TypeId = PatrolTypeId,
            SerialNumber = serialNumber,
            Brand = "Rodaris",
            Model = "Patrulha 2.0",
            OrganizationId = OrganizationId,
            BaseId = BaseId,
            Attributes = new() { ["licensePlate"] = "DEF2G34", ["prefix"] = $"V-{serialNumber}", ["vehicleKind"] = "patrol-car" },
            Status = EquipmentStatus.Operational,
        };

    /// <summary>Patinete elétrico, móvel, que não aceita vínculo de agentes (D-62).</summary>
    public CreateEquipmentRequest Scooter(string serialNumber) =>
        new()
        {
            TypeId = ScooterTypeId,
            SerialNumber = serialNumber,
            Brand = "Voltis",
            Model = "V2",
            OrganizationId = OrganizationId,
            BaseId = BaseId,
            Attributes = new() { ["modality"] = "public", ["operatorCompany"] = "Secretaria Municipal de Mobilidade" },
            Status = EquipmentStatus.Operational,
        };

    /// <summary>Táxi (veículo de transporte), móvel e sem tripulação do órgão (D-62).</summary>
    public CreateEquipmentRequest Taxi(string serialNumber) =>
        new()
        {
            TypeId = TaxiTypeId,
            SerialNumber = serialNumber,
            Brand = "Rodaris",
            Model = "Sedã 1.6",
            OrganizationId = OrganizationId,
            BaseId = BaseId,
            Attributes = new()
            {
                ["modality"] = "taxi", ["licensePlate"] = "GHI3J45", ["passengerCapacity"] = "4",
                ["permitHolder"] = "Cooperativa de Táxi de Prometeia",
            },
            Status = EquipmentStatus.Operational,
        };

    /// <summary>Agente de trânsito do órgão de teste, na base do Centro (RN-25: só os dados mínimos).</summary>
    public CreateAgentRequest Agent(string registrationNumber, string displayName = "Falcão") =>
        new()
        {
            FullName = "Agente Fictício de Teste",
            DisplayName = displayName,
            RegistrationNumber = registrationNumber,
            Role = AgentRole.TrafficAgent,
            OrganizationId = OrganizationId,
            BaseId = BaseId,
        };

    public LocationInput HesperiaLocation() => Location(HesperiaId, "Rua das Flores", "número 181", -22.241, -49.641);

    public LocationInput DowntownLocation() => Location(DowntownId, "Avenida Brasil", "número 200", -22.262, -49.662);

    public static LocationInput Location(
        Guid neighborhoodId, string street, string reference, double latitude, double longitude) =>
        new()
        {
            ZipCode = ZipCode,
            Street = street,
            NeighborhoodId = neighborhoodId,
            Reference = reference,
            Latitude = latitude,
            Longitude = longitude,
        };

    public static Dictionary<string, string?> CameraAttributes(string resolution) =>
        new() { ["resolution"] = resolution, ["colorMode"] = "color", ["nightVision"] = "true", ["panTiltZoom"] = "false" };

    public static RegisterMaintenanceRequest Scheduled(DateOnly dueDate) =>
        new() { Kind = MaintenanceKind.Scheduled, DueDate = dueDate };

    public static RegisterMaintenanceRequest Periodic(DateOnly dueDate, int periodicityDays = 30) =>
        new() { Kind = MaintenanceKind.Periodic, DueDate = dueDate, PeriodicityDays = periodicityDays };
}
