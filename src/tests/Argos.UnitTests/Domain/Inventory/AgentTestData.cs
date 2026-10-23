using Argos.Domain.Inventory.Agents;
using Argos.Domain.Inventory.Equipments;
using Argos.Domain.Inventory.Support;
using Argos.Domain.Inventory.Types;
using static Argos.UnitTests.Domain.Inventory.EquipmentTestData;
using static Argos.UnitTests.Domain.Inventory.InventoryTestData;

namespace Argos.UnitTests.Domain.Inventory;

/// <summary>
/// Agentes e equipamentos móveis de teste: um guarda municipal fictício (Art. VIII.5; D-51, sem nome de pessoa), a
/// viatura e a moto do catálogo (<c>CrewAssignable</c>) e os móveis que não aceitam vínculo (patinete e táxi).
/// </summary>
internal static class AgentTestData
{
    public static Agent NewAgent(
        string registrationNumber = "GCM-0042",
        Organization? organization = null,
        OperationalBase? operationalBase = null,
        AgentRole role = AgentRole.MunicipalGuard,
        string? rank = "Guarda de 2ª classe")
    {
        organization ??= NewOrganization("Guarda Civil Municipal", "GCM");
        return Agent.Register(
            "Agente Fictício Quarenta e Dois",
            "Falcão",
            registrationNumber,
            role,
            organization,
            rank,
            operationalBase ?? NewBase(organization),
            Now,
            Author);
    }

    /// <summary>Leva o agente à situação pedida; a RN-12 permite qualquer passagem.</summary>
    public static Agent AgentIn(AgentStatus status)
    {
        var agent = NewAgent();
        if (status != agent.Status)
        {
            agent.ChangeStatus(status, Now, Author);
        }

        return agent;
    }

    public static Equipment PatrolIn(EquipmentStatus status)
    {
        var patrol = NewPatrol();
        switch (status)
        {
            case EquipmentStatus.Operational:
                break;
            case EquipmentStatus.Deactivated or EquipmentStatus.Stolen or EquipmentStatus.Malfunction
                or EquipmentStatus.UnderMaintenance:
                patrol.ChangeStatus(status, "Justificativa de teste", canReactivate: false, Now, Author);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Use NewMobile para Ainda não instalado.");
        }

        return patrol;
    }

    /// <summary>Equipamento móvel do tipo pedido, com características válidas do catálogo.</summary>
    public static Equipment NewMobile(string typeKey, EquipmentStatus status = EquipmentStatus.Operational)
    {
        var organization = NewOrganization("Concessionária Fictícia de Mobilidade", "CFM");
        return Equipment.Register(
            TypeOf(typeKey),
            $"SN-{typeKey}",
            "Orbis",
            "Modelo 1",
            organization,
            location: null,
            NewBase(organization),
            MobileAttributes(typeKey),
            status,
            Now,
            Author);
    }

    private static Dictionary<string, object?> MobileAttributes(string typeKey) => typeKey switch
    {
        EquipmentTypeCatalog.PatrolVehicle => PatrolAttributes(),
        EquipmentTypeCatalog.TrafficAgentMotorcycle => new() { ["licensePlate"] = "MTO1A23", ["prefix"] = "SMM-07" },
        EquipmentTypeCatalog.ElectricScooter => new() { ["modality"] = "shared", ["operatorCompany"] = "Rodas Leves" },
        EquipmentTypeCatalog.PassengerTransportVehicle => new()
        {
            ["modality"] = "taxi",
            ["licensePlate"] = "TXI4B56",
            ["passengerCapacity"] = 4,
            ["permitHolder"] = "Cooperativa Fictícia de Táxis",
        },
        _ => throw new ArgumentOutOfRangeException(nameof(typeKey), typeKey, "Tipo móvel sem dados de teste."),
    };
}
