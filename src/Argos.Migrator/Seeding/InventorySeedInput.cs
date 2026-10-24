using Argos.Application.Seeding.Inventory;
using Argos.Domain.Inventory.Agents;
using Argos.Domain.Inventory.Equipments;
using Argos.Simulation.Geography;
using Argos.Simulation.Models;

namespace Argos.Migrator.Seeding;

/// <summary>
/// Monta a entrada da carga do Inventário (records da Aplicação) a partir dos dados de Prometeia e do catálogo gerado
/// pela Simulação, que a Aplicação não referencia (plan 15.11.4). Só traduz: as referências entre cadastros continuam
/// pelos nomes, e as regras ficam com o Domínio.
/// </summary>
internal static class InventorySeedInput
{
    /// <summary>Versão <c>inventory-reference-v1</c>: geografia de Prometeia, órgãos e bases do catálogo.</summary>
    public static InventoryReferenceSeed Reference(PrometeiaGeography geography, SimulatedInventoryCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(geography);
        ArgumentNullException.ThrowIfNull(catalog);

        var municipality = geography.Municipality;

        return new InventoryReferenceSeed(
            new MunicipalitySeed(
                municipality.Name,
                municipality.State,
                municipality.TimeZoneId,
                municipality.Boundary.Select(Point).ToArray(),
                Point(municipality.Center)),
            geography.Regions.Select(region => new RegionSeed(region.Name, region.Boundary.Select(Point).ToArray())).ToArray(),
            geography.Neighborhoods
                .Select(neighborhood => new NeighborhoodSeed(neighborhood.Name, neighborhood.Region, Point(neighborhood.Center)))
                .ToArray(),
            geography.Streets
                .Select(street => new ZipCodeSeed(street.ZipCode, street.Name, street.Neighborhood, Point(street.ReferencePoint)))
                .ToArray(),
            catalog.Organizations.Select(organization => new OrganizationSeed(organization.Acronym, organization.Name)).ToArray(),
            catalog.Bases
                .Select(operationalBase => new OperationalBaseSeed(
                    operationalBase.Name, operationalBase.OrganizationAcronym, Location(operationalBase.Location)))
                .ToArray());
    }

    /// <summary>Versão <c>inventory-catalog-v1</c>: os equipamentos, com situações e manutenções.</summary>
    public static EquipmentCatalogSeed Catalog(SimulatedInventoryCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        return new EquipmentCatalogSeed(catalog.Equipment
            .Select(equipment => new EquipmentSeed(
                equipment.TypeKey,
                equipment.SerialNumber,
                equipment.Brand,
                equipment.Model,
                equipment.OrganizationAcronym,
                equipment.Location is { } location ? Location(location) : null,
                equipment.BaseName,
                equipment.Attributes.ToDictionary(entry => entry.Key, entry => (object?)entry.Value, StringComparer.Ordinal),
                equipment.RegisteredBeforeAnchor,
                Status(equipment.InitialStatus),
                equipment.PeriodicMaintenance is { } periodic
                    ? new PeriodicMaintenanceSeed(periodic.DueInDays, periodic.PeriodicityDays, periodic.Description)
                    : null,
                equipment.StatusChange is { } change
                    ? new StatusChangeSeed(Status(change.Target), change.BeforeAnchor, change.Justification)
                    : null))
            .ToArray());
    }

    /// <summary>Versão <c>inventory-agents-v1</c>: os agentes e os vínculos com motos e viaturas.</summary>
    public static AgentCatalogSeed Agents(SimulatedAgentCatalog agents)
    {
        ArgumentNullException.ThrowIfNull(agents);

        return new AgentCatalogSeed(
            agents.Agents
                .Select(agent => new AgentSeed(
                    agent.FullName,
                    agent.DisplayName,
                    agent.RegistrationNumber,
                    Enum.Parse<AgentRole>(agent.Role.ToString()),
                    agent.OrganizationAcronym,
                    agent.Rank,
                    agent.BaseName,
                    agent.RegisteredBeforeAnchor))
                .ToArray(),
            agents.Assignments
                .Select(assignment => new AssignmentSeed(
                    assignment.AgentOrganizationAcronym,
                    assignment.AgentRegistrationNumber,
                    assignment.EquipmentBrand,
                    assignment.EquipmentSerialNumber,
                    assignment.StartedBeforeAnchor))
                .ToArray());
    }

    /// <summary>Versão <c>inventory-inactivations-v1</c>: as inativações de demonstração (RN-35).</summary>
    public static InactivationSeed Inactivations(SimulatedAgentCatalog agents)
    {
        ArgumentNullException.ThrowIfNull(agents);

        return new InactivationSeed(
            agents.EquipmentInactivations
                .Select(item => new EquipmentInactivationSeed(item.Brand, item.SerialNumber, item.Reason, item.BeforeAnchor))
                .ToArray(),
            agents.AgentInactivations
                .Select(item => new AgentInactivationSeed(
                    item.OrganizationAcronym,
                    item.RegistrationNumber,
                    item.Reason,
                    item.OnLeaveBeforeAnchor,
                    item.InactivatedBeforeAnchor))
                .ToArray());
    }

    /// <summary>
    /// Instante de negócio do registro cadastral da referência (plan 15.11.4): um dia antes do cadastro mais antigo do
    /// catálogo, para que bairros, órgãos, bases e tipos existam antes de qualquer equipamento.
    /// </summary>
    public static DateTimeOffset ReferenceRegisteredAt(SimulatedInventoryCatalog catalog, DateTimeOffset anchor)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var earliest = catalog.Equipment.Count == 0
            ? TimeSpan.Zero
            : catalog.Equipment.Max(equipment => equipment.RegisteredBeforeAnchor);
        return anchor - earliest - TimeSpan.FromDays(1);
    }

    private static SeedPoint Point(GeoCoordinate coordinate) => new(coordinate.Latitude, coordinate.Longitude);

    private static LocationSeed Location(SimulatedLocation location) =>
        new(location.ZipCode, location.Street, location.Neighborhood, location.Reference, location.Latitude, location.Longitude);

    // Os itens têm os mesmos nomes nos dois enums (plan 15.11.3); um item sem par falha aqui, antes de gravar.
    private static EquipmentStatus Status(SimulatedEquipmentStatus status) => Enum.Parse<EquipmentStatus>(status.ToString());
}
