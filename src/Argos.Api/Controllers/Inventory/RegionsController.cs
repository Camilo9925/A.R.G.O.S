using Argos.Application.Inventory.Geography;
using Argos.Contracts.Access;
using Argos.Contracts.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Argos.Api.Controllers.Inventory;

/// <summary>
/// Regiões (zonas) do município (RF-020, RN-22; plan 15.2.1, 15.14). Leitura para qualquer usuário autenticado; escrita
/// com <c>inventory.manage</c> (D-58). Sem exclusão: só inativação (RN-23).
/// </summary>
[ApiController]
[Authorize]
[Route("api/inventory/regions")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class RegionsController(RegionService regions) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RegionData>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RegionData>>> List(
        [FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        Ok(await regions.ListAsync(includeInactive, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<RegionData>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<RegionData>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await regions.GetAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = Permissions.InventoryManage)]
    [ProducesResponseType<RegionData>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<RegionData>> Create(RegionRequest request, CancellationToken cancellationToken)
    {
        var region = await regions.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = region.Id }, region);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.InventoryManage)]
    [ProducesResponseType<RegionData>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<RegionData>> Update(
        Guid id, RegionRequest request, CancellationToken cancellationToken) =>
        Ok(await regions.UpdateAsync(id, request, cancellationToken));

    /// <summary>Inativa a região; recusada (409) se houver bairro ativo nela.</summary>
    [HttpPost("{id:guid}/inactivation")]
    [Authorize(Policy = Permissions.InventoryManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Inactivate(Guid id, CancellationToken cancellationToken)
    {
        await regions.InactivateAsync(id, cancellationToken);
        return NoContent();
    }
}
