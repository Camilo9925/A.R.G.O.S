using Argos.Application.Access;
using Argos.Application.Common.Exceptions;
using Argos.Contracts.Access;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Argos.Api.Controllers.Access;

/// <summary>
/// Login dos usuários (RF-022; plan 14.7). Anônimo por definição. Registra no log o resultado com o login,
/// nunca a senha (D-36; plan 6.3).
/// </summary>
[ApiController]
[Route("api/access")]
[AllowAnonymous]
public sealed class AuthenticationController(LoginService loginService, ILogger<AuthenticationController> logger)
    : ControllerBase
{
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await loginService.LoginAsync(request, cancellationToken);
            logger.LogInformation("Login aceito para {Login} ({Role}).", response.User.Login, response.User.Role);
            return Ok(response);
        }
        catch (AuthenticationFailedException)
        {
            logger.LogWarning("Login recusado para {Login}.", request.Login.Trim());
            throw;
        }
    }
}
