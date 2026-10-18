using Argos.Domain.Inventory.Equipments;
using Argos.Domain.Inventory.Geography;
using Argos.Domain.Inventory.Support;
using Argos.Domain.Inventory.Types;
using static Argos.UnitTests.Domain.Inventory.InventoryTestData;

namespace Argos.UnitTests.Domain.Inventory;

/// <summary>Equipamentos de teste: um radar fixo e uma viatura móvel do catálogo inicial.</summary>
internal static class EquipmentTestData
{
    public static readonly IReadOnlyList<EquipmentType> Catalog = EquipmentTypeCatalog.CreateAll();

    public static readonly DateOnly Today = new(2026, 10, 18);

    public static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static EquipmentType TypeOf(string key) => Catalog.Single(type => type.Key == key);

    public static EquipmentType Radar => TypeOf(EquipmentTypeCatalog.TrafficRadar);

    public static EquipmentType Patrol => TypeOf(EquipmentTypeCatalog.PatrolVehicle);

    public static Dictionary<string, object?> RadarAttributes() => new()
    {
        ["modality"] = "speed",
        ["maxSpeedKmh"] = 60,
        ["monitoredLanes"] = 2,
    };

    public static Dictionary<string, object?> PatrolAttributes() => new()
    {
        ["licensePlate"] = "ABC1D23",
        ["prefix"] = "GCM-01",
        ["vehicleKind"] = "patrol-car",
    };

    public static Equipment NewRadar(
        EquipmentStatus status = EquipmentStatus.Operational,
        Organization? organization = null,
        Location? location = null) =>
        Equipment.Register(
            Radar,
            "RD-2024-0057",
            "Vigilis",
            "VX-300",
            organization ?? NewOrganization(),
            location ?? NewLocation(),
            operationalBase: null,
            RadarAttributes(),
            status,
            Now,
            Author);

    public static Equipment NewPatrol(OperationalBase? operationalBase = null, Organization? organization = null)
    {
        organization ??= NewOrganization("Guarda Civil Municipal", "GCM");
        return Equipment.Register(
            Patrol,
            "9BWZZZ377VT004251",
            "Orbis",
            "Patrulha 2.0",
            organization,
            location: null,
            operationalBase ?? NewBase(organization),
            PatrolAttributes(),
            EquipmentStatus.Operational,
            Now,
            Author);
    }

    /// <summary>Leva o equipamento à situação pedida por um caminho permitido pela RN-05.</summary>
    public static Equipment InStatus(EquipmentStatus status)
    {
        var equipment = NewRadar(status switch
        {
            EquipmentStatus.NotInstalled => EquipmentStatus.NotInstalled,
            EquipmentStatus.Malfunction => EquipmentStatus.Malfunction,
            EquipmentStatus.UnderMaintenance => EquipmentStatus.UnderMaintenance,
            _ => EquipmentStatus.Operational,
        });

        if (status is EquipmentStatus.Deactivated or EquipmentStatus.Stolen)
        {
            equipment.ChangeStatus(status, "Justificativa de teste", canReactivate: false, Now, Author);
        }

        return equipment;
    }
}
