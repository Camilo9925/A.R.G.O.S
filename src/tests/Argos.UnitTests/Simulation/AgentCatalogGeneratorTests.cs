using System.Text.Json;
using System.Text.RegularExpressions;
using Argos.Domain.Inventory.Agents;
using Argos.Domain.Inventory.Equipments;
using Argos.Domain.Inventory.Support;
using Argos.Domain.Inventory.Types;
using Argos.Simulation.Generators;
using Argos.Simulation.Geography;
using Argos.Simulation.Models;
using Argos.UnitTests.Domain.Inventory;

namespace Argos.UnitTests.Simulation;

/// <summary>
/// Agentes, vínculos e inativações de demonstração gerados (RF-026, US-06, US-07, US-09, RN-35; Art. VIII.5, IX.3;
/// D-56, D-62; plan 15.11.3): determinismo, composição e conferência contra as regras do Domínio, para que a carga da
/// Fase 3 não falhe (nenhum 409).
/// </summary>
public sealed partial class AgentCatalogGeneratorTests
{
    private const int Seed = 42;

    private static readonly DateTimeOffset Anchor = new(2026, 10, 18, 15, 0, 0, TimeSpan.Zero);
    private static readonly SimulatedInventoryCatalog Inventory = InventoryCatalogGenerator.Generate(Seed, PrometeiaGeography.Load());
    private static readonly SimulatedAgentCatalog Catalog = AgentCatalogGenerator.Generate(Seed, Inventory);

    [Fact]
    public void SameSeed_GeneratesSameAgents_ArtIX3()
    {
        Assert.Equal(Serialize(Catalog), Serialize(AgentCatalogGenerator.Generate(Seed, Inventory)));
    }

    [Fact]
    public void DifferentSeeds_GenerateDifferentAgents_ArtIX3()
    {
        var other = InventoryCatalogGenerator.Generate(Seed + 1, PrometeiaGeography.Load());
        Assert.NotEqual(Serialize(Catalog), Serialize(AgentCatalogGenerator.Generate(Seed + 1, other)));
    }

    [Fact]
    public void Composition_30TrafficAgents28Guards22Police_ByOrganization_ArtIX3_Plan15113()
    {
        Assert.Equal(80, Catalog.Agents.Count);
        Assert.Equal(
            [("GCM", SimulatedAgentRole.MunicipalGuard, 28), ("PM", SimulatedAgentRole.MilitaryPolice, 22), ("SMM", SimulatedAgentRole.TrafficAgent, 30)],
            Catalog.Agents
                .GroupBy(agent => (agent.OrganizationAcronym, agent.Role))
                .Select(group => (group.Key.OrganizationAcronym, group.Key.Role, group.Count()))
                .OrderBy(group => group.OrganizationAcronym, StringComparer.Ordinal));
        Assert.All(Catalog.Agents, agent => Assert.Contains(
            Inventory.Bases, operationalBase => operationalBase.Name == agent.BaseName
                && operationalBase.OrganizationAcronym == agent.OrganizationAcronym));
    }

    [Fact]
    public void Registrations_AreUniquePerOrganization_AndFollowTheDomainFormat_US06_ArtVIII5()
    {
        Assert.Equal(80, Catalog.Agents.Select(agent => (agent.OrganizationAcronym, agent.RegistrationNumber)).Distinct().Count());
        Assert.All(Catalog.Agents, agent =>
        {
            Assert.Matches(RegistrationPattern(), agent.RegistrationNumber);
            Assert.InRange(agent.RegistrationNumber.Length, 1, Agent.RegistrationNumberMaxLength);
            Assert.InRange(agent.FullName.Length, Agent.FullNameMinLength, Agent.FullNameMaxLength);
            Assert.InRange(agent.DisplayName.Length, Agent.DisplayNameMinLength, Agent.DisplayNameMaxLength);
        });
        Assert.Equal(80, Catalog.Agents.Select(agent => agent.FullName).Distinct().Count());
    }

