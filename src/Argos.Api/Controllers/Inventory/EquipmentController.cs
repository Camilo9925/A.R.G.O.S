using Argos.Application.Inventory.Equipments;
using Argos.Application.Inventory.Timeline;
using Argos.Contracts.Access;
using Argos.Contracts.Common;
using Argos.Contracts.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Argos.Api.Controllers.Inventory;

/// <summary>
/// Equipamentos (US-01 a US-04, RF-001 a RF-010; plan 15.14): pesquisa e detalhes com <c>inventory.view</c>, cadastro e
/// edição e inativação com <c>inventory.manage</c>, situação com <c>resources.status.change</c>, histórico de situação
/// e linha do tempo com <c>history.view</c> (D-58). Nenhuma ação <c>DELETE</c> (RN-23): o cadastro é inativado (US-09).
/// </summary>
[ApiController]
[Route("api/inventory/equipment")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class EquipmentController(
    EquipmentQueryService queries,
    EquipmentService equipment,
    EquipmentStatusService statuses,
    EquipmentInactivationService inactivation,
    TimelineService timeline) : ControllerBase
{
    /// <summary>
    /// Pesquisa paginada (US-03). Um filtro aceita vários valores repetindo o parâmetro
    /// (<c>status=Malfunction&amp;status=Stolen</c>): valores do mesmo filtro combinam por "ou"; filtros diferentes,
    /// por "e". <c>activity</c> separa ativos (padrão), inativos e todos (US-09, RF-010).
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Permissions.InventoryView)]
    [ProducesResponseType<PagedResult<EquipmentSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PagedResult<EquipmentSummary>>> Search(
        [FromQuery] EquipmentSearchQuery query, CancellationToken cancellationToken) =>
        Ok(await queries.SearchAsync(query, cancellationToken));

    /// <summary>
    /// Detalhes, com a necessidade de manutenção, as manutenções abertas e as situações permitidas ao usuário.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.InventoryView)]
    [ProducesResponseType<EquipmentDetails>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<EquipmentDetails>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await queries.GetDetailsAsync(id, cancellationToken));

    /// <summary>Situações aceitas no cadastro (D-66), para o formulário listar só essas (D-59).</summary>
    [HttpGet("initial-statuses")]
    [Authorize(Policy = Permissions.InventoryManage)]
    [ProducesResponseType<IReadOnlyList<EquipmentStatus>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<EquipmentStatus>> ListInitialStatuses() => Ok(EquipmentService.InitialStatuses());

    /// <summary>
    /// Cadastro (US-01, US-02). Marca e número de série repetidos: 409 com <c>existingEquipmentId</c> (RN-01).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Permissions.InventoryManage)]
    [ProducesResponseType<EquipmentDetails>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<EquipmentDetails>> Create(
        CreateEquipmentRequest request, CancellationToken cancellationToken)
    {
        var details = await equipment.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = details.Id }, details);
    }

    /// <summary>Edição com a versão lida (D-60): alteração de outra pessoa no meio é 409.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.InventoryManage)]
    [ProducesResponseType<EquipmentDetails>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<EquipmentDetails>> Update(
        Guid id, UpdateEquipmentRequest request, CancellationToken cancellationToken) =>
        Ok(await equipment.UpdateAsync(id, request, cancellationToken));

    /// <summary>
    /// Mudança de situação (US-04): justificativa obrigatória é 400 (RN-06); transição não permitida, 409 (RN-05);
    /// reativação sem <c>resources.reactivate</c>, 403 com o motivo (D-47).
    /// </summary>
    [HttpPost("{id:guid}/status-changes")]
    [Authorize(Policy = Permissions.ResourceStatusChange)]
    [ProducesResponseType<EquipmentDetails>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<EquipmentDetails>> ChangeStatus(
        Guid id, ChangeEquipmentStatusRequest request, CancellationToken cancellationToken) =>
        Ok(await statuses.ChangeAsync(id, request, cancellationToken));

    /// <summary>
    /// Histórico de situação, do mais recente para o mais antigo (RF-008), paginado por <c>page</c> e <c>pageSize</c>;
    /// a ordem é fixa, e <c>sort</c> e <c>direction</c> não se aplicam. O parâmetro não se chama <c>page</c>: com esse
    /// nome, o binding tomaria a chave <c>page</c> da query como prefixo do objeto e ignoraria <c>page</c> e
    /// <c>pageSize</c>.
    /// </summary>
    [HttpGet("{id:guid}/status-changes")]
    [Authorize(Policy = Permissions.HistoryView)]
    [ProducesResponseType<PagedResult<StatusChangeData>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PagedResult<StatusChangeData>>> ListStatusChanges(
        Guid id, [FromQuery] PageQuery paging, CancellationToken cancellationToken) =>
        Ok(await queries.ListStatusChangesAsync(id, paging, cancellationToken));

    /// <summary>
    /// Inativação (US-09): só em Desativado, Furtado ou Ainda não instalado; fora delas, ou já inativo, 409 com o
    /// motivo (RN-35). Encerra os vínculos com agentes e cancela as manutenções abertas, tudo no histórico. O corpo é
    /// opcional (<c>reason</c> até 500 caracteres, 400 acima disso). Não existe exclusão (RN-23).
    /// </summary>
    [HttpPost("{id:guid}/inactivation")]
    [Authorize(Policy = Permissions.InventoryManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Inactivate(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] InactivationRequest? request,
        CancellationToken cancellationToken)
    {
        await inactivation.InactivateAsync(id, request ?? new InactivationRequest(), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Linha do tempo (US-08, RF-011): situação, manutenções, alterações de cadastro e vínculos, do mais recente para
    /// o mais antigo, paginada por <c>page</c> e <c>pageSize</c>; a ordem é fixa. O parâmetro não se chama
    /// <c>page</c>, pelo mesmo motivo do histórico de situação.
    /// </summary>
    [HttpGet("{id:guid}/timeline")]
    [Authorize(Policy = Permissions.HistoryView)]
    [ProducesResponseType<PagedResult<TimelineEntry>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PagedResult<TimelineEntry>>> ListTimeline(
        Guid id, [FromQuery] PageQuery paging, CancellationToken cancellationToken) =>
        Ok(await timeline.ForEquipmentAsync(id, paging, cancellationToken));
}
