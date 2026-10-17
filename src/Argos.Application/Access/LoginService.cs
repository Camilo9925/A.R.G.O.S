using Argos.Application.Common.Abstractions;
using Argos.Application.Common.Exceptions;
using Argos.Contracts.Access;
using Argos.Domain.Access;

namespace Argos.Application.Access;

/// <summary>
/// Login de usuário (RF-022; D-36; plan 14.4). Qualquer falha gera a mesma <see cref="AuthenticationFailedException"/>.
/// </summary>
public sealed class LoginService(IUserRepository users, IPasswordHasher passwordHasher, IAccessTokenIssuer tokenIssuer)
{
    // Senha usada só para produzir um hash de comparação quando o login não existe.
    private const string DummyPassword = "argos-login-inexistente";

    private static string? _dummyHash;

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var login = User.NormalizeLogin(request.Login);
        var password = request.Password ?? string.Empty;
        var user = login.Length == 0 ? null : await users.FindByLoginAsync(login, cancellationToken);

        if (user is null)
        {
            // Mesmo custo de verificação de um login existente: a demora não revela quais logins existem.
            _dummyHash ??= passwordHasher.Hash(DummyPassword);
            passwordHasher.Verify(_dummyHash, password);
            throw new AuthenticationFailedException();
        }

        if (!passwordHasher.Verify(user.PasswordHash, password) || !user.IsActive)
        {
            throw new AuthenticationFailedException();
        }

        var role = AccessPolicyCatalog.RoleName(user.Role);
        var token = tokenIssuer.Issue(new AccessTokenSubject(user.Id, user.Login, user.Name, role));

        return new LoginResponse
        {
            AccessToken = token.Value,
            ExpiresAt = token.ExpiresAt,
            User = new AuthenticatedUser { Login = user.Login, Name = user.Name, Role = role },
            Permissions = AccessPolicyCatalog.PermissionsFor(user.Role),
        };
    }
}
