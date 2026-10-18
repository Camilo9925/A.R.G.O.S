using Argos.Application.Inventory;
using Argos.Contracts.Common;
using Argos.Domain.Common;
using Argos.Domain.Inventory.Equipments;
using Argos.Domain.Inventory.Geography;
using Argos.Domain.Inventory.Support;
using Argos.Domain.Inventory.Types;
using Argos.Infrastructure.Persistence.Configurations.Inventory;
using Microsoft.EntityFrameworkCore;
using C = Argos.Contracts.Inventory;

namespace Argos.Infrastructure.Persistence.Queries;

/// <summary>
/// Consultas de leitura do Inventário (plan 5.1, 15.8, 15.12), sem rastreamento e com projeção para os contratos. A
/// pesquisa filtra, ordena e pagina no banco, com a necessidade de manutenção traduzida em subconsultas (D-54); o
/// mapeamento para os contratos é feito depois da leitura, sobre poucas linhas.
/// </summary>
internal sealed class InventoryQueries(ArgosDbContext dbContext) : IInventoryQueries
{
    private const char LikeEscape = '\\';

    public async Task<C.MunicipalityData?> GetMunicipalityAsync(CancellationToken cancellationToken = default)
    {
        var municipality = await dbContext.Municipalities.AsNoTracking().SingleOrDefaultAsync(cancellationToken);

        return municipality is null
            ? null
            : new C.MunicipalityData
            {
                Name = municipality.Name,
                State = municipality.State,
                TimeZoneId = municipality.TimeZoneId,
                Boundary = ContractMapping.Rings(municipality.Boundary),
                Center = ContractMapping.Point(municipality.Center),
            };
    }

    public async Task<IReadOnlyList<C.RegionData>> ListRegionsAsync(
        bool includeInactive, CancellationToken cancellationToken = default)
    {
        var regions = await dbContext.Regions
            .AsNoTracking()
            .Where(region => includeInactive || region.IsActive)
            .OrderBy(region => region.Name)
            .ThenBy(region => region.Id)
            .ToListAsync(cancellationToken);

        return regions.Select(ToData).ToArray();
    }

