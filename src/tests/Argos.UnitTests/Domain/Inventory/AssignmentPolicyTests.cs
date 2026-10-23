using System.Reflection;
using Argos.Domain.Inventory;
using Argos.Domain.Inventory.Agents;
using Argos.Domain.Inventory.Equipments;
using Argos.Domain.Inventory.Types;
using static Argos.UnitTests.Domain.Inventory.AgentTestData;
using static Argos.UnitTests.Domain.Inventory.EquipmentTestData;
using static Argos.UnitTests.Domain.Inventory.InventoryTestData;

namespace Argos.UnitTests.Domain.Inventory;

/// <summary>
/// Vínculo agente–equipamento móvel (RF-016, US-07, RN-35; D-62; plan 15.2.10): um vínculo aberto por agente, troca
/// com encerramento do anterior, só tipos <c>CrewAssignable</c>, nenhum vínculo novo com equipamento Desativado ou
/// Furtado e encerramento na inativação do agente ou do equipamento.
/// </summary>
public sealed class AssignmentPolicyTests
{
    private static readonly DateTimeOffset Later = Now.AddHours(1);
    private static readonly DateTimeOffset Latest = Now.AddHours(2);

    [Theory]
    [InlineData(EquipmentTypeCatalog.PatrolVehicle)]
    [InlineData(EquipmentTypeCatalog.TrafficAgentMotorcycle)]
    public void Assign_PatrolOrMotorcycle_StartsAnOpenAssignment_US07(string typeKey)
    {
        var agent = NewAgent();
        var equipment = NewMobile(typeKey);

        var result = AssignmentPolicy.Assign(agent, equipment, TypeOf(typeKey), null, Later, "admin");

        Assert.Null(result.Ended);
        var started = result.Started;
        Assert.Equal(agent.Id, started.AgentId);
        Assert.Equal(equipment.Id, started.EquipmentId);
        Assert.Equal(Later, started.StartedAt);
        Assert.Equal("admin", started.StartedBy);
        Assert.True(started.IsOpen);
        Assert.Null(started.EndedAt);
        Assert.Null(started.EndReason);
    }

    [Fact]
    public void Assign_ToAnotherEquipment_EndsThePreviousAsReassigned_US07()
    {
        var agent = NewAgent();
        var first = PatrolIn(EquipmentStatus.Operational);
        var second = PatrolIn(EquipmentStatus.Operational);
        var previous = AssignmentPolicy.Assign(agent, first, Patrol, null, Now, Author).Started;

        var result = AssignmentPolicy.Assign(agent, second, Patrol, previous, Later, "admin");

        Assert.Same(previous, result.Ended);
        Assert.False(previous.IsOpen);
        Assert.Equal(AssignmentEndReason.Reassigned, previous.EndReason);
        Assert.Equal(Later, previous.EndedAt);
        Assert.Equal("admin", previous.EndedBy);
        Assert.Equal(Now, previous.StartedAt);
        Assert.Equal(second.Id, result.Started.EquipmentId);
        Assert.True(result.Started.IsOpen);
        Assert.Equal(Later, result.Started.StartedAt);
    }

    [Fact]
    public void Assign_ToTheSameEquipment_IsRefusedAndKeepsTheCurrent_US07()
    {
        var agent = NewAgent();
        var patrol = PatrolIn(EquipmentStatus.Operational);
        var current = AssignmentPolicy.Assign(agent, patrol, Patrol, null, Now, Author).Started;

        var exception = Assert.Throws<InventoryRuleException>(() =>
            AssignmentPolicy.Assign(agent, patrol, Patrol, current, Later, Author));

        Assert.Equal("O agente já está vinculado a esse equipamento.", exception.Message);
        Assert.True(current.IsOpen);
    }

    [Fact]
    public void Assign_SeveralAgentsToTheSamePatrol_AllOpen_US07()
    {
        var patrol = PatrolIn(EquipmentStatus.Operational);

        var first = AssignmentPolicy.Assign(NewAgent("GCM-1"), patrol, Patrol, null, Now, Author).Started;
        var second = AssignmentPolicy.Assign(NewAgent("GCM-2"), patrol, Patrol, null, Now, Author).Started;

        Assert.True(first.IsOpen);
        Assert.True(second.IsOpen);
        Assert.Equal(patrol.Id, second.EquipmentId);
    }

