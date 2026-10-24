using Argos.Application.Inventory;
using Argos.Application.Inventory.Agents;
using Argos.Contracts.Common;
using Argos.Domain.Inventory.Agents;
using Argos.Domain.Inventory.Equipments;
using Argos.Domain.Inventory.Geography;
using Argos.Domain.Inventory.Support;
using Argos.Domain.Inventory.Types;
using C = Argos.Contracts.Inventory;

namespace Argos.UnitTests.Application.Fakes;

/// <summary>
/// Consultas sobre o <see cref="FakeInventory"/>, com o mapeamento mínimo de que os testes da Aplicação precisam. A
/// pesquisa só aplica o filtro de ativos e inativos (como a consulta real) e guarda a consulta recebida para o teste
/// conferir os demais.
/// </summary>
internal sealed class FakeInventoryQueries(FakeInventory inventory) : IInventoryQueries, IAgentQueries
{
    public C.EquipmentSearchQuery? LastSearch { get; private set; }

    public C.AgentSearchQuery? LastAgentSearch { get; private set; }

    public C.AssignableEquipmentQuery? LastAssignableSearch { get; private set; }

    public (DateOnly Today, int Window)? LastSearchCalendar { get; private set; }

    public Task<C.MunicipalityData?> GetMunicipalityAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(inventory.Municipalities.Select(municipality => new C.MunicipalityData
        {
            Name = municipality.Name,
            State = municipality.State,
            TimeZoneId = municipality.TimeZoneId,
            Boundary = Rings(municipality.Boundary),
            Center = Point(municipality.Center),
        }).SingleOrDefault());

