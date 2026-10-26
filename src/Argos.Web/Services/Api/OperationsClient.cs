using Argos.Contracts.Operations;

namespace Argos.Web.Services.Api;

/// <summary>
/// Central de Operações (plan 16.10): retrato com filtros, detalhes pelo mapa e tratamento de alertas. As políticas são
/// conferidas pela API (D-58); escopo dos filtros, ordem e ações permitidas já vêm prontos (D-59).
/// </summary>
public sealed class OperationsClient(HttpClient http) : ApiClient(http)
{
    private const string Base = "api/operations";

    public Task<OperationsSnapshot> GetSnapshotAsync(OperationsSnapshotQuery query, CancellationToken cancellationToken = default)
    {
        var queryString = new QueryString()
            .AddEach("typeId", query.TypeId)
            .AddEach("status", query.Status)
            .AddEach("maintenanceNeed", query.MaintenanceNeed)
            .AddEach("agentRole", query.AgentRole)
            .AddEach("organizationId", query.OrganizationId)
            .AddEach("regionId", query.RegionId)
            .AddEach("neighborhoodId", query.NeighborhoodId);

        return GetAsync<OperationsSnapshot>($"{Base}/snapshot{queryString}", cancellationToken);
    }

    public Task<OperationsEquipmentDetails> GetEquipmentAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<OperationsEquipmentDetails>($"{Base}/equipment/{id}", cancellationToken);

    public Task<OperationsAgentDetails> GetAgentAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<OperationsAgentDetails>($"{Base}/agents/{id}", cancellationToken);

    public Task<AlertSummary> AcknowledgeAsync(Guid alertId, CancellationToken cancellationToken = default) =>
        SendAsync<AlertSummary>(HttpMethod.Post, $"{Base}/alerts/{alertId}/acknowledgement", null, cancellationToken);

    public Task<AlertSummary> ResolveAsync(Guid alertId, ResolveAlertRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<AlertSummary>(HttpMethod.Post, $"{Base}/alerts/{alertId}/resolution", request, cancellationToken);
}
