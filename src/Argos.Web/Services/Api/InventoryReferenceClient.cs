using Argos.Contracts.Inventory;

namespace Argos.Web.Services.Api;

/// <summary>
/// Município, CEP, tipos e cadastros de apoio do Inventário (plan 15.14). As listas são de referência para qualquer
/// usuário autenticado; a escrita e o CEP exigem <c>inventory.manage</c>, conferido pela API.
/// </summary>
public sealed class InventoryReferenceClient(HttpClient http) : ApiClient(http)
{
    private const string Base = "api/inventory";

    public Task<MunicipalityData> GetMunicipalityAsync(CancellationToken cancellationToken = default) =>
        GetAsync<MunicipalityData>($"{Base}/municipality", cancellationToken);

    public Task<ZipCodeSuggestion> SuggestAddressAsync(string zipCode, CancellationToken cancellationToken = default) =>
        GetAsync<ZipCodeSuggestion>($"{Base}/zip-codes/{Uri.EscapeDataString(zipCode.Trim())}", cancellationToken);

    public Task<IReadOnlyList<EquipmentTypeData>> ListEquipmentTypesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<EquipmentTypeData>>($"{Base}/equipment-types", cancellationToken);

    public Task<IReadOnlyList<RegionData>> ListRegionsAsync(bool includeInactive = false, CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<RegionData>>(
            $"{Base}/regions{new QueryString().Add("includeInactive", includeInactive)}", cancellationToken);

    public Task<RegionData> CreateRegionAsync(RegionRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<RegionData>(HttpMethod.Post, $"{Base}/regions", request, cancellationToken);

    public Task<RegionData> UpdateRegionAsync(Guid id, RegionRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<RegionData>(HttpMethod.Put, $"{Base}/regions/{id}", request, cancellationToken);

    public Task InactivateRegionAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"{Base}/regions/{id}/inactivation", null, cancellationToken);

    public Task<IReadOnlyList<NeighborhoodData>> ListNeighborhoodsAsync(
        bool includeInactive = false, CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<NeighborhoodData>>(
            $"{Base}/neighborhoods{new QueryString().Add("includeInactive", includeInactive)}", cancellationToken);

    public Task<NeighborhoodData> CreateNeighborhoodAsync(NeighborhoodRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<NeighborhoodData>(HttpMethod.Post, $"{Base}/neighborhoods", request, cancellationToken);

    public Task<NeighborhoodData> UpdateNeighborhoodAsync(
        Guid id, NeighborhoodRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<NeighborhoodData>(HttpMethod.Put, $"{Base}/neighborhoods/{id}", request, cancellationToken);

    public Task InactivateNeighborhoodAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"{Base}/neighborhoods/{id}/inactivation", null, cancellationToken);

    public Task<IReadOnlyList<OrganizationData>> ListOrganizationsAsync(
        bool includeInactive = false, CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<OrganizationData>>(
            $"{Base}/organizations{new QueryString().Add("includeInactive", includeInactive)}", cancellationToken);

    public Task<OrganizationData> CreateOrganizationAsync(OrganizationRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<OrganizationData>(HttpMethod.Post, $"{Base}/organizations", request, cancellationToken);

    public Task<OrganizationData> UpdateOrganizationAsync(
        Guid id, OrganizationRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<OrganizationData>(HttpMethod.Put, $"{Base}/organizations/{id}", request, cancellationToken);

    public Task InactivateOrganizationAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"{Base}/organizations/{id}/inactivation", null, cancellationToken);

    public Task<IReadOnlyList<OperationalBaseData>> ListOperationalBasesAsync(
        bool includeInactive = false, CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<OperationalBaseData>>(
            $"{Base}/bases{new QueryString().Add("includeInactive", includeInactive)}", cancellationToken);

    public Task<OperationalBaseData> CreateOperationalBaseAsync(
        OperationalBaseRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<OperationalBaseData>(HttpMethod.Post, $"{Base}/bases", request, cancellationToken);

    public Task<OperationalBaseData> UpdateOperationalBaseAsync(
        Guid id, OperationalBaseRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<OperationalBaseData>(HttpMethod.Put, $"{Base}/bases/{id}", request, cancellationToken);

    public Task InactivateOperationalBaseAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"{Base}/bases/{id}/inactivation", null, cancellationToken);
}
