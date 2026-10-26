using Argos.Application.Operations;
using Argos.Contracts.Access;
using Argos.Contracts.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Argos.Api.Controllers.Operations;

/// <summary>
/// Central de Operações (US-11 a US-14; RF-027 a RF-034; D-79, D-81, D-83; plan 16.7, 16.10): retrato e detalhes pelo
/// mapa. Escopo dos filtros (RN-36), visibilidade de agentes (RN-27, A-38), indicadores (RN-14, RN-15) e ações
/// permitidas vêm prontos da Aplicação (D-59). A mudança de situação pelo mapa usa as rotas do Inventário (D-81). Sem
/// <c>DELETE</c> (RN-23).
/// </summary>
[ApiController]
[Route("api/operations")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class OperationsController(OperationsSnapshotService snapshots, OperationsDetailsService details) : ControllerBase
{
    /// <summary>
    /// Retrato para o filtro: marcadores, indicadores e alertas abertos (RN-21, com <c>outsideFilter</c>). Cada filtro
    /// se repete na query string para vários valores ("ou"); filtros diferentes combinam por "e" (D-53).
    /// </summary>
    [HttpGet("snapshot")]
    [Authorize(Policy = Permissions.OperationsView)]
    [ProducesResponseType<OperationsSnapshot>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<OperationsSnapshot>> Snapshot(
        [FromQuery] OperationsSnapshotQuery query, CancellationToken cancellationToken) =>
        Ok(await snapshots.GetAsync(query, cancellationToken));

    /// <summary>
    /// Detalhes de um equipamento (US-14): os do Inventário, a posição atual (sem ela para viatura e moto, para quem não
    /// vê agentes; A-38), a última comunicação e os alertas abertos.
    /// </summary>
    [HttpGet("equipment/{id:guid}")]
    [Authorize(Policy = Permissions.OperationsView)]
    [ProducesResponseType<OperationsEquipmentDetails>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<OperationsEquipmentDetails>> Equipment(Guid id, CancellationToken cancellationToken) =>
        Ok(await details.GetEquipmentAsync(id, cancellationToken));

    /// <summary>
    /// Detalhes de um agente (US-14), sem nome completo nem matrícula (RN-25); só com <c>agents.positions.view</c>
    /// (RN-27): o Gestor recebe 403.
    /// </summary>
    [HttpGet("agents/{id:guid}")]
    [Authorize(Policy = Permissions.AgentPositionsView)]
    [ProducesResponseType<OperationsAgentDetails>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<OperationsAgentDetails>> Agent(Guid id, CancellationToken cancellationToken) =>
        Ok(await details.GetAgentAsync(id, cancellationToken));
}
