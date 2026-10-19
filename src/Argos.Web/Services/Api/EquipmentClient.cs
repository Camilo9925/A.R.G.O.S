using Argos.Contracts.Common;
using Argos.Contracts.Inventory;

namespace Argos.Web.Services.Api;

/// <summary>Equipamentos e manutenções (plan 15.14). As políticas são conferidas pela API (D-58).</summary>
public sealed class EquipmentClient(HttpClient http) : ApiClient(http)
{
    private const string Base = "api/inventory/equipment";

    public Task<PagedResult<EquipmentSummary>> SearchAsync(EquipmentSearchQuery query, CancellationToken cancellationToken = default)
    {
        var queryString = new QueryString()
            .Add("search", query.Search)
            .AddEach("typeId", query.TypeId)
            .AddEach("status", query.Status)
            .AddEach("organizationId", query.OrganizationId)
            .AddEach("regionId", query.RegionId)
            .AddEach("neighborhoodId", query.NeighborhoodId)
            .AddEach("maintenanceNeed", query.MaintenanceNeed)
            .AddEach("category", query.Category)
            .Add("page", query.Page)
            .Add("pageSize", query.PageSize)
            .Add("sort", query.Sort)
            .Add("direction", query.Direction.ToString());

        return GetAsync<PagedResult<EquipmentSummary>>($"{Base}{queryString}", cancellationToken);
    }

    public Task<EquipmentDetails> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<EquipmentDetails>($"{Base}/{id}", cancellationToken);

    /// <summary>Situações aceitas no cadastro (D-66), que o formulário lista sem repetir a regra (D-59).</summary>
    public Task<IReadOnlyList<EquipmentStatus>> ListInitialStatusesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<EquipmentStatus>>($"{Base}/initial-statuses", cancellationToken);

    public Task<EquipmentDetails> CreateAsync(CreateEquipmentRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<EquipmentDetails>(HttpMethod.Post, Base, request, cancellationToken);

    public Task<EquipmentDetails> UpdateAsync(Guid id, UpdateEquipmentRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<EquipmentDetails>(HttpMethod.Put, $"{Base}/{id}", request, cancellationToken);

    public Task<EquipmentDetails> ChangeStatusAsync(
        Guid id, ChangeEquipmentStatusRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<EquipmentDetails>(HttpMethod.Post, $"{Base}/{id}/status-changes", request, cancellationToken);

    public Task<PagedResult<StatusChangeData>> ListStatusChangesAsync(
        Guid id, int page, int pageSize, CancellationToken cancellationToken = default) =>
        GetAsync<PagedResult<StatusChangeData>>(
            $"{Base}/{id}/status-changes{new QueryString().Add("page", page).Add("pageSize", pageSize)}", cancellationToken);

    public Task<IReadOnlyList<MaintenanceData>> ListMaintenancesAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<MaintenanceData>>($"{Base}/{id}/maintenances", cancellationToken);

    public Task<MaintenanceData> RegisterMaintenanceAsync(
        Guid id, RegisterMaintenanceRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<MaintenanceData>(HttpMethod.Post, $"{Base}/{id}/maintenances", request, cancellationToken);

    public Task<MaintenanceCompletionData> CompleteMaintenanceAsync(
        Guid id, Guid maintenanceId, CompleteMaintenanceRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<MaintenanceCompletionData>(
            HttpMethod.Post, $"{Base}/{id}/maintenances/{maintenanceId}/completion", request, cancellationToken);
}