    [Theory]
    [InlineData(EquipmentTypeCatalog.ElectricScooter, "Patinete elétrico")]
    [InlineData(EquipmentTypeCatalog.PassengerTransportVehicle, "Veículo de transporte")]
    public void Assign_ScooterOrTaxi_IsRefused_US07(string typeKey, string typeName)
    {
        var equipment = NewMobile(typeKey);

        var exception = Assert.Throws<InventoryRuleException>(() =>
            AssignmentPolicy.Assign(NewAgent(), equipment, TypeOf(typeKey), null, Now, Author));

        Assert.Equal($"Equipamentos do tipo {typeName} não recebem vínculo de agentes.", exception.Message);
    }

    [Fact]
    public void Assign_FixedEquipment_IsRefused_US07()
    {
        var exception = Assert.Throws<InventoryRuleException>(() =>
            AssignmentPolicy.Assign(NewAgent(), NewRadar(), Radar, null, Now, Author));

        Assert.Equal("Equipamentos do tipo Radar de fiscalização não recebem vínculo de agentes.", exception.Message);
    }

    [Fact]
    public void Assign_OnlyTheTwoCrewAssignableTypesOfTheCatalog_US07_D62()
    {
        Assert.Equal(
            [EquipmentTypeCatalog.PatrolVehicle, EquipmentTypeCatalog.TrafficAgentMotorcycle],
            Catalog.Where(type => type.CrewAssignable).Select(type => type.Key).Order(StringComparer.Ordinal));
        Assert.All(
            Catalog.Where(type => type.CrewAssignable),
            type => Assert.Equal(EquipmentCategory.Mobile, type.Category));
    }

    [Fact]
    public void Assign_InactiveAgent_IsRefused_US07()
    {
        var agent = NewAgent();
        agent.Inactivate(null, Now, Author);

        var exception = Assert.Throws<InventoryRuleException>(() =>
            AssignmentPolicy.Assign(agent, PatrolIn(EquipmentStatus.Operational), Patrol, null, Later, Author));

        Assert.Equal("Agente inativo não pode ser vinculado a equipamentos.", exception.Message);
    }

    [Fact]
    public void Assign_InactiveEquipment_IsRefused_US07()
    {
        // Ainda não instalado aceitaria o vínculo; só a inativação o recusa.
        var patrol = NewMobile(EquipmentTypeCatalog.PatrolVehicle, EquipmentStatus.NotInstalled);
        patrol.Inactivate(null, Now, Author);

        var exception = Assert.Throws<InventoryRuleException>(() =>
            AssignmentPolicy.Assign(NewAgent(), patrol, Patrol, null, Later, Author));

        Assert.Equal("Equipamento inativo não recebe vínculo de agentes.", exception.Message);
    }

    [Fact]
    public void Assign_NotInstalledPatrol_IsAccepted_D62()
    {
        var patrol = NewMobile(EquipmentTypeCatalog.PatrolVehicle, EquipmentStatus.NotInstalled);

        Assert.True(AssignmentPolicy.Assign(NewAgent(), patrol, Patrol, null, Now, Author).Started.IsOpen);
    }

    [Theory]
    [InlineData(EquipmentStatus.Operational)]
    [InlineData(EquipmentStatus.Malfunction)]
    [InlineData(EquipmentStatus.UnderMaintenance)]
    public void Assign_PatrolInOtherStatuses_IsAccepted_D62(EquipmentStatus status)
    {
        Assert.True(AssignmentPolicy.Assign(NewAgent(), PatrolIn(status), Patrol, null, Now, Author).Started.IsOpen);
    }

