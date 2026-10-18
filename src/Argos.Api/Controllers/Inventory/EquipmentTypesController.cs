using Argos.Application.Inventory.Types;
using Argos.Contracts.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Argos.Api.Controllers.Inventory;

/// <summary>
/// Tipos de equipamento com as definições das características (RF-006; D-48; plan 15.5, 15.14), lista de referência
/// para qualquer usuário autenticado (D-58). A criação de tipos e a inativação chegam na Fase 3 (US-10).
/// </summary>
[ApiController]
[Authorize]
[Route("api/inventory/equipment-types")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class EquipmentTypesController(EquipmentTypeService types) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<EquipmentTypeData>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EquipmentTypeData>>> List(
        [FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        Ok(await types.ListAsync(includeInactive, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<EquipmentTypeData>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<EquipmentTypeData>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await types.GetAsync(id, cancellationToken));
}
