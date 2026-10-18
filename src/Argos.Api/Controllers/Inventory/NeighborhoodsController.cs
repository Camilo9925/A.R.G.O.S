using Argos.Application.Inventory.Geography;
using Argos.Contracts.Access;
using Argos.Contracts.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Argos.Api.Controllers.Inventory;

/// <summary>
/// Bairros (RF-020, RN-02, RN-22; plan 15.2.1, 15.14), com ponto central e região. Leitura para qualquer usuário
/// autenticado; escrita com <c>inventory.manage</c> (D-58). Sem exclusão: só inativação (RN-23).
/// </summary>
[ApiController]
[Authorize]
[Route("api/inventory/neighborhoods")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class NeighborhoodsController(NeighborhoodService neighborhoods) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NeighborhoodData>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NeighborhoodData>>> List(
        [FromQuery] Guid? regionId, [FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        Ok(await neighborhoods.ListAsync(regionId, includeInactive, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<NeighborhoodData>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<NeighborhoodData>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await neighborhoods.GetAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = Permissions.InventoryManage)]
    [ProducesResponseType<NeighborhoodData>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<NeighborhoodData>> Create(
        NeighborhoodRequest request, CancellationToken cancellationToken)
    {
        var neighborhood = await neighborhoods.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = neighborhood.Id }, neighborhood);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.InventoryManage)]
    [ProducesResponseType<NeighborhoodData>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<NeighborhoodData>> Update(
        Guid id, NeighborhoodRequest request, CancellationToken cancellationToken) =>
        Ok(await neighborhoods.UpdateAsync(id, request, cancellationToken));

    /// <summary>Inativa o bairro; recusada (409) se houver equipamento ou base ativos nele.</summary>
    [HttpPost("{id:guid}/inactivation")]
    [Authorize(Policy = Permissions.InventoryManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Inactivate(Guid id, CancellationToken cancellationToken)
    {
        await neighborhoods.InactivateAsync(id, cancellationToken);
        return NoContent();
    }
}