    [Theory]
    [InlineData(EquipmentStatus.Deactivated, "Não é possível vincular agentes a um equipamento desativado.")]
    [InlineData(EquipmentStatus.Stolen, "Não é possível vincular agentes a um equipamento furtado.")]
    public void Assign_NewToDeactivatedOrStolen_IsRefusedAndExistingIsKept_US07_D62(EquipmentStatus status, string message)
    {
        var patrol = PatrolIn(EquipmentStatus.Operational);
        var existing = AssignmentPolicy.Assign(NewAgent("GCM-1"), patrol, Patrol, null, Now, Author).Started;
        patrol.ChangeStatus(status, "Justificativa de teste", canReactivate: false, Later, Author);

        var exception = Assert.Throws<InventoryRuleException>(() =>
            AssignmentPolicy.Assign(NewAgent("GCM-2"), patrol, Patrol, null, Latest, Author));

        Assert.Equal(message, exception.Message);
        Assert.True(existing.IsOpen);
        Assert.Null(existing.EndReason);
    }

    [Theory]
    [InlineData(EquipmentStatus.Deactivated)]
    [InlineData(EquipmentStatus.Stolen)]
    public void Assign_MovingToDeactivatedOrStolen_IsRefusedAndKeepsTheCurrent_US07_D62(EquipmentStatus status)
    {
        var agent = NewAgent();
        var current = AssignmentPolicy.Assign(agent, PatrolIn(EquipmentStatus.Operational), Patrol, null, Now, Author).Started;

        Assert.Throws<InventoryRuleException>(() =>
            AssignmentPolicy.Assign(agent, PatrolIn(status), Patrol, current, Later, Author));

        Assert.True(current.IsOpen);
    }

    [Fact]
    public void Assign_WithAnotherAgentsOrClosedAssignment_IsAProgrammingError()
    {
        var patrol = PatrolIn(EquipmentStatus.Operational);
        var othersAssignment = AssignmentPolicy.Assign(NewAgent("GCM-1"), patrol, Patrol, null, Now, Author).Started;
        var agentWithClosed = NewAgent("GCM-2");
        var closed = AssignmentPolicy.Assign(agentWithClosed, patrol, Patrol, null, Now, Author).Started;
        closed.End(Now, Author);

        Assert.Throws<ArgumentException>(() =>
            AssignmentPolicy.Assign(NewAgent("GCM-3"), patrol, Patrol, othersAssignment, Later, Author));
        Assert.Throws<ArgumentException>(() =>
            AssignmentPolicy.Assign(agentWithClosed, PatrolIn(EquipmentStatus.Operational), Patrol, closed, Later, Author));
        Assert.Throws<ArgumentException>(() =>
            AssignmentPolicy.Assign(NewAgent("GCM-4"), patrol, Radar, null, Later, Author));
        Assert.True(othersAssignment.IsOpen);
    }

    [Fact]
    public void End_ByAPerson_IsRecordedOnceAndNeverReopens_RF016()
    {
        var assignment = AssignmentPolicy.Assign(
            NewAgent(), PatrolIn(EquipmentStatus.Operational), Patrol, null, Now, Author).Started;

        assignment.End(Later, "admin");

        Assert.False(assignment.IsOpen);
        Assert.Equal(AssignmentEndReason.Ended, assignment.EndReason);
        Assert.Equal(Later, assignment.EndedAt);
        Assert.Equal("admin", assignment.EndedBy);

        var exception = Assert.Throws<InventoryRuleException>(() => assignment.End(Latest, "outro"));
        Assert.Equal("O vínculo já foi encerrado e não volta a abrir.", exception.Message);
        Assert.Equal(Later, assignment.EndedAt);
    }

    [Fact]
    public void InactivateAgent_EndsTheOpenAssignment_RN35()
    {
        var agent = NewAgent();
        var assignment = AssignmentPolicy.Assign(agent, PatrolIn(EquipmentStatus.Operational), Patrol, null, Now, Author).Started;

        var result = AssignmentPolicy.InactivateAgent(agent, assignment, "Desligado", Later, "admin");

        Assert.False(agent.IsActive);
        Assert.Same(assignment, result.EndedAssignment);
        Assert.Equal(AssignmentEndReason.AgentInactivated, assignment.EndReason);
        Assert.Equal(Later, assignment.EndedAt);
        Assert.Equal("admin", assignment.EndedBy);
        Assert.Equal(InventoryChangeKind.Inactivated, result.Change.Kind);
        Assert.Equal("Desligado", result.Change.Reason);
    }