    public async Task<C.RegionData?> GetRegionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var region = await dbContext.Regions.AsNoTracking().SingleOrDefaultAsync(region => region.Id == id, cancellationToken);
        return region is null ? null : ToData(region);
    }

    public async Task<IReadOnlyList<C.NeighborhoodData>> ListNeighborhoodsAsync(
        Guid? regionId, bool includeInactive, CancellationToken cancellationToken = default)
    {
        var rows = await Neighborhoods()
            .Where(row => (includeInactive || row.Neighborhood.IsActive)
                && (regionId == null || row.Neighborhood.RegionId == regionId))
            .OrderBy(row => row.Neighborhood.Name)
            .ThenBy(row => row.Neighborhood.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(row => ToData(row.Neighborhood, row.RegionName)).ToArray();
    }

    public async Task<C.NeighborhoodData?> GetNeighborhoodAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await Neighborhoods().SingleOrDefaultAsync(row => row.Neighborhood.Id == id, cancellationToken);
        return row is null ? null : ToData(row.Neighborhood, row.RegionName);
    }

    public async Task<IReadOnlyList<C.OrganizationData>> ListOrganizationsAsync(
        bool includeInactive, CancellationToken cancellationToken = default)
    {
        var organizations = await dbContext.Organizations
            .AsNoTracking()
            .Where(organization => includeInactive || organization.IsActive)
            .OrderBy(organization => organization.Name)
            .ThenBy(organization => organization.Id)
            .ToListAsync(cancellationToken);

        return organizations.Select(ToData).ToArray();
    }

    public async Task<C.OrganizationData?> GetOrganizationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(organization => organization.Id == id, cancellationToken);

        return organization is null ? null : ToData(organization);
    }

    public async Task<IReadOnlyList<C.OperationalBaseData>> ListOperationalBasesAsync(
        Guid? organizationId, bool includeInactive, CancellationToken cancellationToken = default)
    {
        var rows = await OperationalBases()
            .Where(row => (includeInactive || row.Base.IsActive)
                && (organizationId == null || row.Base.OrganizationId == organizationId))
            .OrderBy(row => row.Base.Name)
            .ThenBy(row => row.Base.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(ToData).ToArray();
    }

    public async Task<C.OperationalBaseData?> GetOperationalBaseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await OperationalBases().SingleOrDefaultAsync(row => row.Base.Id == id, cancellationToken);
        return row is null ? null : ToData(row);
    }

    public async Task<C.ZipCodeSuggestion?> FindZipCodeAsync(string zipCodeDigits, CancellationToken cancellationToken = default)
    {
        if (!ZipCode.TryParse(zipCodeDigits, out var zipCode))
        {
            return null;
        }

        var row = await (
                from entry in dbContext.ZipCodes.AsNoTracking()
                join neighborhood in dbContext.Neighborhoods on entry.NeighborhoodId equals neighborhood.Id
                join region in dbContext.Regions on neighborhood.RegionId equals region.Id
                where entry.ZipCode == zipCode
                select new { Entry = entry, Neighborhood = neighborhood, Region = region })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var municipality = await dbContext.Municipalities
            .Select(municipality => new { municipality.Name, municipality.State })
            .SingleAsync(cancellationToken);

        return new C.ZipCodeSuggestion
        {
            ZipCode = row.Entry.ZipCode.Formatted,
            Street = row.Entry.Street,
            NeighborhoodId = row.Neighborhood.Id,
            NeighborhoodName = row.Neighborhood.Name,
            RegionId = row.Region.Id,
            RegionName = row.Region.Name,
            City = municipality.Name,
            State = municipality.State,
            Latitude = row.Entry.ReferencePoint.Latitude,
            Longitude = row.Entry.ReferencePoint.Longitude,
        };
    }

    public async Task<IReadOnlyList<C.EquipmentTypeData>> ListEquipmentTypesAsync(
        bool includeInactive, CancellationToken cancellationToken = default)
    {
        var types = await dbContext.EquipmentTypes
            .AsNoTracking()
            .Include(type => type.Attributes)
            .Where(type => includeInactive || type.IsActive)
            .OrderBy(type => type.Name)
            .ThenBy(type => type.Id)
            .ToListAsync(cancellationToken);

        return types.Select(ContractMapping.Type).ToArray();
    }

    public async Task<C.EquipmentTypeData?> GetEquipmentTypeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var type = await dbContext.EquipmentTypes
            .AsNoTracking()
            .Include(type => type.Attributes)
            .SingleOrDefaultAsync(type => type.Id == id, cancellationToken);

        return type is null ? null : ContractMapping.Type(type);
    }

    public async Task<PagedResult<C.EquipmentSummary>> SearchEquipmentAsync(
        C.EquipmentSearchQuery query, DateOnly today, int upcomingWindowDays, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rows = EquipmentSearchRows(today, today.AddDays(upcomingWindowDays));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{EscapeLike(TextNormalization.Normalize(query.Search))}%";
            rows = rows.Where(row => EF.Functions.Like(row.SearchText, pattern, LikeEscape.ToString())
                || EF.Functions.Like(row.NeighborhoodNameKey, pattern, LikeEscape.ToString()));
        }

        if (query.TypeId.Count > 0)
        {
            var typeIds = query.TypeId.ToArray();
            rows = rows.Where(row => typeIds.Contains(row.TypeId));
        }

        if (query.Status.Count > 0)
        {
            var statuses = query.Status.Select(status => ContractMapping.Convert<EquipmentStatus>(status)).ToArray();
            rows = rows.Where(row => statuses.Contains(row.Status));
        }

        if (query.OrganizationId.Count > 0)
        {
            var organizationIds = query.OrganizationId.ToArray();
            rows = rows.Where(row => organizationIds.Contains(row.OrganizationId));
        }

        if (query.RegionId.Count > 0)
        {
            var regionIds = query.RegionId.ToArray();
            rows = rows.Where(row => regionIds.Contains(row.RegionId));
        }

        if (query.NeighborhoodId.Count > 0)
        {
            var neighborhoodIds = query.NeighborhoodId.ToArray();
            rows = rows.Where(row => neighborhoodIds.Contains(row.NeighborhoodId));
        }

        if (query.MaintenanceNeed.Count > 0)
        {
            var needs = query.MaintenanceNeed.Select(need => ContractMapping.Convert<MaintenanceNeed>(need)).ToArray();
            rows = rows.Where(row => needs.Contains(row.MaintenanceNeed));
        }

        if (query.Category.Count > 0)
        {
            var categories = query.Category.Select(category => ContractMapping.Convert<EquipmentCategory>(category)).ToArray();
            rows = rows.Where(row => categories.Contains(row.Category));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        var page = await Sort(rows, query.Sort, query.Direction)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<C.EquipmentSummary>
        {
            Items = page.Select(ToSummary).ToArray(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<C.EquipmentDetails?> GetEquipmentDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await (
                from equipment in dbContext.Equipment.AsNoTracking()
                join type in dbContext.EquipmentTypes on equipment.TypeId equals type.Id
                join organization in dbContext.Organizations on equipment.OrganizationId equals organization.Id
                join operationalBase in dbContext.OperationalBases on equipment.BaseId equals (Guid?)operationalBase.Id into bases
                from operationalBase in bases.DefaultIfEmpty()
                join neighborhood in dbContext.Neighborhoods
                    on equipment.BaseId == null ? equipment.Location!.NeighborhoodId : operationalBase!.Location.NeighborhoodId
                    equals neighborhood.Id
                join region in dbContext.Regions on neighborhood.RegionId equals region.Id
                where equipment.Id == id
                select new
                {
                    Equipment = equipment,
                    Version = EF.Property<uint>(equipment, EquipmentConfiguration.VersionProperty),
                    TypeKey = type.Key,
                    TypeName = type.Name,
                    TypeIcon = type.Icon,
                    OrganizationName = organization.Name,
                    OrganizationAcronym = organization.Acronym,
                    BaseName = operationalBase == null ? null : operationalBase.Name,
                    Neighborhood = neighborhood,
                    Region = region,
                })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var found = row.Equipment;
        return new C.EquipmentDetails
        {
            Id = found.Id,
            TypeId = found.TypeId,
            TypeKey = row.TypeKey,
            TypeName = row.TypeName,
            TypeIcon = row.TypeIcon,
            Category = ContractMapping.Convert<C.EquipmentCategory>(found.Category),
            SerialNumber = found.SerialNumber,
            Brand = found.Brand,
            Model = found.Model,
            OrganizationId = found.OrganizationId,
            OrganizationName = row.OrganizationName,
            OrganizationAcronym = row.OrganizationAcronym,
            Location = found.Location is { } location ? ContractMapping.Location(location, row.Neighborhood, row.Region) : null,
            BaseId = found.BaseId,
            BaseName = row.BaseName,
            NeighborhoodId = row.Neighborhood.Id,
            NeighborhoodName = row.Neighborhood.Name,
            RegionId = row.Region.Id,
            RegionName = row.Region.Name,
            Status = ContractMapping.Convert<C.EquipmentStatus>(found.Status),
            StatusChangedAt = found.StatusChangedAt,
            IsActive = found.IsActive,
            Version = row.Version,
            CreatedAt = found.CreatedAt,
            CreatedBy = found.CreatedBy,
            UpdatedAt = found.UpdatedAt,
            UpdatedBy = found.UpdatedBy,
        };
    }

    public Task<bool> EquipmentExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Equipment.AnyAsync(equipment => equipment.Id == id, cancellationToken);

    public async Task<IReadOnlyList<C.MaintenanceData>> ListMaintenancesAsync(
        Guid equipmentId, C.MaintenanceState? state, CancellationToken cancellationToken = default)
    {
        var domainState = ContractMapping.ConvertNullable<MaintenanceState>(state);
        var maintenances = await dbContext.Maintenances
            .AsNoTracking()
            .Where(maintenance => maintenance.EquipmentId == equipmentId
                && (domainState == null || maintenance.State == domainState))
            .OrderByDescending(maintenance => maintenance.OpenedAt)
            .ThenBy(maintenance => maintenance.Id)
            .ToListAsync(cancellationToken);

        return maintenances.Select(ToData).ToArray();
    }

    public async Task<PagedResult<C.StatusChangeData>> ListStatusChangesAsync(
        Guid equipmentId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var changes = dbContext.EquipmentStatusChanges
            .AsNoTracking()
            .Where(change => change.EquipmentId == equipmentId);

        var totalCount = await changes.CountAsync(cancellationToken);
        var items = await changes
            .OrderByDescending(change => change.OccurredAt)
            .ThenBy(change => change.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<C.StatusChangeData>
        {
            Items = items.Select(ToData).ToArray(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    /// <summary>
    /// Base da pesquisa: só os ativos até a Fase 3. Bairro e região do fixo vêm da localização; do móvel, da base
    /// (D-65). A necessidade de manutenção é o mesmo cálculo de <see cref="MaintenanceNeedCalculator"/>, em
    /// subconsultas <c>EXISTS</c> sobre as manutenções abertas, da mais grave para a menos grave (D-54).
    /// </summary>
    private IQueryable<SearchRow> EquipmentSearchRows(DateOnly today, DateOnly upcomingLimit)
    {
        var maintenances = dbContext.Maintenances;

        return
            from equipment in dbContext.Equipment.AsNoTracking()
            join type in dbContext.EquipmentTypes on equipment.TypeId equals type.Id
            join organization in dbContext.Organizations on equipment.OrganizationId equals organization.Id
            join operationalBase in dbContext.OperationalBases on equipment.BaseId equals (Guid?)operationalBase.Id into bases
            from operationalBase in bases.DefaultIfEmpty()
            join neighborhood in dbContext.Neighborhoods
                on equipment.BaseId == null ? equipment.Location!.NeighborhoodId : operationalBase!.Location.NeighborhoodId
                equals neighborhood.Id
            join region in dbContext.Regions on neighborhood.RegionId equals region.Id
            where equipment.IsActive
            select new SearchRow
            {
                Id = equipment.Id,
                TypeId = equipment.TypeId,
                TypeName = type.Name,
                TypeIcon = type.Icon,
                Category = equipment.Category,
                SerialNumber = equipment.SerialNumber,
                Brand = equipment.Brand,
                Model = equipment.Model,
                Status = equipment.Status,
                StatusOrder = equipment.Status == EquipmentStatus.NotInstalled ? 0
                    : equipment.Status == EquipmentStatus.Operational ? 1
                    : equipment.Status == EquipmentStatus.Malfunction ? 2
                    : equipment.Status == EquipmentStatus.UnderMaintenance ? 3
                    : equipment.Status == EquipmentStatus.Deactivated ? 4
                    : 5,
                MaintenanceNeed =
                    maintenances.Any(maintenance => maintenance.EquipmentId == equipment.Id
                        && maintenance.State == MaintenanceState.Open
                        && (maintenance.Kind == MaintenanceKind.Immediate || maintenance.DueDate == null))
                        ? MaintenanceNeed.Immediate
                    : maintenances.Any(maintenance => maintenance.EquipmentId == equipment.Id
                        && maintenance.State == MaintenanceState.Open
                        && maintenance.DueDate < today)
                        ? MaintenanceNeed.Overdue
                    : maintenances.Any(maintenance => maintenance.EquipmentId == equipment.Id
                        && maintenance.State == MaintenanceState.Open
                        && maintenance.DueDate <= upcomingLimit)
                        ? MaintenanceNeed.Upcoming
                    : maintenances.Any(maintenance => maintenance.EquipmentId == equipment.Id
                        && maintenance.State == MaintenanceState.Open)
                        ? MaintenanceNeed.Scheduled
                    : MaintenanceNeed.None,
                OrganizationId = equipment.OrganizationId,
                OrganizationAcronym = organization.Acronym,
                NeighborhoodId = neighborhood.Id,
                NeighborhoodName = neighborhood.Name,
                NeighborhoodNameKey = neighborhood.NameKey,
                RegionId = region.Id,
                RegionName = region.Name,
                SearchText = EF.Property<string>(equipment, EquipmentConfiguration.SearchTextProperty),
                IsActive = equipment.IsActive,
                UpdatedAt = equipment.UpdatedAt ?? equipment.CreatedAt,
            };
    }

    /// <summary>
    /// Ordenação da lista fechada de <see cref="C.EquipmentSortFields"/> (já conferida pela Aplicação), estável pelo id.
    /// A situação segue a ordem do ciclo de vida (<see cref="EquipmentStatus"/>), e não a do texto gravado.
    /// </summary>
    private static IOrderedQueryable<SearchRow> Sort(IQueryable<SearchRow> rows, string? sort, SortDirection direction)
    {
        var descending = direction == SortDirection.Desc;

        var ordered = sort switch
        {
            C.EquipmentSortFields.Type => descending ? rows.OrderByDescending(row => row.TypeName) : rows.OrderBy(row => row.TypeName),
            C.EquipmentSortFields.Status => descending ? rows.OrderByDescending(row => row.StatusOrder) : rows.OrderBy(row => row.StatusOrder),
            C.EquipmentSortFields.Neighborhood => descending
                ? rows.OrderByDescending(row => row.NeighborhoodName)
                : rows.OrderBy(row => row.NeighborhoodName),
            C.EquipmentSortFields.UpdatedAt => descending ? rows.OrderByDescending(row => row.UpdatedAt) : rows.OrderBy(row => row.UpdatedAt),
            _ => descending ? rows.OrderByDescending(row => row.SerialNumber) : rows.OrderBy(row => row.SerialNumber),
        };

        return ordered.ThenBy(row => row.Id);
    }

    /// <summary>Escapa os curingas do <c>LIKE</c> (<c>%</c>, <c>_</c> e a própria barra) no termo pesquisado (plan 15.8).</summary>
    private static string EscapeLike(string term) =>
        term
            .Replace(LikeEscape.ToString(), $"{LikeEscape}{LikeEscape}", StringComparison.Ordinal)
            .Replace("%", $"{LikeEscape}%", StringComparison.Ordinal)
            .Replace("_", $"{LikeEscape}_", StringComparison.Ordinal);

    private IQueryable<NeighborhoodRow> Neighborhoods() =>
        from neighborhood in dbContext.Neighborhoods.AsNoTracking()
        join region in dbContext.Regions on neighborhood.RegionId equals region.Id
        select new NeighborhoodRow { Neighborhood = neighborhood, RegionName = region.Name };

    private IQueryable<OperationalBaseRow> OperationalBases() =>
        from operationalBase in dbContext.OperationalBases.AsNoTracking()
        join organization in dbContext.Organizations on operationalBase.OrganizationId equals organization.Id
        join neighborhood in dbContext.Neighborhoods on operationalBase.Location.NeighborhoodId equals neighborhood.Id
        join region in dbContext.Regions on neighborhood.RegionId equals region.Id
        select new OperationalBaseRow
        {
            Base = operationalBase,
            OrganizationAcronym = organization.Acronym,
            Neighborhood = neighborhood,
            Region = region,
        };

    private static C.RegionData ToData(Region region) => new()
    {
        Id = region.Id,
        Name = region.Name,
        Boundary = region.Boundary is { } boundary ? ContractMapping.Rings(boundary) : null,
        IsActive = region.IsActive,
    };

    private static C.NeighborhoodData ToData(Neighborhood neighborhood, string regionName) => new()
    {
        Id = neighborhood.Id,
        Name = neighborhood.Name,
        RegionId = neighborhood.RegionId,
        RegionName = regionName,
        Center = ContractMapping.Point(neighborhood.Center),
        IsActive = neighborhood.IsActive,
    };

    private static C.OrganizationData ToData(Organization organization) => new()
    {
        Id = organization.Id,
        Name = organization.Name,
        Acronym = organization.Acronym,
        IsActive = organization.IsActive,
    };

    private static C.OperationalBaseData ToData(OperationalBaseRow row) => new()
    {
        Id = row.Base.Id,
        Name = row.Base.Name,
        OrganizationId = row.Base.OrganizationId,
        OrganizationAcronym = row.OrganizationAcronym,
        Location = ContractMapping.Location(row.Base.Location, row.Neighborhood, row.Region),
        IsActive = row.Base.IsActive,
    };

    private static C.EquipmentSummary ToSummary(SearchRow row) => new()
    {
        Id = row.Id,
        TypeId = row.TypeId,
        TypeName = row.TypeName,
        TypeIcon = row.TypeIcon,
        Category = ContractMapping.Convert<C.EquipmentCategory>(row.Category),
        SerialNumber = row.SerialNumber,
        Brand = row.Brand,
        Model = row.Model,
        Status = ContractMapping.Convert<C.EquipmentStatus>(row.Status),
        MaintenanceNeed = ContractMapping.Convert<C.MaintenanceNeed>(row.MaintenanceNeed),
        OrganizationId = row.OrganizationId,
        OrganizationAcronym = row.OrganizationAcronym,
        NeighborhoodId = row.NeighborhoodId,
        NeighborhoodName = row.NeighborhoodName,
        RegionId = row.RegionId,
        RegionName = row.RegionName,
        IsActive = row.IsActive,
        UpdatedAt = row.UpdatedAt,
    };

    private static C.MaintenanceData ToData(Maintenance maintenance) => new()
    {
        Id = maintenance.Id,
        Kind = ContractMapping.Convert<C.MaintenanceKind>(maintenance.Kind),
        State = ContractMapping.Convert<C.MaintenanceState>(maintenance.State),
        Description = maintenance.Description,
        DueDate = maintenance.DueDate,
        PeriodicityDays = maintenance.PeriodicityDays,
        Origin = ContractMapping.Convert<C.MaintenanceOrigin>(maintenance.Origin),
        PreviousMaintenanceId = maintenance.PreviousMaintenanceId,
        OpenedAt = maintenance.OpenedAt,
        OpenedBy = maintenance.OpenedBy,
        CompletedAt = maintenance.CompletedAt,
        CompletedBy = maintenance.CompletedBy,
        Result = ContractMapping.ConvertNullable<C.MaintenanceResult>(maintenance.Result),
        CompletionNotes = maintenance.CompletionNotes,
        CancelledAt = maintenance.CancelledAt,
        CancelledBy = maintenance.CancelledBy,
        CancellationReason = maintenance.CancellationReason,
    };

    private static C.StatusChangeData ToData(EquipmentStatusChange change) => new()
    {
        Id = change.Id,
        FromStatus = ContractMapping.ConvertNullable<C.EquipmentStatus>(change.FromStatus),
        ToStatus = ContractMapping.Convert<C.EquipmentStatus>(change.ToStatus),
        Origin = ContractMapping.Convert<C.StatusChangeOrigin>(change.Origin),
        OccurredAt = change.OccurredAt,
        Author = change.Author,
        Justification = change.Justification,
        MaintenanceId = change.MaintenanceId,
    };

    // As linhas intermediárias usam inicializadores, e não construtor, para que o EF componha filtros sobre elas.
    private sealed class NeighborhoodRow
    {
        public Neighborhood Neighborhood { get; init; } = null!;

        public string RegionName { get; init; } = string.Empty;
    }

    private sealed class OperationalBaseRow
    {
        public OperationalBase Base { get; init; } = null!;

        public string OrganizationAcronym { get; init; } = string.Empty;

        public Neighborhood Neighborhood { get; init; } = null!;

        public Region Region { get; init; } = null!;
    }

    /// <summary>Linha da pesquisa, montada no banco; os filtros e a ordenação se compõem sobre ela.</summary>
    private sealed class SearchRow
    {
        public Guid Id { get; init; }

        public Guid TypeId { get; init; }

        public string TypeName { get; init; } = string.Empty;

        public string TypeIcon { get; init; } = string.Empty;

        public EquipmentCategory Category { get; init; }

        public string SerialNumber { get; init; } = string.Empty;

        public string Brand { get; init; } = string.Empty;

        public string Model { get; init; } = string.Empty;

        public EquipmentStatus Status { get; init; }

        public int StatusOrder { get; init; }

        public MaintenanceNeed MaintenanceNeed { get; init; }

        public Guid OrganizationId { get; init; }

        public string OrganizationAcronym { get; init; } = string.Empty;

        public Guid NeighborhoodId { get; init; }

        public string NeighborhoodName { get; init; } = string.Empty;

        public string NeighborhoodNameKey { get; init; } = string.Empty;

        public Guid RegionId { get; init; }

        public string RegionName { get; init; } = string.Empty;

        public string SearchText { get; init; } = string.Empty;

        public bool IsActive { get; init; }

        public DateTimeOffset UpdatedAt { get; init; }
    }
}