    public Task<IReadOnlyList<C.RegionData>> ListRegionsAsync(bool includeInactive, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<C.RegionData>>(
            inventory.Regions.Where(region => includeInactive || region.IsActive).Select(Region).ToArray());

    public Task<C.RegionData?> GetRegionAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(inventory.Regions.Where(region => region.Id == id).Select(Region).SingleOrDefault());

    public Task<IReadOnlyList<C.NeighborhoodData>> ListNeighborhoodsAsync(
        Guid? regionId, bool includeInactive, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<C.NeighborhoodData>>(inventory.Neighborhoods
            .Where(neighborhood => (includeInactive || neighborhood.IsActive) && (regionId is null || neighborhood.RegionId == regionId))
            .Select(Neighborhood)
            .ToArray());

    public Task<C.NeighborhoodData?> GetNeighborhoodAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(inventory.Neighborhoods.Where(neighborhood => neighborhood.Id == id).Select(Neighborhood).SingleOrDefault());

    public Task<IReadOnlyList<C.OrganizationData>> ListOrganizationsAsync(
        bool includeInactive, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<C.OrganizationData>>(
            inventory.Organizations.Where(organization => includeInactive || organization.IsActive).Select(Organization).ToArray());

    public Task<C.OrganizationData?> GetOrganizationAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(inventory.Organizations.Where(organization => organization.Id == id).Select(Organization).SingleOrDefault());

    public Task<IReadOnlyList<C.OperationalBaseData>> ListOperationalBasesAsync(
        Guid? organizationId, bool includeInactive, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<C.OperationalBaseData>>(inventory.Bases
            .Where(operationalBase => (includeInactive || operationalBase.IsActive)
                && (organizationId is null || operationalBase.OrganizationId == organizationId))
            .Select(Base)
            .ToArray());

    public Task<C.OperationalBaseData?> GetOperationalBaseAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(inventory.Bases.Where(operationalBase => operationalBase.Id == id).Select(Base).SingleOrDefault());

    public Task<C.ZipCodeSuggestion?> FindZipCodeAsync(string zipCodeDigits, CancellationToken cancellationToken = default) =>
        Task.FromResult(inventory.ZipCodes
            .Where(entry => entry.ZipCode.Value == zipCodeDigits)
            .Select(entry =>
            {
                var neighborhood = NeighborhoodOf(entry.NeighborhoodId);
                var region = RegionOf(neighborhood.RegionId);
                var municipality = inventory.Municipalities.Single();
                return new C.ZipCodeSuggestion
                {
                    ZipCode = entry.ZipCode.Formatted,
                    Street = entry.Street,
                    NeighborhoodId = neighborhood.Id,
                    NeighborhoodName = neighborhood.Name,
                    RegionId = region.Id,
                    RegionName = region.Name,
                    City = municipality.Name,
                    State = municipality.State,
                    Latitude = entry.ReferencePoint.Latitude,
                    Longitude = entry.ReferencePoint.Longitude,
                };
            })
            .SingleOrDefault());

    public Task<IReadOnlyList<C.EquipmentTypeData>> ListEquipmentTypesAsync(
        bool includeInactive, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<C.EquipmentTypeData>>(
            inventory.Types.Where(type => includeInactive || type.IsActive).Select(Type).ToArray());

    public Task<C.EquipmentTypeData?> GetEquipmentTypeAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(inventory.Types.Where(type => type.Id == id).Select(Type).SingleOrDefault());

    public Task<PagedResult<C.EquipmentSummary>> SearchEquipmentAsync(
        C.EquipmentSearchQuery query, DateOnly today, int upcomingWindowDays, CancellationToken cancellationToken = default)
    {
        LastSearch = query;
        LastSearchCalendar = (today, upcomingWindowDays);

        var items = inventory.Equipment
            .Where(equipment => Matches(query.Activity, equipment.IsActive))
            .Select(equipment => new C.EquipmentSummary
            {
                Id = equipment.Id,
                SerialNumber = equipment.SerialNumber,
                Brand = equipment.Brand,
                Model = equipment.Model,
                Status = Convert<C.EquipmentStatus>(equipment.Status),
                MaintenanceNeed = Convert<C.MaintenanceNeed>(equipment.CalculateMaintenanceNeed(today, upcomingWindowDays)),
                IsActive = equipment.IsActive,
            })
            .ToArray();

        return Task.FromResult(new PagedResult<C.EquipmentSummary>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = items.Length,
        });
    }

    public Task<C.EquipmentDetails?> GetEquipmentDetailsAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(inventory.Equipment.Where(equipment => equipment.Id == id).Select(equipment =>
        {
            var type = inventory.Types.Single(candidate => candidate.Id == equipment.TypeId);
            var organization = inventory.Organizations.Single(candidate => candidate.Id == equipment.OrganizationId);
            return new C.EquipmentDetails
            {
                Id = equipment.Id,
                TypeId = type.Id,
                TypeKey = type.Key,
                TypeName = type.Name,
                TypeIcon = type.Icon,
                Category = Convert<C.EquipmentCategory>(type.Category),
                SerialNumber = equipment.SerialNumber,
                Brand = equipment.Brand,
                Model = equipment.Model,
                OrganizationId = organization.Id,
                OrganizationName = organization.Name,
                OrganizationAcronym = organization.Acronym,
                Location = equipment.Location is { } location ? Location(location) : null,
                BaseId = equipment.BaseId,
                Status = Convert<C.EquipmentStatus>(equipment.Status),
                StatusChangedAt = equipment.StatusChangedAt,
                IsActive = equipment.IsActive,
                Version = inventory.VersionOf(equipment.Id),
            };
        }).SingleOrDefault());

    public Task<bool> EquipmentExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(inventory.Equipment.Any(equipment => equipment.Id == id));

    public Task<IReadOnlyList<C.MaintenanceData>> ListMaintenancesAsync(
        Guid equipmentId, C.MaintenanceState? state, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<C.MaintenanceData>>(inventory.Equipment
            .Single(equipment => equipment.Id == equipmentId)
            .Maintenances
            .Where(maintenance => state is null || maintenance.State.ToString() == state.ToString())
            .OrderByDescending(maintenance => maintenance.OpenedAt)
            .Select(maintenance => new C.MaintenanceData
            {
                Id = maintenance.Id,
                Kind = Convert<C.MaintenanceKind>(maintenance.Kind),
                State = Convert<C.MaintenanceState>(maintenance.State),
                DueDate = maintenance.DueDate,
                PeriodicityDays = maintenance.PeriodicityDays,
                Origin = Convert<C.MaintenanceOrigin>(maintenance.Origin),
                OpenedAt = maintenance.OpenedAt,
                OpenedBy = maintenance.OpenedBy,
            })
            .ToArray());

    public Task<PagedResult<C.StatusChangeData>> ListStatusChangesAsync(
        Guid equipmentId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var changes = inventory.Equipment.Single(equipment => equipment.Id == equipmentId).StatusChanges
            .OrderByDescending(change => change.OccurredAt)
            .ToArray();

        return Task.FromResult(new PagedResult<C.StatusChangeData>
        {
            Items = changes.Skip((page - 1) * pageSize).Take(pageSize).Select(change => new C.StatusChangeData
            {
                Id = change.Id,
                FromStatus = change.FromStatus is { } from ? Convert<C.EquipmentStatus>(from) : null,
                ToStatus = Convert<C.EquipmentStatus>(change.ToStatus),
                Origin = Convert<C.StatusChangeOrigin>(change.Origin),
                OccurredAt = change.OccurredAt,
                Author = change.Author,
                Justification = change.Justification,
                MaintenanceId = change.MaintenanceId,
            }).ToArray(),
            Page = page,
            PageSize = pageSize,
            TotalCount = changes.Length,
        });
    }

    public Task<PagedResult<C.AgentSummary>> SearchAgentsAsync(
        C.AgentSearchQuery query, CancellationToken cancellationToken = default)
    {
        LastAgentSearch = query;

        var items = inventory.Agents
            .Where(agent => Matches(query.Activity, agent.IsActive))
            .Select(agent => new C.AgentSummary
            {
                Id = agent.Id,
                FullName = agent.FullName,
                DisplayName = agent.DisplayName,
                RegistrationNumber = agent.RegistrationNumber,
                Role = Convert<C.AgentRole>(agent.Role),
                Status = Convert<C.AgentStatus>(agent.Status),
                IsActive = agent.IsActive,
            })
            .ToArray();

        return Task.FromResult(new PagedResult<C.AgentSummary>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = items.Length,
        });
    }

    public Task<C.AgentDetails?> GetAgentDetailsAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(inventory.Agents.Where(agent => agent.Id == id).Select(agent =>
        {
            var organization = inventory.Organizations.Single(candidate => candidate.Id == agent.OrganizationId);
            var operationalBase = inventory.Bases.Single(candidate => candidate.Id == agent.BaseId);
            var assignment = inventory.Assignments.SingleOrDefault(candidate => candidate.IsOpen && candidate.AgentId == agent.Id);
            return new C.AgentDetails
            {
                Id = agent.Id,
                FullName = agent.FullName,
                DisplayName = agent.DisplayName,
                RegistrationNumber = agent.RegistrationNumber,
                Role = Convert<C.AgentRole>(agent.Role),
                Rank = agent.Rank,
                OrganizationId = organization.Id,
                OrganizationName = organization.Name,
                OrganizationAcronym = organization.Acronym,
                BaseId = operationalBase.Id,
                BaseName = operationalBase.Name,
                Status = Convert<C.AgentStatus>(agent.Status),
                StatusChangedAt = agent.StatusChanges.Max(change => change.OccurredAt),
                AssignedEquipment = assignment is null ? null : new C.AssignedEquipmentData
                {
                    AssignmentId = assignment.Id,
                    EquipmentId = assignment.EquipmentId,
                    StartedAt = assignment.StartedAt,
                    StartedBy = assignment.StartedBy,
                },
                IsActive = agent.IsActive,
                Version = inventory.AgentVersionOf(agent.Id),
            };
        }).SingleOrDefault());

    public Task<bool> AgentExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(inventory.Agents.Any(agent => agent.Id == id));

    /// <summary>Aplica o critério da consulta real (D-68), com a lista de situações lida da política.</summary>
    public Task<PagedResult<C.EquipmentSummary>> SearchAssignableEquipmentAsync(
        C.AssignableEquipmentQuery query, DateOnly today, int upcomingWindowDays, CancellationToken cancellationToken = default)
    {
        LastAssignableSearch = query;
        LastSearchCalendar = (today, upcomingWindowDays);

        var items = inventory.Equipment
            .Where(equipment => equipment.IsActive
                && equipment.Category == EquipmentCategory.Mobile
                && inventory.Types.Single(type => type.Id == equipment.TypeId).CrewAssignable
                && !AssignmentPolicy.StatusesRefusingNewAssignment.Contains(equipment.Status))
            .Select(equipment => new C.EquipmentSummary
            {
                Id = equipment.Id,
                SerialNumber = equipment.SerialNumber,
                Status = Convert<C.EquipmentStatus>(equipment.Status),
                IsActive = equipment.IsActive,
            })
            .ToArray();

        return Task.FromResult(new PagedResult<C.EquipmentSummary>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = items.Length,
        });
    }

    private static bool Matches(ActivityFilter activity, bool isActive) => activity switch
    {
        ActivityFilter.Active => isActive,
        ActivityFilter.Inactive => !isActive,
        _ => true,
    };

    private static TTarget Convert<TTarget>(Enum value)
        where TTarget : struct, Enum => Enum.Parse<TTarget>(value.ToString());

    private static C.GeoPointData Point(GeoPoint point) => new(point.Latitude, point.Longitude);

    private static IReadOnlyList<IReadOnlyList<C.GeoPointData>> Rings(GeoPolygon polygon) =>
        polygon.Rings.Select(ring => (IReadOnlyList<C.GeoPointData>)ring.Select(Point).ToArray()).ToArray();

    private static C.RegionData Region(Region region) => new()
    {
        Id = region.Id,
        Name = region.Name,
        Boundary = region.Boundary is { } boundary ? Rings(boundary) : null,
        IsActive = region.IsActive,
    };

    private C.NeighborhoodData Neighborhood(Neighborhood neighborhood) => new()
    {
        Id = neighborhood.Id,
        Name = neighborhood.Name,
        RegionId = neighborhood.RegionId,
        RegionName = RegionOf(neighborhood.RegionId).Name,
        Center = Point(neighborhood.Center),
        IsActive = neighborhood.IsActive,
    };

    private static C.OrganizationData Organization(Organization organization) => new()
    {
        Id = organization.Id,
        Name = organization.Name,
        Acronym = organization.Acronym,
        IsActive = organization.IsActive,
    };

    private C.OperationalBaseData Base(OperationalBase operationalBase) => new()
    {
        Id = operationalBase.Id,
        Name = operationalBase.Name,
        OrganizationId = operationalBase.OrganizationId,
        OrganizationAcronym = inventory.Organizations.Single(organization => organization.Id == operationalBase.OrganizationId).Acronym,
        Location = Location(operationalBase.Location),
        IsActive = operationalBase.IsActive,
    };

    private C.LocationData Location(Location location)
    {
        var neighborhood = NeighborhoodOf(location.NeighborhoodId);
        var region = RegionOf(neighborhood.RegionId);
        return new C.LocationData
        {
            ZipCode = location.ZipCode.Formatted,
            Street = location.Street,
            NeighborhoodId = neighborhood.Id,
            NeighborhoodName = neighborhood.Name,
            RegionId = region.Id,
            RegionName = region.Name,
            City = location.City,
            State = location.State,
            Reference = location.Reference,
            Latitude = location.Point.Latitude,
            Longitude = location.Point.Longitude,
        };
    }

    private static C.EquipmentTypeData Type(EquipmentType type) => new()
    {
        Id = type.Id,
        Key = type.Key,
        Name = type.Name,
        Category = Convert<C.EquipmentCategory>(type.Category),
        Icon = type.Icon,
        IsBuiltIn = type.IsBuiltIn,
        CrewAssignable = type.CrewAssignable,
        IsActive = type.IsActive,
        Attributes = type.Attributes.Select(attribute => new C.EquipmentTypeAttributeData
        {
            Key = attribute.Key,
            Label = attribute.Label,
            DataType = Convert<C.AttributeDataType>(attribute.DataType),
            IsRequired = attribute.IsRequired,
            Order = attribute.Order,
        }).ToArray(),
    };

    private Neighborhood NeighborhoodOf(Guid id) => inventory.Neighborhoods.Single(neighborhood => neighborhood.Id == id);

    private Region RegionOf(Guid id) => inventory.Regions.Single(region => region.Id == id);
}
