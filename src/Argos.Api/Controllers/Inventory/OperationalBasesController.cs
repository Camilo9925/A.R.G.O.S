using Argos.Application.Inventory.Support;
using Argos.Contracts.Access;
using Argos.Contracts.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Argos.Api.Controllers.Inventory;

/// <summary>
/// Bases operacionais (RF-007, RF-019; D-57; plan 15.2.3, 15.14), com órgão e localização. Leitura para qualquer
/// usuário autenticado; escrita com <c>inventory.manage</c> (D-58). Sem exclusão: só inativação (RN-23).
/// </summary>
[ApiController]
[Authorize]
[Route("api/inventory/bases")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class OperationalBasesController(OperationalBaseService bases) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OperationalBaseData>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OperationalBaseData>>> List(
        [FromQuery] Guid? organizationId, [FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        Ok(await bases.ListAsync(organizationId, includeInactive, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<OperationalBaseData>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<OperationalBaseData>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await bases.GetAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = Permissions.InventoryManage)]
    [ProducesResponseType<OperationalBaseData>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<OperationalBaseData>> Create(
        OperationalBaseRequest request, CancellationToken cancellationToken)
    {
        var operationalBase = await bases.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = operationalBase.Id }, operationalBase);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.InventoryManage)]
    [ProducesResponseType<OperationalBaseData>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<OperationalBaseData>> Update(
        Guid id, OperationalBaseRequest request, CancellationToken cancellationToken) =>
        Ok(await bases.UpdateAsync(id, request, cancellationToken));

    /// <summary>Inativa a base; recusada (409) se houver equipamento ativo nela.</summary>
    [HttpPost("{id:guid}/inactivation")]
    [Authorize(Policy = Permissions.InventoryManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Inactivate(Guid id, CancellationToken cancellationToken)
    {
        await bases.InactivateAsync(id, cancellationToken);
        return NoContent();
    }
}