    [Fact]
    public void Assignments_Are48_OnlyMotorcyclesAndPatrols_OneOpenPerAgent_InTheVehicleBase_US07_ArtIX3()
    {
        Assert.Equal(48, Catalog.Assignments.Count);
        Assert.Equal(48, Catalog.Assignments.Select(item => (item.AgentOrganizationAcronym, item.AgentRegistrationNumber)).Distinct().Count());

        var byVehicle = Catalog.Assignments
            .GroupBy(item => Inventory.Equipment.Single(equipment =>
                equipment.Brand == item.EquipmentBrand && equipment.SerialNumber == item.EquipmentSerialNumber))
            .ToArray();
        Assert.Equal(30, byVehicle.Length);
        Assert.Equal(12, byVehicle.Count(group => group.Key.TypeKey == AgentCatalogGenerator.MotorcycleType && group.Count() == 1));
        Assert.Equal(18, byVehicle.Count(group => group.Key.TypeKey == AgentCatalogGenerator.PatrolType && group.Count() == 2));

        foreach (var group in byVehicle)
        {
            Assert.All(group, item =>
            {
                var agent = AgentOf(item.AgentOrganizationAcronym, item.AgentRegistrationNumber);
                Assert.Equal(group.Key.OrganizationAcronym, agent.OrganizationAcronym);
                Assert.Equal(group.Key.BaseName, agent.BaseName);
                Assert.True(item.StartedBeforeAnchor < agent.RegisteredBeforeAnchor);
                Assert.True(item.StartedBeforeAnchor < group.Key.RegisteredBeforeAnchor);
            });
        }
    }

    [Fact]
    public void Assignments_AppliedToTheCatalogOfTheSameSeed_AreAllAcceptedByTheAssignmentPolicy_US07_D62()
    {
        var world = new DomainWorld();
        var open = new Dictionary<Guid, AgentAssignment>();

        foreach (var item in Catalog.Assignments)
        {
            var agent = world.Agent(AgentOf(item.AgentOrganizationAcronym, item.AgentRegistrationNumber));
            var simulated = Inventory.Equipment.Single(equipment =>
                equipment.Brand == item.EquipmentBrand && equipment.SerialNumber == item.EquipmentSerialNumber);
            var equipment = world.Equipment(simulated);

            var change = AssignmentPolicy.Assign(
                agent, equipment, EquipmentTestData.TypeOf(simulated.TypeKey), open.GetValueOrDefault(agent.Id),
                Anchor - item.StartedBeforeAnchor, "simulação");

            Assert.Null(change.Ended);
            open[agent.Id] = change.Started;
        }

        Assert.Equal(48, open.Count);
    }

    [Fact]
    public void EquipmentInactivations_AreThreeDeactivatedOrStolen_NoneMotorcycleOrPatrol_AcceptedByRN35()
    {
        Assert.Equal(3, Catalog.EquipmentInactivations.Count);
        var world = new DomainWorld();

        foreach (var item in Catalog.EquipmentInactivations)
        {
            var simulated = Inventory.Equipment.Single(equipment =>
                equipment.Brand == item.Brand && equipment.SerialNumber == item.SerialNumber);
            Assert.Contains(simulated.FinalStatus, new[] { SimulatedEquipmentStatus.Deactivated, SimulatedEquipmentStatus.Stolen });
            Assert.DoesNotContain(simulated.TypeKey, new[] { AgentCatalogGenerator.MotorcycleType, AgentCatalogGenerator.PatrolType });
            Assert.True(item.BeforeAnchor < simulated.StatusChange!.BeforeAnchor);

            var equipment = world.Equipment(simulated);
            var inactivation = AssignmentPolicy.InactivateEquipment(equipment, [], item.Reason, Anchor - item.BeforeAnchor, "simulação");

            Assert.False(equipment.IsActive);
            Assert.Equal(item.Reason, inactivation.Change.Reason);
        }
    }

    [Fact]
    public void AgentInactivations_AreTwoAgentsWithoutAssignment_OnLeaveBeforeInactivated_RN12_RN35()
    {
        Assert.Equal(2, Catalog.AgentInactivations.Count);
        var world = new DomainWorld();

        foreach (var item in Catalog.AgentInactivations)
        {
            Assert.DoesNotContain(Catalog.Assignments, assignment =>
                assignment.AgentOrganizationAcronym == item.OrganizationAcronym
                && assignment.AgentRegistrationNumber == item.RegistrationNumber);
            Assert.True(item.InactivatedBeforeAnchor < item.OnLeaveBeforeAnchor);

            var agent = world.Agent(AgentOf(item.OrganizationAcronym, item.RegistrationNumber));
            agent.ChangeStatus(AgentStatus.OnLeave, Anchor - item.OnLeaveBeforeAnchor, "simulação");
            AssignmentPolicy.InactivateAgent(agent, null, item.Reason, Anchor - item.InactivatedBeforeAnchor, "simulação");

            Assert.False(agent.IsActive);
        }
    }

