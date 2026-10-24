using Argos.Contracts.Common;
using Argos.Contracts.Inventory;

namespace Argos.Web.Services.Api;

/// <summary>
/// Agentes (US-06, US-09; plan 15.14). As políticas são conferidas pela API (D-58); as situações permitidas vêm nos
/// detalhes (D-59). Os equipamentos vinculáveis vêm da API já filtrados (D-68): a Web não conhece tipos nem situações.
/// </summary>
public sealed class AgentClient(HttpClient http) : ApiClient(http)
{
    private const string Base = "api/inventory/agents";

    public Task<PagedResult<AgentSummary>> SearchAsync(AgentSearchQuery query, CancellationToken cancellationToken = default)
    {
        var queryString = new QueryString()
            .Add("search", query.Search)
            .AddEach("role", query.Role)
            .AddEach("organizationId", query.OrganizationId)
            .AddEach("baseId", query.BaseId)
            .AddEach("regionId", query.RegionId)
            .AddEach("status", query.Status)
            .Add("activity", query.Activity.ToString().ToLowerInvariant())
            .Add("page", query.Page)
            .Add("pageSize", query.PageSize)
            .Add("sort", query.Sort)
            .Add("direction", query.Direction.ToString());

        return GetAsync<PagedResult<AgentSummary>>($"{Base}{queryString}", cancellationToken);
    }

    public Task<AgentDetails> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<AgentDetails>($"{Base}/{id}", cancellationToken);

    public Task<AgentDetails> CreateAsync(CreateAgentRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<AgentDetails>(HttpMethod.Post, Base, request, cancellationToken);

    public Task<AgentDetails> UpdateAsync(Guid id, UpdateAgentRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<AgentDetails>(HttpMethod.Put, $"{Base}/{id}", request, cancellationToken);

    public Task<AgentDetails> ChangeStatusAsync(
        Guid id, ChangeAgentStatusRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<AgentDetails>(HttpMethod.Post, $"{Base}/{id}/status-changes", request, cancellationToken);

    /// <summary>Inativa o agente (US-09, RN-35); o <c>409</c> explica em que situações é possível. Sem exclusão (RN-23).</summary>
    public Task InactivateAsync(Guid id, InactivationRequest request, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"{Base}/{id}/inactivation", request, cancellationToken);

    /// <summary>Equipamentos que aceitam vínculo agora (US-07; D-68), como a API devolve, sem filtro na Web (D-59).</summary>
    public Task<PagedResult<EquipmentSummary>> SearchAssignableEquipmentAsync(
        AssignableEquipmentQuery query, CancellationToken cancellationToken = default)
    {
        var queryString = new QueryString()
            .Add("search", query.Search)
            .Add("page", query.Page)
            .Add("pageSize", query.PageSize)
            .Add("sort", query.Sort)
            .Add("direction", query.Direction.ToString());

        return GetAsync<PagedResult<EquipmentSummary>>($"{Base}/assignable-equipment{queryString}", cancellationToken);
    }

    /// <summary>Vincula o agente ao equipamento; um vínculo aberto com outro é encerrado pela API (US-07, D-62).</summary>
    public Task<AssignmentData> AssignAsync(Guid id, AssignEquipmentRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<AssignmentData>(HttpMethod.Post, $"{Base}/{id}/assignments", request, cancellationToken);

    public Task<AssignmentData> EndAssignmentAsync(Guid id, Guid assignmentId, CancellationToken cancellationToken = default) =>
        SendAsync<AssignmentData>(HttpMethod.Post, $"{Base}/{id}/assignments/{assignmentId}/end", null, cancellationToken);

    /// <summary>Linha do tempo do agente, do mais recente para o mais antigo (US-08, RF-018).</summary>
    public Task<PagedResult<TimelineEntry>> ListTimelineAsync(
        Guid id, int page, int pageSize, CancellationToken cancellationToken = default) =>
        GetAsync<PagedResult<TimelineEntry>>(
            $"{Base}/{id}/timeline{new QueryString().Add("page", page).Add("pageSize", pageSize)}", cancellationToken);
}
