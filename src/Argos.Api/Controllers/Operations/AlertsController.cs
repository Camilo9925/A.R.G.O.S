using Argos.Application.Operations;
using Argos.Contracts.Access;
using Argos.Contracts.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Argos.Api.Controllers.Operations;

/// <summary>
/// Tratamento de alertas (US-15, RF-036, RN-20; D-83; plan 16.6, 16.10), com <c>alerts.handle</c>. As transições e as
/// recusas ficam no Domínio: reconhecer o já reconhecido, tratar o resolvido e resolver um crítico não reconhecido são
/// 409 com o motivo; a observação da resolução é obrigatória (400 em <c>notes</c>). O alerta nunca é apagado (RN-23).
/// </summary>
[ApiController]
[Route("api/operations/alerts/{id:guid}")]
[Authorize(Policy = Permissions.AlertsHandle)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
public sealed class AlertsController(AlertService alerts) : ControllerBase
{
    /// <summary>Reconhecimento: registra quem e quando; o alerta continua aberto e para de piscar (RN-20).</summary>
    [HttpPost("acknowledgement")]
    [ProducesResponseType<AlertSummary>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AlertSummary>> Acknowledge(Guid id, CancellationToken cancellationToken) =>
        Ok(await alerts.AcknowledgeAsync(id, cancellationToken));

    /// <summary>Resolução com observação de 1 a 500 caracteres (RN-20); o crítico precisa estar reconhecido.</summary>
    [HttpPost("resolution")]
    [ProducesResponseType<AlertSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<AlertSummary>> Resolve(
        Guid id, ResolveAlertRequest request, CancellationToken cancellationToken) =>
        Ok(await alerts.ResolveAsync(id, request, cancellationToken));
}
