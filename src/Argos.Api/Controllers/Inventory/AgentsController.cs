using Argos.Application.Inventory.Agents;
using Argos.Application.Inventory.Timeline;
using Argos.Contracts.Access;
using Argos.Contracts.Common;
using Argos.Contracts.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Argos.Api.Controllers.Inventory;

/// <summary>
/// Agentes e vínculos com equipamentos móveis (US-06, US-07, US-09, RF-013 a RF-017; plan 15.14): pesquisa e detalhes
/// com <c>inventory.view</c>; cadastro, edição, inativação, vínculos e equipamentos vinculáveis com
/// <c>inventory.manage</c>; situação com <c>resources.status.change</c>; linha do tempo com <c>history.view</c> (D-58).
/// As rotas com id usam <c>{id:guid}</c>: o segmento literal <c>assignable-equipment</c> já tem precedência, e a
/// restrição faz um id que não é GUID responder 404. Nenhuma ação <c>DELETE</c> (RN-23): o cadastro é inativado.
/// </summary>
[ApiController]
[Route("api/inventory/agents")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class AgentsController(
    AgentQueryService queries,
    AgentService agents,
    AgentStatusService statuses,
    AssignmentService assignments,
    TimelineService timeline) : ControllerBase
{
    /// <summary>
    /// Pesquisa paginada (US-06): por nome de exibição ou matrícula, com filtros de função, órgão, base, região (a do
    /// bairro da base, D-65), situação e <c>activity</c> (ativos por padrão, inativos ou todos; US-09). Um filtro aceita
    /// vários valores repetindo o parâmetro.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Permissions.InventoryView)]
    [ProducesResponseType<PagedResult<AgentSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PagedResult<AgentSummary>>> Search(
        [FromQuery] AgentSearchQuery query, CancellationToken cancellationToken) =>
        Ok(await queries.SearchAsync(query, cancellationToken));

    /// <summary>
    /// Equipamentos que aceitam vínculo agora (D-68): ativos, de categoria móvel e numa situação que a
    /// <c>AssignmentPolicy</c> aceita (D-62). A Web lista esta resposta sem filtrar nada (D-59); <c>search</c> procura
    /// como na pesquisa de equipamentos, e a ordenação usa os campos de lá.
    /// </summary>
    [HttpGet("assignable-equipment")]
    [Authorize(Policy = Permissions.InventoryManage)]
    [ProducesResponseType<PagedResult<EquipmentSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PagedResult<EquipmentSummary>>> SearchAssignableEquipment(
        [FromQuery] AssignableEquipmentQuery query, CancellationToken cancellationToken) =>
        Ok(await queries.SearchAssignableEquipmentAsync(query, cancellationToken));

    /// <summary>
    /// Detalhes, com os equipamentos vinculados e as situações permitidas a partir da atual (RN-12, D-59).
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.InventoryView)]
    [ProducesResponseType<AgentDetails>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<AgentDetails>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await queries.GetDetailsAsync(id, cancellationToken));

    /// <summary>
    /// Cadastro (US-06), só com os campos de RF-014 (RN-25). Matrícula repetida: 409 com o motivo.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Permissions.InventoryManage)]
    [ProducesResponseType<AgentDetails>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<AgentDetails>> Create(CreateAgentRequest request, CancellationToken cancellationToken)
    {
        var details = await agents.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = details.Id }, details);
    }

    /// <summary>
    /// Edição com a versão lida (D-60): matrícula repetida, alteração de outra pessoa no meio ou cadastro inativo é 409.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.InventoryManage)]
    [ProducesResponseType<AgentDetails>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<AgentDetails>> Update(
        Guid id, UpdateAgentRequest request, CancellationToken cancellationToken) =>
        Ok(await agents.UpdateAsync(id, request, cancellationToken));

    /// <summary>
    /// Mudança de situação (RN-12), registrada com data, hora e autor: cadastro inativo ou a mesma situação é 409 com o
    /// motivo (RN-35).
    /// </summary>
    [HttpPost("{id:guid}/status-changes")]
    [Authorize(Policy = Permissions.ResourceStatusChange)]
    [ProducesResponseType<AgentDetails>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<AgentDetails>> ChangeStatus(
        Guid id, ChangeAgentStatusRequest request, CancellationToken cancellationToken) =>
        Ok(await statuses.ChangeAsync(id, request, cancellationToken));

    /// <summary>
    /// Inativação (US-09): só em Fora de serviço ou Afastado; fora delas, ou já inativo, 409 com o motivo (RN-35). Encerra os
    /// vínculos, no histórico. O corpo é opcional (<c>reason</c> até 500 caracteres, 400 acima disso).
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
        await agents.InactivateAsync(id, request ?? new InactivationRequest(), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Vínculo com um equipamento móvel (US-07): encerra o vínculo aberto do agente, se houver. Agente ou equipamento
    /// inativo, tipo que não recebe vínculo, equipamento Desativado ou Furtado, ou o mesmo vínculo já aberto, é 409
    /// com o motivo (D-62). Não há consulta de um
    /// vínculo isolado (ele aparece nos detalhes), então a resposta não traz <c>Location</c>.
    /// </summary>
    [HttpPost("{id:guid}/assignments")]
    [Authorize(Policy = Permissions.InventoryManage)]
    [ProducesResponseType<AssignmentData>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<AssignmentData>> Assign(
        Guid id, AssignEquipmentRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await assignments.AssignAsync(id, request, cancellationToken));

    /// <summary>Encerramento de um vínculo aberto (US-07); vínculo já encerrado é 409.</summary>
    [HttpPost("{id:guid}/assignments/{assignmentId:guid}/end")]
    [Authorize(Policy = Permissions.InventoryManage)]
    [ProducesResponseType<AssignmentData>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<AssignmentData>> EndAssignment(
        Guid id, Guid assignmentId, CancellationToken cancellationToken) =>
        Ok(await assignments.EndAsync(id, assignmentId, cancellationToken));

    /// <summary>
    /// Linha do tempo (US-08, RF-018): situação, alterações de cadastro e vínculos, do mais recente para o mais
    /// antigo, paginada por <c>page</c> e <c>pageSize</c>; a ordem é fixa. O parâmetro não se chama <c>page</c>: com
    /// esse nome, o binding tomaria a chave <c>page</c> da query como prefixo do objeto.
    /// </summary>
    [HttpGet("{id:guid}/timeline")]
    [Authorize(Policy = Permissions.HistoryView)]
    [ProducesResponseType<PagedResult<TimelineEntry>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PagedResult<TimelineEntry>>> ListTimeline(
        Guid id, [FromQuery] PageQuery paging, CancellationToken cancellationToken) =>
        Ok(await timeline.ForAgentAsync(id, paging, cancellationToken));
}
