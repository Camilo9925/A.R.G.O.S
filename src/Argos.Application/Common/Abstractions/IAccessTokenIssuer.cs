namespace Argos.Application.Common.Abstractions;

/// <summary>
/// Porta de emissão do token de acesso (D-35; plan 14.4). Implementada pela API, que também o valida.
/// </summary>
public interface IAccessTokenIssuer
{
    AccessToken Issue(AccessTokenSubject subject);
}

/// <summary>Quem recebe o token. <paramref name="Role"/> é um dos valores de <c>UserRoles</c>.</summary>
public sealed record AccessTokenSubject(Guid UserId, string Login, string Name, string Role);

/// <summary>Token emitido e o instante UTC em que deixa de valer.</summary>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