    [Fact]
    public void InactivateAgent_WithoutAssignment_OnlyInactivates_RN35()
    {
        var agent = NewAgent();

        var result = AssignmentPolicy.InactivateAgent(agent, null, null, Later, "admin");

        Assert.False(agent.IsActive);
        Assert.Null(result.EndedAssignment);
    }

    [Fact]
    public void InactivateAgent_Refused_KeepsTheAssignmentOpen_RN35()
    {
        var agent = NewAgent();
        var assignment = AssignmentPolicy.Assign(agent, PatrolIn(EquipmentStatus.Operational), Patrol, null, Now, Author).Started;
        agent.ChangeStatus(AgentStatus.OnDuty, Now, Author);

        Assert.Throws<InventoryRuleException>(() => AssignmentPolicy.InactivateAgent(agent, assignment, null, Later, "admin"));

        Assert.True(agent.IsActive);
        Assert.True(assignment.IsOpen);
    }

    [Fact]
    public void InactivateEquipment_DeactivatedPatrol_EndsAllOpenAssignments_RN35_D62()
    {
        var patrol = PatrolIn(EquipmentStatus.Operational);
        var first = AssignmentPolicy.Assign(NewAgent("GCM-1"), patrol, Patrol, null, Now, Author).Started;
        var second = AssignmentPolicy.Assign(NewAgent("GCM-2"), patrol, Patrol, null, Now, Author).Started;
        patrol.ChangeStatus(EquipmentStatus.Deactivated, "Baixa da frota", canReactivate: false, Now, Author);

        var result = AssignmentPolicy.InactivateEquipment(patrol, [first, second], "Leiloada", Later, "admin");

        Assert.False(patrol.IsActive);
        Assert.Equal([first, second], result.EndedAssignments);
        Assert.All(result.EndedAssignments, assignment =>
        {
            Assert.Equal(AssignmentEndReason.EquipmentInactivated, assignment.EndReason);
            Assert.Equal(Later, assignment.EndedAt);
            Assert.Equal("admin", assignment.EndedBy);
        });
        Assert.Equal(InventoryEntityType.Equipment, result.Change.EntityType);
        Assert.Equal("Leiloada", result.Change.Reason);
    }

    [Fact]
    public void InactivateEquipment_Refused_KeepsTheAssignmentsOpen_RN35()
    {
        var patrol = PatrolIn(EquipmentStatus.Operational);
        var assignment = AssignmentPolicy.Assign(NewAgent(), patrol, Patrol, null, Now, Author).Started;

        Assert.Throws<InventoryRuleException>(() =>
            AssignmentPolicy.InactivateEquipment(patrol, [assignment], null, Later, "admin"));

        Assert.True(patrol.IsActive);
        Assert.True(assignment.IsOpen);
    }

    [Fact]
    public void InactivateEquipment_WithAnotherEquipmentsAssignment_IsAProgrammingError()
    {
        var other = AssignmentPolicy.Assign(NewAgent(), PatrolIn(EquipmentStatus.Operational), Patrol, null, Now, Author).Started;
        var patrol = PatrolIn(EquipmentStatus.Deactivated);

        Assert.Throws<ArgumentException>(() => AssignmentPolicy.InactivateEquipment(patrol, [other], null, Later, "admin"));

        Assert.True(patrol.IsActive);
        Assert.True(other.IsOpen);
    }

    // D-62 e plan 15.2.10: o agregado Equipment não conhece agentes; a coordenação fica na AssignmentPolicy.
    [Fact]
    public void Equipment_DoesNotReferenceTheAgentsNamespace_D62()
    {
        const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var agentsNamespace = typeof(Agent).Namespace;
        var equipmentTypes = typeof(Equipment).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(Equipment).Namespace);

        var referenced = equipmentTypes
            .SelectMany(type => type.GetFields(All).Select(field => field.FieldType)
                .Concat(type.GetProperties(All).Select(property => property.PropertyType))
                .Concat(type.GetMethods(All).SelectMany(method =>
                    method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType))))
            .SelectMany(type => type.IsGenericType ? type.GetGenericArguments().Append(type) : [type]);

        Assert.DoesNotContain(referenced, type => type.Namespace == agentsNamespace);
    }
}
