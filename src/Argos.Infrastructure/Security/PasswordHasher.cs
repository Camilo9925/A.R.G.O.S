using Microsoft.AspNetCore.Identity;
using IPasswordHasherPort = Argos.Application.Common.Abstractions.IPasswordHasher;

namespace Argos.Infrastructure.Security;

/// <summary>
/// Hash de senha com o <see cref="PasswordHasher{TUser}"/> do <c>Microsoft.Extensions.Identity.Core</c>
/// (PBKDF2; plan 9.6, 9.7). O algoritmo não usa o usuário, por isso o tipo genérico é só <see cref="object"/>.
/// </summary>
internal sealed class PasswordHasher : IPasswordHasherPort
{
    private static readonly object Owner = new();

    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        return _hasher.HashPassword(Owner, password);
    }

    public bool Verify(string passwordHash, string password)
    {
        ArgumentNullException.ThrowIfNull(passwordHash);
        ArgumentNullException.ThrowIfNull(password);

        // SuccessRehashNeeded também é sucesso; não há rehash automático (D-36).
        return _hasher.VerifyHashedPassword(Owner, passwordHash, password) != PasswordVerificationResult.Failed;
    }
}
