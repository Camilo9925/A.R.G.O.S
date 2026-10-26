using Argos.Application.Telemetry;
using Argos.Contracts.Access;
using Argos.Contracts.Telemetry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Argos.Api.Controllers.Telemetry;

/// <summary>
/// Telemetria do PROMETHEUS (RF-023, RF-024; D-71, D-72; plan 16.4, 16.10): descoberta dos recursos ativos e ingestão
/// em lote. Só o ator de sistema, pela chave <c>X-Argos-System-Key</c> (D-34): sem chave é 401; com o JWT de um usuário,
/// 403. Substitui o sinal de vida técnico da Fase 0 (D-73). Sem <c>DELETE</c> (RN-23).
/// </summary>
[ApiController]
[Route("api/telemetry")]
[Authorize(Policy = SystemPolicies.TelemetryIngest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class TelemetryController(TelemetryResourceService resources, TelemetryIngestionService ingestion) : ControllerBase
{
    /// <summary>
    /// Recursos ativos, lidos pelo Simulador a cada ciclo (D-72), com o turno da escala de cada agente e se ele cobre o
    /// instante atual no fuso do município (<c>scheduledOnDuty</c>; D-94).
    /// </summary>
    [HttpGet("resources")]
    [ProducesResponseType<TelemetryResourceList>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TelemetryResourceList>> Resources(CancellationToken cancellationToken) =>
        Ok(await resources.GetResourcesAsync(cancellationToken));

    /// <summary>
    /// Lote de telemetria (plan 16.4): situações reportadas (RN-12), posições e sinais de vida, num só
    /// <c>SaveChanges</c>. Formato inválido é 400 com o campo (<c>positions[0].latitude</c>); itens de recurso
    /// inexistente, inativo ou fora de atividade são ignorados e contados (Art. VIII.2); conflito de concorrência numa
    /// situação de agente é 409 para o lote inteiro.
    /// </summary>
    [HttpPost("batches")]
    [ProducesResponseType<TelemetryBatchResult>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<TelemetryBatchResult>> Batches(
        TelemetryBatchRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status202Accepted, await ingestion.IngestAsync(request, cancellationToken));
}
