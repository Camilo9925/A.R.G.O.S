using Argos.Application.Diagnostics;
using Argos.Contracts.Realtime;
using Microsoft.AspNetCore.Mvc;

namespace Argos.Api.Controllers.Diagnostics;

/// <summary>
/// Recebe o sinal de vida técnico do Simulador (temporário, Fase 0; plan 4.4).
/// Será removido quando a ingestão de telemetria real chegar. Sem autenticação até a rodada de Acesso.
/// </summary>
[ApiController]
[Route("api/diagnostics/heartbeats")]
public sealed class TechnicalHeartbeatsController(TechnicalHeartbeatService heartbeatService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Receive(TechnicalHeartbeat heartbeat, CancellationToken cancellationToken)
    {
        await heartbeatService.ReceiveAsync(heartbeat, cancellationToken);
        return Accepted();
    }
}
