using Argos.Application.Common.Abstractions;
using Argos.Contracts.Access;

namespace Argos.Api.Authentication;

/// <summary>
/// Autor das operações (D-43; Art. VIII.4): o login do usuário autenticado, <c>PROMETHEUS</c> para a chave de
/// sistema e <c>sistema</c> quando não há requisição (serviços em segundo plano).
/// </summary>
internal sealed class HttpContextCurrentActor(IHttpContextAccessor httpContextAccessor) : ICurrentActor
{
    public const string Internal = "sistema";

    public string Name =>
        httpContextAccessor.HttpContext?.User is { Identity.IsAuthenticated: true } user
            ? user.FindFirst(AccessClaimTypes.Login)?.Value ?? Internal
            : Internal;
}
