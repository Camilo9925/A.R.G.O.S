using Argos.Api.Configuration;
using Argos.Application.Common.Abstractions;
using Argos.Contracts.Access;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Argos.Api.Authentication;

/// <summary>
/// Emite o JWT dos usuários (D-35; plan 14.6): HS256, 8 h, sem refresh e sem permissões no token
/// (a API decide pelo perfil).
/// </summary>
internal sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> jwtOptions, TimeProvider timeProvider) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Issue(AccessTokenSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        var options = jwtOptions.Value;

        // O JWT guarda instantes em segundos; a expiração devolvida à Web é a mesma do token.
        var now = timeProvider.GetUtcNow();
        now = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
        var expiresAt = now.AddHours(options.LifetimeHours);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(options.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = subject.UserId.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
                [AccessClaimTypes.Login] = subject.Login,
                [AccessClaimTypes.Name] = subject.Name,
                [AccessClaimTypes.Role] = subject.Role,
            },
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }
}
