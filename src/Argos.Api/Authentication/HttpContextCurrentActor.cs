using Argos.Application.Common.Abstractions;
using Argos.Contracts.Access;
using Argos.Domain.Access;

namespace Argos.Api.Authentication;

/// <summary>
/// Autor das operações (D-43; Art. VIII.4): o login do usuário autenticado, <c>PROMETHEUS</c> para a chave de
/// sistema e <c>sistema</c> quando não há requisição (serviços em segundo plano). O perfil vem da claim <c>role</c>
/// (D-47); o papel técnico do PROMETHEUS não é um perfil de usuário e resulta em nulo.
/// </summary>
internal sealed class HttpContextCurrentActor(IHttpContextAccessor httpContextAccessor) : ICurrentActor
{
    public const string Internal = "sistema";

    public string Name =>
        httpContextAccessor.HttpContext?.User is { Identity.IsAuthenticated: true } user
            ? user.FindFirst(AccessClaimTypes.Login)?.Value ?? Internal
            : Internal;

    // Os nomes de UserRoles (Contracts) são os mesmos do enum do Domínio, garantido por teste unitário.
    public UserRole? Role =>
        httpContextAccessor.HttpContext?.User is { Identity.IsAuthenticated: true } user
        && Enum.TryParse<UserRole>(user.FindFirst(AccessClaimTypes.Role)?.Value, ignoreCase: false, out var role)
        && Enum.IsDefined(role)
            ? role
            : null;
}
