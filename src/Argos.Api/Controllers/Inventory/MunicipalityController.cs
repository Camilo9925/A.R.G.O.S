using Argos.Application.Inventory.Geography;
using Argos.Contracts.Access;
using Argos.Contracts.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Argos.Api.Controllers.Inventory;

/// <summary>
/// O município da demonstração e a sugestão de endereço por CEP (RF-004, RF-020; D-50, D-52; plan 15.9, 15.14). Os
/// dados do município são lista de referência, para qualquer usuário autenticado; o CEP serve ao cadastro (D-58).
/// </summary>
[ApiController]
[Authorize]
[Route("api/inventory")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class MunicipalityController(MunicipalityService municipalities) : ControllerBase
{
    [HttpGet("municipality")]
    [ProducesResponseType<MunicipalityData>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<MunicipalityData>> Get(CancellationToken cancellationToken) =>
        Ok(await municipalities.GetAsync(cancellationToken));

    /// <summary>Sugestão de rua, bairro, região, cidade e UF; o CEP vai com ou sem hífen.</summary>
    [HttpGet("zip-codes/{zipCode}")]
    [Authorize(Policy = Permissions.InventoryManage)]
    [ProducesResponseType<ZipCodeSuggestion>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ZipCodeSuggestion>> SuggestAddress(
        string zipCode, CancellationToken cancellationToken) =>
        Ok(await municipalities.SuggestAsync(zipCode, cancellationToken));
}
