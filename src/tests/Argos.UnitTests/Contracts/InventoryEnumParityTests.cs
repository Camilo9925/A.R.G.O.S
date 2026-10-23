using C = Argos.Contracts.Inventory;
using D = Argos.Domain.Inventory;

namespace Argos.UnitTests.Contracts;

/// <summary>
/// D-61: cada enum de Contracts do Inventário tem exatamente os itens do enum do Domínio, com os mesmos nomes, nos dois
/// sentidos, porque a conversão entre eles é por nome. Só ficam de fora os enums que existem apenas no contrato, sem
/// conversão, listados em <see cref="ContractOnly"/>.
/// </summary>
public sealed class InventoryEnumParityTests
{
    private static readonly (Type Contract, Type Domain)[] Mirrors =
    [
        (typeof(C.EquipmentStatus), typeof(D.Equipments.EquipmentStatus)),
        (typeof(C.EquipmentCategory), typeof(D.Types.EquipmentCategory)),
        (typeof(C.StatusChangeOrigin), typeof(D.Equipments.StatusChangeOrigin)),
        (typeof(C.MaintenanceKind), typeof(D.Equipments.MaintenanceKind)),
        (typeof(C.MaintenanceState), typeof(D.Equipments.MaintenanceState)),
        (typeof(C.MaintenanceResult), typeof(D.Equipments.MaintenanceResult)),
        (typeof(C.MaintenanceOrigin), typeof(D.Equipments.MaintenanceOrigin)),
        (typeof(C.MaintenanceNeed), typeof(D.Equipments.MaintenanceNeed)),
        (typeof(C.AttributeDataType), typeof(D.Types.AttributeDataType)),
        (typeof(C.AgentRole), typeof(D.Agents.AgentRole)),
        (typeof(C.AgentStatus), typeof(D.Agents.AgentStatus)),
        (typeof(C.AssignmentEndReason), typeof(D.Agents.AssignmentEndReason)),
    ];

    /// <summary>
    /// Enums só do contrato: a linha do tempo é uma projeção das fontes do histórico, montada pela consulta e pela
    /// Aplicação, sem enum equivalente no Domínio (plan 15.6).
    /// </summary>
    private static readonly Type[] ContractOnly = [typeof(C.TimelineEntryKind)];

    public static TheoryData<Type, Type> Pairs()
    {
        var data = new TheoryData<Type, Type>();
        foreach (var (contract, domain) in Mirrors)
        {
            data.Add(contract, domain);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void ContractEnum_HasSameNamesAsDomainEnum_D61(Type contract, Type domain)
    {
        Assert.Equal(Enum.GetNames(domain).Order(), Enum.GetNames(contract).Order());
    }

    [Fact]
    public void EveryContractEnumOfTheInventory_IsCoveredByParityTest_D61()
    {
        var covered = Mirrors.Select(mirror => mirror.Contract).Concat(ContractOnly).ToHashSet();
        var contractEnums = typeof(C.EquipmentStatus).Assembly.GetTypes()
            .Where(type => type.IsEnum && type.Namespace == typeof(C.EquipmentStatus).Namespace);

        Assert.All(contractEnums, contractEnum => Assert.Contains(contractEnum, covered));
    }
}