    private static SimulatedAgent AgentOf(string acronym, string registrationNumber) =>
        Catalog.Agents.Single(agent => agent.OrganizationAcronym == acronym && agent.RegistrationNumber == registrationNumber);

    private static string Serialize(SimulatedAgentCatalog catalog) => JsonSerializer.Serialize(catalog);

    [GeneratedRegex("^[A-Za-z0-9./-]+$")]
    private static partial Regex RegistrationPattern();

    /// <summary>
    /// Órgãos, bases, agentes e equipamentos do Domínio montados a partir do que foi gerado, pelas mesmas operações da
    /// carga (cadastro e mudança de situação), para conferir as regras sem I/O.
    /// </summary>
    private sealed class DomainWorld
    {
        private readonly Dictionary<string, Organization> _organizations = [];
        private readonly Dictionary<(string, string), OperationalBase> _bases = [];
        private readonly Dictionary<string, Agent> _agents = [];
        private readonly Dictionary<string, Equipment> _equipment = [];

        public Agent Agent(SimulatedAgent simulated)
        {
            var key = $"{simulated.OrganizationAcronym}|{simulated.RegistrationNumber}";
            if (!_agents.TryGetValue(key, out var agent))
            {
                var organization = Organization(simulated.OrganizationAcronym);
                agent = Argos.Domain.Inventory.Agents.Agent.Register(
                    simulated.FullName,
                    simulated.DisplayName,
                    simulated.RegistrationNumber,
                    Enum.Parse<AgentRole>(simulated.Role.ToString()),
                    organization,
                    simulated.Rank,
                    Base(organization, simulated.BaseName),
                    Anchor - simulated.RegisteredBeforeAnchor,
                    "simulação");
                Assert.Equal(AgentStatus.OffDuty, agent.Status);
                _agents[key] = agent;
            }

            return agent;
        }

        public Equipment Equipment(SimulatedEquipment simulated)
        {
            var key = $"{simulated.Brand}|{simulated.SerialNumber}";
            if (!_equipment.TryGetValue(key, out var equipment))
            {
                var organization = Organization(simulated.OrganizationAcronym);
                equipment = Argos.Domain.Inventory.Equipments.Equipment.Register(
                    EquipmentTestData.TypeOf(simulated.TypeKey),
                    simulated.SerialNumber,
                    simulated.Brand,
                    simulated.Model,
                    organization,
                    simulated.Location is null ? null : InventoryTestData.NewLocation(),
                    simulated.BaseName is null ? null : Base(organization, simulated.BaseName),
                    simulated.Attributes.ToDictionary(entry => entry.Key, entry => (object?)entry.Value),
                    Enum.Parse<EquipmentStatus>(simulated.InitialStatus.ToString()),
                    Anchor - simulated.RegisteredBeforeAnchor,
                    "simulação");

                if (simulated.StatusChange is { } change)
                {
                    equipment.ChangeStatus(
                        Enum.Parse<EquipmentStatus>(change.Target.ToString()),
                        change.Justification,
                        canReactivate: true,
                        Anchor - change.BeforeAnchor,
                        "simulação");
                }

                _equipment[key] = equipment;
            }

            return equipment;
        }

        private Organization Organization(string acronym)
        {
            if (!_organizations.TryGetValue(acronym, out var organization))
            {
                organization = InventoryTestData.NewOrganization($"Órgão {acronym}", acronym);
                _organizations[acronym] = organization;
            }

            return organization;
        }

        private OperationalBase Base(Organization organization, string name)
        {
            if (!_bases.TryGetValue((organization.Acronym, name), out var operationalBase))
            {
                operationalBase = OperationalBase.Create(name, organization, InventoryTestData.NewLocation());
                _bases[(organization.Acronym, name)] = operationalBase;
            }

            return operationalBase;
        }
    }
}
