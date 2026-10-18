using Argos.Contracts.Common;
using Argos.Contracts.Inventory;

namespace Argos.Application.Inventory;

/// <summary>
/// Consultas de leitura do Inventário, com projeção direta para os contratos (plan 5.1, 15.12). A mesma consulta
/// serve a todas as telas, para que um número exibido em dois lugares seja sempre o mesmo (Art. V.3). As listas de
/// referência vêm ordenadas por nome; "inativos" só entram quando pedidos.
/// </summary>
public interface IInventoryQueries
{
    Task<MunicipalityData?> GetMunicipalityAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RegionData>> ListRegionsAsync(bool includeInactive, CancellationToken cancellationToken = default);

    Task<RegionData?> GetRegionAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NeighborhoodData>> ListNeighborhoodsAsync(
        Guid? regionId, bool includeInactive, CancellationToken cancellationToken = default);

    Task<NeighborhoodData?> GetNeighborhoodAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrganizationData>> ListOrganizationsAsync(bool includeInactive, CancellationToken cancellationToken = default);

    Task<OrganizationData?> GetOrganizationAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OperationalBaseData>> ListOperationalBasesAsync(
        Guid? organizationId, bool includeInactive, CancellationToken cancellationToken = default);

    Task<OperationalBaseData?> GetOperationalBaseAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Sugestão do CEP informado, com os 8 dígitos sem hífen (D-52), ou nulo se não estiver na tabela.</summary>
    Task<ZipCodeSuggestion?> FindZipCodeAsync(string zipCodeDigits, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EquipmentTypeData>> ListEquipmentTypesAsync(bool includeInactive, CancellationToken cancellationToken = default);

    Task<EquipmentTypeData?> GetEquipmentTypeAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pesquisa paginada (US-03; plan 15.8), com a necessidade de manutenção traduzida para o banco a partir do
    /// <paramref name="today"/> e da janela de manutenção próxima (D-54). A ordenação já chega conferida pela Aplicação
    /// e é estável (desempate por id). Até a Fase 3, só os ativos.
    /// </summary>
    Task<PagedResult<EquipmentSummary>> SearchEquipmentAsync(
        EquipmentSearchQuery query, DateOnly today, int upcomingWindowDays, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dados de cadastro do equipamento, com nomes, localização, bairro e região, versão e auditoria. A Aplicação
    /// completa o que vem do Domínio: características, necessidade de manutenção, manutenções abertas e situações
    /// permitidas (D-59). Nulo se o equipamento não existe.
    /// </summary>
    Task<EquipmentDetails?> GetEquipmentDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> EquipmentExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Manutenções do equipamento, das mais recentes para as mais antigas; <c>Need</c> fica com a Aplicação.</summary>
    Task<IReadOnlyList<MaintenanceData>> ListMaintenancesAsync(
        Guid equipmentId, MaintenanceState? state, CancellationToken cancellationToken = default);

    /// <summary>Histórico de situação do equipamento, do mais recente para o mais antigo.</summary>
    Task<PagedResult<StatusChangeData>> ListStatusChangesAsync(
        Guid equipmentId, int page, int pageSize, CancellationToken cancellationToken = default);
}
