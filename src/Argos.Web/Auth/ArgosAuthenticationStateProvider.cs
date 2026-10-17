using System.Security.Claims;
using Argos.Contracts.Access;
using Microsoft.AspNetCore.Components.Authorization;

namespace Argos.Web.Auth;

/// <summary>
/// Monta o usuário da interface a partir da sessão (D-42; plan 14.11): login, nome, perfil e uma claim por permissão.
/// Não decodifica o JWT: quem confere o token é a API; aqui ele só é repassado.
/// </summary>
public sealed class ArgosAuthenticationStateProvider(SessionStore sessionStore) : AuthenticationStateProvider
{
    private const string AuthenticationType = "argos";

    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    public override async Task<AuthenticationState> GetAuthenticationStateAsync() =>
        await sessionStore.GetAsync() is { } session ? new AuthenticationState(CreatePrincipal(session)) : Anonymous;

    /// <summary>Avisa a interface de que a sessão começou ou terminou.</summary>
    public void NotifySessionChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    private static ClaimsPrincipal CreatePrincipal(LoginResponse session)
    {
        var claims = new List<Claim>
        {
            new(AccessClaimTypes.Login, session.User.Login),
            new(AccessClaimTypes.Name, session.User.Name),
            new(AccessClaimTypes.Role, session.User.Role),
        };
        claims.AddRange(session.Permissions.Select(permission => new Claim(AccessClaimTypes.Permission, permission)));

        return new ClaimsPrincipal(
            new ClaimsIdentity(claims, AuthenticationType, AccessClaimTypes.Login, AccessClaimTypes.Role));
    }
}
