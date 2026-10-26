using Argos.Application.Predictive;
using Argos.Contracts.Access;
using Argos.Contracts.Predictive;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Argos.Api.Controllers.Predictive;

/// <summary>
/// Cidade Preditiva: índice de risco por região (US-20, US-25; RF-043, RF-049, RF-051; RN-26, RN-28 a RN-30; D-96, D-98;
/// plan 17.11). Só consulta, com <c>predictive.view</c> (Administrador e Gestor; o Operador recebe 403). O índice, o nível,
/// as faixas da legenda, a confiança, os fatores e os textos da explicação vêm prontos da Aplicação (D-59). Sem
/// <c>DELETE</c> (RN-23).
/// </summary>
[ApiController]
[Route("api/predictive/risk")]
[Authorize(Policy = Permissions.PredictiveView)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class RiskController(RiskService risk) : ControllerBase
{
    /// <summary>
    /// Grade dia da semana × faixa horária de uma região: 42 células com índice, nível, confiança e explicação. Região com
    /// poucas semanas de histórico (RN-29) vem com <c>insufficientData</c> e sem células.
    /// </summary>
    [HttpGet("regions/{regionId:guid}/grid")]
    [ProducesResponseType<RiskGrid>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<RiskGrid>> Grid(Guid regionId, CancellationToken cancellationToken) =>
        Ok(await risk.GetGridAsync(regionId, cancellationToken));

    /// <summary>
    /// As regiões coloridas pelo risco das próximas 24 horas (<c>Next24Hours</c>, o padrão) ou dos próximos 7 dias
    /// (<c>Next7Days</c>): o maior índice entre as células do horizonte, com a explicação do pico.
    /// </summary>
    [HttpGet("forecast")]
    [ProducesResponseType<RiskForecast>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<RiskForecast>> Forecast(
        [FromQuery] ForecastHorizon horizon = ForecastHorizon.Next24Hours, CancellationToken cancellationToken = default) =>
        Ok(await risk.GetForecastAsync(horizon, cancellationToken));
}
