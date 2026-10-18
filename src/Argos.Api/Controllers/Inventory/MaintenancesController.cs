using Argos.Application.Inventory.Equipments;
using Argos.Contracts.Access;
using Argos.Contracts.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Argos.Api.Controllers.Inventory;

/// <summary>
/// Manutenções de um equipamento (US-05, RF-009; plan 15.4, 15.14): lista com <c>inventory.view</c>; registro e
/// conclusão com <c>inventory.manage</c> (D-58). Manutenções não são excluídas nem reabertas (RN-23).
/// </summary>
[ApiController]
[Route("api/inventory/equipment/{id:guid}/maintenances")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
public sealed class MaintenancesController(EquipmentQueryService queries, MaintenanceService maintenances)
    : ControllerBase
{
    /// <summary>
    /// Manutenções abertas e encerradas, das mais recentes para as mais antigas, filtradas pelo estado se informado.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Permissions.InventoryView)]
    [ProducesResponseType<IReadOnlyList<MaintenanceData>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MaintenanceData>>> List(
        Guid id, [FromQuery] MaintenanceState? state, CancellationToken cancellationToken) =>
        Ok(await queries.ListMaintenancesAsync(id, state, cancellationToken));

    /// <summary>
    /// Registro (RN-08): campos incoerentes com o tipo são 400; equipamento desativado ou furtado, ou uma segunda
    /// imediata aberta, 409 (RN-07, RN-11). Não há consulta de uma manutenção isolada, então a resposta não traz
    /// <c>Location</c>.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Permissions.InventoryManage)]
    [ProducesResponseType<MaintenanceData>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<MaintenanceData>> Register(
        Guid id, RegisterMaintenanceRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await maintenances.RegisterAsync(id, request, cancellationToken));

    /// <summary>
    /// Conclusão (US-05): a situação segue a RN-11 e a periódica programa a próxima (RN-09). Manutenção já encerrada
    /// é 409.
    /// </summary>
    [HttpPost("{maintenanceId:guid}/completion")]
    [Authorize(Policy = Permissions.InventoryManage)]
    [ProducesResponseType<MaintenanceCompletionData>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<MaintenanceCompletionData>> Complete(
        Guid id, Guid maintenanceId, CompleteMaintenanceRequest request, CancellationToken cancellationToken) =>
        Ok(await maintenances.CompleteAsync(id, maintenanceId, request, cancellationToken));
}
