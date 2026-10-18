using Argos.Application.Inventory.Support;
using Argos.Contracts.Access;
using Argos.Contracts.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Argos.Api.Controllers.Inventory;

/// <summary>
/// Órgãos (RF-019, RN-22; plan 15.2.2, 15.14). Leitura para qualquer usuário autenticado; escrita com
/// <c>inventory.manage</c> (D-58). Sem exclusão: só inativação (RN-23).
/// </summary>
[ApiController]
[Authorize]
[Route("api/inventory/organizations")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class OrganizationsController(OrganizationService organizations) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OrganizationData>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrganizationData>>> List(
        [FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        Ok(await organizations.ListAsync(includeInactive, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrganizationData>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<OrganizationData>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await organizations.GetAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = Permissions.InventoryManage)]
    [ProducesResponseType<OrganizationData>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<OrganizationData>> Create(
        OrganizationRequest request, CancellationToken cancellationToken)
    {
        var organization = await organizations.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = organization.Id }, organization);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.InventoryManage)]
    [ProducesResponseType<OrganizationData>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<OrganizationData>> Update(
        Guid id, OrganizationRequest request, CancellationToken cancellationToken) =>
        Ok(await organizations.UpdateAsync(id, request, cancellationToken));

    /// <summary>Inativa o órgão.</summary>
    [HttpPost("{id:guid}/inactivation")]
    [Authorize(Policy = Permissions.InventoryManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Inactivate(Guid id, CancellationToken cancellationToken)
    {
        await organizations.InactivateAsync(id, cancellationToken);
        return NoContent();
    }
}
