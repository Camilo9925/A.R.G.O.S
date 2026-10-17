using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Argos.Api.Configuration;
using Argos.Contracts.Access;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Argos.Api.Authentication;

/// <summary>
/// Autentica o PROMETHEUS pela chave de sistema no cabeçalho <c>X-Argos-System-Key</c> (D-34; plan 14.6).
/// A comparação é em tempo constante, para não revelar pela demora quantos caracteres conferem.
/// </summary>
internal sealed class SystemKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<SystemKeyOptions> systemKeyOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(SystemKeyContract.HeaderName, out var provided))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var expected = Encoding.UTF8.GetBytes(systemKeyOptions.Value.Key);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided.ToString()), expected))
        {
            return Task.FromResult(AuthenticateResult.Fail("Chave de sistema inválida."));
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(AccessClaimTypes.Login, SystemActor.Name),
                new Claim(AccessClaimTypes.Name, SystemActor.Name),
                new Claim(AccessClaimTypes.Role, SystemActor.Role),
            ],
            Scheme.Name,
            AccessClaimTypes.Login,
            AccessClaimTypes.Role);

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
