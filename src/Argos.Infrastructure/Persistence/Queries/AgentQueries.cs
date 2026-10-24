using Argos.Application.Inventory.Agents;
using Argos.Contracts.Common;
using Argos.Domain.Inventory.Agents;
using Argos.Domain.Inventory.Types;
using Argos.Infrastructure.Persistence.Configurations.Inventory;
using Microsoft.EntityFrameworkCore;
using C = Argos.Contracts.Inventory;

namespace Argos.Infrastructure.Persistence.Queries;

/// <summary>
/// Consultas de leitura dos agentes e dos equipamentos vinculáveis (plan 5.1, 15.8, 15.12, 15.13), sem rastreamento e
/// com projeção para os contratos. Bairro e região do agente vêm da base (D-65). A pesquisa filtra, ordena e pagina no
/// banco; o mapeamento para os contratos é feito depois da leitura.
/// </summary>
internal sealed class AgentQueries(ArgosDbContext dbContext) : IAgentQueries
{
    public async Task<PagedResult<C.AgentSummary>> SearchAgentsAsync(
        C.AgentSearchQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rows = query.Activity switch
        {
            ActivityFilter.Inactive => AgentRows().Where(row => !row.IsActive),
            ActivityFilter.All => AgentRows(),
            _ => AgentRows().Where(row => row.IsActive),
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = LikePatterns.Containing(query.Search);
            rows = rows.Where(row => EF.Functions.Like(row.SearchText, pattern, LikePatterns.Escape));
        }

        if (query.Role.Count > 0)
        {
            var roles = query.Role.Select(role => ContractMapping.Convert<AgentRole>(role)).ToArray();
            rows = rows.Where(row => roles.Contains(row.Role));
        }

        if (query.OrganizationId.Count > 0)
        {
            var organizationIds = query.OrganizationId.ToArray();
            rows = rows.Where(row => organizationIds.Contains(row.OrganizationId));
        }

        if (query.BaseId.Count > 0)
        {
            var baseIds = query.BaseId.ToArray();
            rows = rows.Where(row => baseIds.Contains(row.BaseId));
        }

        if (query.RegionId.Count > 0)
        {
            var regionIds = query.RegionId.ToArray();
            rows = rows.Where(row => regionIds.Contains(row.RegionId));
        }

        if (query.Status.Count > 0)
        {
            var statuses = query.Status.Select(status => ContractMapping.Convert<AgentStatus>(status)).ToArray();
            rows = rows.Where(row => statuses.Contains(row.Status));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        var page = await Sort(rows, query.Sort, query.Direction)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<C.AgentSummary>
        {
            Items = page.Select(ToSummary).ToArray(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<C.AgentDetails?> GetAgentDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await (
                from agent in dbContext.Agents.AsNoTracking()
                join organization in dbContext.Organizations on agent.OrganizationId equals organization.Id
                join operationalBase in dbContext.OperationalBases on agent.BaseId equals operationalBase.Id
                join neighborhood in dbContext.Neighborhoods on operationalBase.Location.NeighborhoodId equals neighborhood.Id
                join region in dbContext.Regions on neighborhood.RegionId equals region.Id
                where agent.Id == id
                select new
                {
                    Agent = agent,
                    Version = EF.Property<uint>(agent, AgentConfiguration.VersionProperty),
                    OrganizationName = organization.Name,
                    OrganizationAcronym = organization.Acronym,
                    BaseName = operationalBase.Name,
                    NeighborhoodId = neighborhood.Id,
                    NeighborhoodName = neighborhood.Name,
                    RegionId = region.Id,
                    RegionName = region.Name,
                    StatusChangedAt = dbContext.AgentStatusChanges
                        .Where(change => change.AgentId == agent.Id)
                        .Max(change => (DateTimeOffset?)change.OccurredAt),
                })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var assigned = await (
                from assignment in dbContext.AgentAssignments.AsNoTracking()
                join equipment in dbContext.Equipment on assignment.EquipmentId equals equipment.Id
                join type in dbContext.EquipmentTypes on equipment.TypeId equals type.Id
                where assignment.AgentId == id && assignment.EndedAt == null
                select new { Assignment = assignment, Equipment = equipment, TypeName = type.Name, TypeIcon = type.Icon })
            .SingleOrDefaultAsync(cancellationToken);

        var found = row.Agent;
        return new C.AgentDetails
        {
            Id = found.Id,
            FullName = found.FullName,
            DisplayName = found.DisplayName,
            RegistrationNumber = found.RegistrationNumber,
            Role = ContractMapping.Convert<C.AgentRole>(found.Role),
            Rank = found.Rank,
            OrganizationId = found.OrganizationId,
            OrganizationName = row.OrganizationName,
            OrganizationAcronym = row.OrganizationAcronym,
            BaseId = found.BaseId,
            BaseName = row.BaseName,
            NeighborhoodId = row.NeighborhoodId,
            NeighborhoodName = row.NeighborhoodName,
            RegionId = row.RegionId,
            RegionName = row.RegionName,
            Status = ContractMapping.Convert<C.AgentStatus>(found.Status),
            StatusChangedAt = row.StatusChangedAt ?? found.CreatedAt,
            AssignedEquipment = assigned is null
                ? null
                : new C.AssignedEquipmentData
                {
                    AssignmentId = assigned.Assignment.Id,
                    EquipmentId = assigned.Equipment.Id,
                    TypeName = assigned.TypeName,
                    TypeIcon = assigned.TypeIcon,
                    SerialNumber = assigned.Equipment.SerialNumber,
                    Brand = assigned.Equipment.Brand,
                    Model = assigned.Equipment.Model,
                    Status = ContractMapping.Convert<C.EquipmentStatus>(assigned.Equipment.Status),
                    StartedAt = assigned.Assignment.StartedAt,
                    StartedBy = assigned.Assignment.StartedBy,
                },
            IsActive = found.IsActive,
            Version = row.Version,
            CreatedAt = found.CreatedAt,
            CreatedBy = found.CreatedBy,
            UpdatedAt = found.UpdatedAt,
            UpdatedBy = found.UpdatedBy,
        };
    }

    public Task<bool> AgentExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Agents.AnyAsync(agent => agent.Id == id, cancellationToken);

    /// <summary>
    /// Vinculáveis (D-68): ativos, móveis, de tipo com <c>CrewAssignable</c> e fora das situações que a
    /// <see cref="AssignmentPolicy"/> recusa, lidas dela; o resto é a pesquisa de equipamentos.
    /// </summary>
    public Task<PagedResult<C.EquipmentSummary>> SearchAssignableEquipmentAsync(
        C.AssignableEquipmentQuery query, DateOnly today, int upcomingWindowDays, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var refusing = AssignmentPolicy.StatusesRefusingNewAssignment.ToArray();
        var rows = EquipmentSearch.Rows(dbContext, today, today.AddDays(upcomingWindowDays))
            .Where(row => row.IsActive
                && row.Category == EquipmentCategory.Mobile
                && row.CrewAssignable
                && !refusing.Contains(row.Status));
        rows = EquipmentSearch.WhereTerm(rows, query.Search);

        return EquipmentSearch.PageAsync(rows, query, cancellationToken);
    }

    private IQueryable<AgentRow> AgentRows() =>
        from agent in dbContext.Agents.AsNoTracking()
        join organization in dbContext.Organizations on agent.OrganizationId equals organization.Id
        join operationalBase in dbContext.OperationalBases on agent.BaseId equals operationalBase.Id
        join neighborhood in dbContext.Neighborhoods on operationalBase.Location.NeighborhoodId equals neighborhood.Id
        join region in dbContext.Regions on neighborhood.RegionId equals region.Id
        select new AgentRow
        {
            Id = agent.Id,
            FullName = agent.FullName,
            DisplayName = agent.DisplayName,
            RegistrationNumber = agent.RegistrationNumber,
            Role = agent.Role,
            Rank = agent.Rank,
            OrganizationId = agent.OrganizationId,
            OrganizationAcronym = organization.Acronym,
            BaseId = agent.BaseId,
            BaseName = operationalBase.Name,
            RegionId = region.Id,
            RegionName = region.Name,
            Status = agent.Status,
            StatusOrder = agent.Status == AgentStatus.OnDuty ? 0
                : agent.Status == AgentStatus.Responding ? 1
                : agent.Status == AgentStatus.OnBreak ? 2
                : agent.Status == AgentStatus.OffDuty ? 3
                : 4,
            SearchText = EF.Property<string>(agent, AgentConfiguration.SearchTextProperty),
            IsActive = agent.IsActive,
            UpdatedAt = agent.UpdatedAt ?? agent.CreatedAt,
        };

    /// <summary>
    /// Ordenação da lista fechada de <see cref="C.AgentSortFields"/> (já conferida pela Aplicação), estável pelo id. A
    /// situação segue a ordem do enum (<see cref="AgentStatus"/>), e não a do texto gravado.
    /// </summary>
    private static IOrderedQueryable<AgentRow> Sort(IQueryable<AgentRow> rows, string? sort, SortDirection direction)
    {
        var descending = direction == SortDirection.Desc;

        var ordered = sort switch
        {
            C.AgentSortFields.RegistrationNumber => descending
                ? rows.OrderByDescending(row => row.RegistrationNumber)
                : rows.OrderBy(row => row.RegistrationNumber),
            C.AgentSortFields.Status => descending ? rows.OrderByDescending(row => row.StatusOrder) : rows.OrderBy(row => row.StatusOrder),
            _ => descending ? rows.OrderByDescending(row => row.DisplayName) : rows.OrderBy(row => row.DisplayName),
        };

        return ordered.ThenBy(row => row.Id);
    }

    private static C.AgentSummary ToSummary(AgentRow row) => new()
    {
        Id = row.Id,
        FullName = row.FullName,
        DisplayName = row.DisplayName,
        RegistrationNumber = row.RegistrationNumber,
        Role = ContractMapping.Convert<C.AgentRole>(row.Role),
        Rank = row.Rank,
        OrganizationId = row.OrganizationId,
        OrganizationAcronym = row.OrganizationAcronym,
        BaseId = row.BaseId,
        BaseName = row.BaseName,
        RegionId = row.RegionId,
        RegionName = row.RegionName,
        Status = ContractMapping.Convert<C.AgentStatus>(row.Status),
        IsActive = row.IsActive,
        UpdatedAt = row.UpdatedAt,
    };

    /// <summary>Linha da pesquisa de agentes, montada no banco; os filtros e a ordenação se compõem sobre ela.</summary>
    private sealed class AgentRow
    {
        public Guid Id { get; init; }

        public string FullName { get; init; } = string.Empty;

        public string DisplayName { get; init; } = string.Empty;

        public string RegistrationNumber { get; init; } = string.Empty;

        public AgentRole Role { get; init; }

        public string? Rank { get; init; }

        public Guid OrganizationId { get; init; }

        public string OrganizationAcronym { get; init; } = string.Empty;

        public Guid BaseId { get; init; }

        public string BaseName { get; init; } = string.Empty;

        public Guid RegionId { get; init; }

        public string RegionName { get; init; } = string.Empty;

        public AgentStatus Status { get; init; }

        public int StatusOrder { get; init; }

        public string SearchText { get; init; } = string.Empty;

        public bool IsActive { get; init; }

        public DateTimeOffset UpdatedAt { get; init; }
    }
}
