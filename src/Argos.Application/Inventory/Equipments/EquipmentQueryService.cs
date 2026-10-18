using Argos.Application.Common;
using Argos.Application.Common.Abstractions;
using Argos.Application.Common.Exceptions;
using Argos.Application.Inventory.Types;
using Argos.Contracts.Common;
using Argos.Contracts.Inventory;
using Argos.Domain.Common.Parameters;
using ContractMaintenanceNeed = Argos.Contracts.Inventory.MaintenanceNeed;

namespace Argos.Application.Inventory.Equipments;

/// <summary>
/// Consulta do inventário de equipamentos (US-03 a US-05, RF-010, RF-011; plan 15.8, 15.14). A pesquisa e as listas
/// vêm de <see cref="IInventoryQueries"/>; nos detalhes, o que é regra vem pronto do Domínio: necessidade de
/// manutenção (RN-10, D-54), situações permitidas ao usuário (RN-05, RN-06, D-47, D-59) e o texto das características.
/// </summary>
public sealed class EquipmentQueryService(
    IInventoryQueries queries,
    IEquipmentRepository equipments,
    IEquipmentTypeRepository types,
    MunicipalCalendar calendar,
    IBusinessParameters parameters,
    ICurrentActor actor)
{
    internal const string NotFoundMessage = "Equipamento não encontrado.";

    /// <summary>Pesquisa paginada (US-03). A ordenação é de uma lista fechada (<see cref="EquipmentSortFields"/>).</summary>
    public async Task<PagedResult<EquipmentSummary>> SearchAsync(
        EquipmentSearchQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidatePage(query.Page, query.PageSize);

        var sort = string.IsNullOrWhiteSpace(query.Sort) ? EquipmentSortFields.Default : query.Sort.Trim();
        if (!EquipmentSortFields.All.Contains(sort))
        {
            throw new ValidationFailedException(
                "sort", $"Ordenação inválida. Use uma destas: {string.Join(", ", EquipmentSortFields.All)}.");
        }

        var (today, window) = await MaintenanceCalendarAsync(cancellationToken);
        return await queries.SearchEquipmentAsync(query with { Sort = sort }, today, window, cancellationToken);
    }

    public async Task<EquipmentDetails> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var details = await queries.GetEquipmentDetailsAsync(id, cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);
        var equipment = await equipments.FindByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);
        var type = await types.FindByIdAsync(equipment.TypeId, cancellationToken)
            ?? throw new InvalidOperationException($"Tipo {equipment.TypeId} do equipamento {id} não encontrado.");
        var (today, window) = await MaintenanceCalendarAsync(cancellationToken);

        return details with
        {
            Attributes = EquipmentMapping.Describe(type, equipment.Attributes),
            MaintenanceNeed = EnumNames.Convert<ContractMaintenanceNeed>(equipment.CalculateMaintenanceNeed(today, window)),
            OpenMaintenances = equipment.Maintenances
                .Where(maintenance => maintenance.IsOpen)
                .OrderBy(maintenance => maintenance.OpenedAt)
                .Select(maintenance => EquipmentMapping.ToData(maintenance, today, window))
                .ToArray(),
            AllowedStatusChanges = EquipmentPermissions.CanChangeStatus(actor)
                ? equipment.AllowedStatusChanges(EquipmentPermissions.CanReactivate(actor)).Select(EquipmentMapping.ToData).ToArray()
                : [],
        };
    }

    /// <summary>Manutenções do equipamento, abertas e encerradas (US-05), filtradas pelo estado se informado.</summary>
    public async Task<IReadOnlyList<MaintenanceData>> ListMaintenancesAsync(
        Guid id, MaintenanceState? state, CancellationToken cancellationToken = default)
    {
        await EnsureExistsAsync(id, cancellationToken);
        var (today, window) = await MaintenanceCalendarAsync(cancellationToken);
        var maintenances = await queries.ListMaintenancesAsync(id, state, cancellationToken);

        return maintenances.Select(maintenance => EquipmentMapping.WithNeed(maintenance, today, window)).ToArray();
    }

    /// <summary>Histórico de situação, do mais recente para o mais antigo (RF-008, US-04).</summary>
    public async Task<PagedResult<StatusChangeData>> ListStatusChangesAsync(
        Guid id, PageQuery page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ValidatePage(page.Page, page.PageSize);
        await EnsureExistsAsync(id, cancellationToken);

        return await queries.ListStatusChangesAsync(id, page.Page, page.PageSize, cancellationToken);
    }

    /// <summary>Hoje no fuso do município e a janela de manutenção próxima, o parâmetro da RN-10 (RN-34).</summary>
    internal async Task<(DateOnly Today, int Window)> MaintenanceCalendarAsync(CancellationToken cancellationToken) =>
        (await calendar.TodayAsync(cancellationToken),
            await parameters.GetAsync(BusinessParameter.MaintenanceUpcomingWindowDays, cancellationToken));

    private async Task EnsureExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await queries.EquipmentExistsAsync(id, cancellationToken))
        {
            throw new NotFoundException(NotFoundMessage);
        }
    }

    private static void ValidatePage(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();

        if (page < 1)
        {
            errors["page"] = ["A página deve ser 1 ou maior."];
        }

        if (pageSize is < 1 or > PageQuery.MaxPageSize)
        {
            errors["pageSize"] = [$"O tamanho da página deve estar entre 1 e {PageQuery.MaxPageSize}."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException(errors);
        }
    }
}
