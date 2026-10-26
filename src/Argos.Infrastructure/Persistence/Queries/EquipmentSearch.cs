using Argos.Contracts.Common;
using Argos.Domain.Common;
using Argos.Domain.Inventory.Equipments;
using Argos.Domain.Inventory.Types;
using Argos.Infrastructure.Persistence.Configurations.Inventory;
using Microsoft.EntityFrameworkCore;
using C = Argos.Contracts.Inventory;

namespace Argos.Infrastructure.Persistence.Queries;

/// <summary>
/// Linhas da pesquisa de equipamentos (plan 15.8), montadas no banco, com o termo, a ordenação e a paginação comuns à
/// pesquisa do inventário (US-03) e à dos equipamentos vinculáveis (D-68): a mesma consulta e a mesma projeção para
/// <see cref="C.EquipmentSummary"/> nas duas telas (Art. V.3). Os filtros (<see cref="WhereFilters"/>) também são os
/// da Central (RN-16, D-79).
/// </summary>
internal static class EquipmentSearch
{
    /// <summary>
    /// Todos os equipamentos, ativos e inativos; quem chama aplica o filtro de ativos. Bairro e região do fixo vêm da
    /// localização; do móvel, da base (D-65). A necessidade de manutenção é o mesmo cálculo de
    /// <see cref="MaintenanceNeedCalculator"/>, em subconsultas <c>EXISTS</c> sobre as manutenções abertas, da mais grave
    /// para a menos grave (D-54).
    /// </summary>
    public static IQueryable<Row> Rows(ArgosDbContext dbContext, DateOnly today, DateOnly upcomingLimit)
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
            select new Row
            {
                Id = equipment.Id,
                TypeId = equipment.TypeId,
                TypeName = type.Name,
                TypeIcon = type.Icon,
                CrewAssignable = type.CrewAssignable,
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
                MapLatitude = equipment.BaseId == null ? equipment.Location!.Point.Latitude : operationalBase!.Location.Point.Latitude,
                MapLongitude = equipment.BaseId == null ? equipment.Location!.Point.Longitude : operationalBase!.Location.Point.Longitude,
                SearchText = EF.Property<string>(equipment, EquipmentConfiguration.SearchTextProperty),
                IsActive = equipment.IsActive,
                UpdatedAt = equipment.UpdatedAt ?? equipment.CreatedAt,
            };
    }

    /// <summary>Ativos (padrão), inativos ou todos (RF-010, US-09, RN-35).</summary>
    public static IQueryable<Row> WhereActivity(IQueryable<Row> rows, ActivityFilter activity) => activity switch
    {
        ActivityFilter.Inactive => rows.Where(row => !row.IsActive),
        ActivityFilter.All => rows,
        _ => rows.Where(row => row.IsActive),
    };

    /// <summary>
    /// Filtros da pesquisa de equipamentos (plan 15.8), os mesmos da Central (RN-16, D-79; plan 16.7): tipo, situação,
    /// necessidade de manutenção, órgão, região e bairro (do móvel, pela base, D-65). "Ou" dentro do mesmo filtro e "e"
    /// entre filtros (D-53); uma lista vazia não filtra. Não inclui o filtro de ativos (<see cref="WhereActivity"/>).
    /// </summary>
    public static IQueryable<Row> WhereFilters(
        IQueryable<Row> rows,
        IReadOnlyCollection<Guid> typeIds,
        IReadOnlyCollection<EquipmentStatus> statuses,
        IReadOnlyCollection<MaintenanceNeed> maintenanceNeeds,
        IReadOnlyCollection<Guid> organizationIds,
        IReadOnlyCollection<Guid> regionIds,
        IReadOnlyCollection<Guid> neighborhoodIds)
    {
        if (typeIds.Count > 0)
        {
            var values = typeIds.ToArray();
            rows = rows.Where(row => values.Contains(row.TypeId));
        }

        if (statuses.Count > 0)
        {
            var values = statuses.ToArray();
            rows = rows.Where(row => values.Contains(row.Status));
        }

        if (organizationIds.Count > 0)
        {
            var values = organizationIds.ToArray();
            rows = rows.Where(row => values.Contains(row.OrganizationId));
        }

        if (regionIds.Count > 0)
        {
            var values = regionIds.ToArray();
            rows = rows.Where(row => values.Contains(row.RegionId));
        }

        if (neighborhoodIds.Count > 0)
        {
            var values = neighborhoodIds.ToArray();
            rows = rows.Where(row => values.Contains(row.NeighborhoodId));
        }

        if (maintenanceNeeds.Count > 0)
        {
            var values = maintenanceNeeds.ToArray();
            rows = rows.Where(row => values.Contains(row.MaintenanceNeed));
        }

        return rows;
    }

    /// <summary>
    /// Termo sem acento e sem diferença de maiúsculas em <c>search_text</c> ou no nome do bairro (plan 15.8), com os
    /// curingas do <c>LIKE</c> escapados.
    /// </summary>
    public static IQueryable<Row> WhereTerm(IQueryable<Row> rows, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return rows;
        }

        var pattern = LikePatterns.Containing(search);
        return rows.Where(row => EF.Functions.Like(row.SearchText, pattern, LikePatterns.Escape)
            || EF.Functions.Like(row.NeighborhoodNameKey, pattern, LikePatterns.Escape));
    }

    /// <summary>Conta, ordena (ordenação já conferida pela Aplicação) e lê a página pedida.</summary>
    public static async Task<PagedResult<C.EquipmentSummary>> PageAsync(
        IQueryable<Row> rows, PageQuery query, CancellationToken cancellationToken)
    {
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

    /// <summary>
    /// Ordenação da lista fechada de <see cref="C.EquipmentSortFields"/> (já conferida pela Aplicação), estável pelo id.
    /// A situação segue a ordem do ciclo de vida (<see cref="EquipmentStatus"/>), e não a do texto gravado.
    /// </summary>
    private static IOrderedQueryable<Row> Sort(IQueryable<Row> rows, string? sort, SortDirection direction)
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

    private static C.EquipmentSummary ToSummary(Row row) => new()
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

    /// <summary>
    /// Linha da pesquisa, montada no banco; os filtros e a ordenação se compõem sobre ela. Usa inicializadores, e não
    /// construtor, para que o EF componha filtros sobre ela.
    /// </summary>
    internal sealed class Row
    {
        public Guid Id { get; init; }

        public Guid TypeId { get; init; }

        public string TypeName { get; init; } = string.Empty;

        public string TypeIcon { get; init; } = string.Empty;

        /// <summary>O tipo aceita tripulação (<see cref="EquipmentType.CrewAssignable"/>; D-62).</summary>
        public bool CrewAssignable { get; init; }

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

        /// <summary>Posição no mapa sem telemetria: a instalação do fixo ou a base do móvel (plan 16.7).</summary>
        public double MapLatitude { get; init; }

        public double MapLongitude { get; init; }

        public string SearchText { get; init; } = string.Empty;

        public bool IsActive { get; init; }

        public DateTimeOffset UpdatedAt { get; init; }
    }
}

/// <summary>Padrão do <c>LIKE</c> das pesquisas (plan 15.8): termo normalizado entre curingas, com escape.</summary>
internal static class LikePatterns
{
    private const char EscapeCharacter = '\\';

    /// <summary>Caractere de escape informado ao <c>LIKE</c>.</summary>
    public const string Escape = "\\";

    /// <summary>
    /// <c>%termo%</c> com o termo normalizado como as colunas de busca (D-53) e os curingas (<c>%</c>, <c>_</c> e a
    /// própria barra) escapados.
    /// </summary>
    public static string Containing(string term)
    {
        var escaped = TextNormalization.Normalize(term)
            .Replace(EscapeCharacter.ToString(), $"{EscapeCharacter}{EscapeCharacter}", StringComparison.Ordinal)
            .Replace("%", $"{EscapeCharacter}%", StringComparison.Ordinal)
            .Replace("_", $"{EscapeCharacter}_", StringComparison.Ordinal);
        return $"%{escaped}%";
    }
}
