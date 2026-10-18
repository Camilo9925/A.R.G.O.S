using Argos.Application.Inventory;
using Argos.Application.Inventory.Equipments;
using Argos.Application.Inventory.Geography;
using Argos.Application.Inventory.Support;
using Argos.Application.Inventory.Types;
using Argos.Domain.Access;
using Argos.Domain.Inventory.Geography;
using Argos.Domain.Inventory.Support;
using Argos.Domain.Inventory.Types;
using Argos.UnitTests.Application.Fakes;
using Argos.UnitTests.Domain.Inventory;

namespace Argos.UnitTests.Application.Inventory;

/// <summary>
/// Serviços do Inventário montados sobre o <see cref="FakeInventory"/>, com um cenário mínimo de Prometeia: o
/// município quadrado de <see cref="InventoryTestData"/>, a região Centro, o bairro Jardim das Musas, o órgão SMM, a
/// base Central de Mobilidade e os dez tipos do catálogo. O relógio marca <see cref="InventoryTestData.Now"/>.
/// </summary>
internal sealed class InventoryServices
{
    public InventoryServices()
    {
        Queries = new FakeInventoryQueries(Inventory);

        Municipality = InventoryTestData.NewMunicipality();
        Region = Region.Create("Centro");
        Neighborhood = Neighborhood.Create("Jardim das Musas", Region, InventoryTestData.Point(-22.20, -49.65), Municipality);
        Organization = Organization.Create("Secretaria Municipal de Mobilidade", "SMM");
        Base = OperationalBase.Create("Central de Mobilidade", Organization, NewLocation());

        Inventory.Municipalities.Add(Municipality);
        Inventory.Regions.Add(Region);
        Inventory.Neighborhoods.Add(Neighborhood);
        Inventory.Organizations.Add(Organization);
        Inventory.Bases.Add(Base);
        Inventory.Types.AddRange(EquipmentTypeCatalog.CreateAll());
    }

    public FakeInventory Inventory { get; } = new();

    public FakeInventoryQueries Queries { get; }

    public FakeCurrentActor Actor { get; } = new("admin", UserRole.Administrator);

    public FakeTimeProvider Time { get; } = new(InventoryTestData.Now);

    public FakeUnitOfWork UnitOfWork => Inventory.UnitOfWork;

    public Municipality Municipality { get; }

    public Region Region { get; }

    public Neighborhood Neighborhood { get; }

    public Organization Organization { get; }

    public OperationalBase Base { get; }

    public EquipmentType Type(string key) => Inventory.Types.Single(type => type.Key == key);

    public Location NewLocation(double latitude = -22.21, double longitude = -49.66) =>
        Location.Create(Municipality, Neighborhood, "16985-001", "Rua das Acácias", "número 181", latitude, longitude);

    public MunicipalCalendar Calendar() => new(Inventory, Time);

    public InventoryReferenceNames ReferenceNames() => new(Inventory, Inventory, Inventory, Inventory);

    public InventoryLocations Locations() => new(Calendar(), Inventory);

    public MunicipalityService MunicipalityService() => new(Queries);

    public RegionService RegionService() => new(Inventory, Inventory, Queries, Inventory, UnitOfWork, Actor, Time);

    public NeighborhoodService NeighborhoodService() =>
        new(Inventory, Inventory, Inventory, Inventory, Calendar(), ReferenceNames(), Queries, Inventory, UnitOfWork, Actor, Time);

    public OrganizationService OrganizationService() => new(Inventory, Queries, Inventory, UnitOfWork, Actor, Time);

    public OperationalBaseService OperationalBaseService() =>
        new(Inventory, Inventory, Inventory, Locations(), ReferenceNames(), Queries, Inventory, UnitOfWork, Actor, Time);

    public EquipmentTypeService EquipmentTypeService() => new(Queries);

    public EquipmentQueryService EquipmentQueryService() => new(Queries, Inventory, Inventory, Calendar(), Inventory, Actor);

    public EquipmentService EquipmentService() =>
        new(Inventory, Inventory, Inventory, Inventory, Locations(), Calendar(), ReferenceNames(), EquipmentQueryService(),
            Inventory, UnitOfWork, Actor, Time);

    public EquipmentStatusService EquipmentStatusService() =>
        new(Inventory, EquipmentQueryService(), UnitOfWork, Actor, Time);

    public MaintenanceService MaintenanceService() =>
        new(Inventory, Calendar(), EquipmentQueryService(), UnitOfWork, Actor, Time);
}
