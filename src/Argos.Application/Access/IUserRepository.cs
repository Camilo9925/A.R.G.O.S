using Argos.Domain.Access;

namespace Argos.Application.Access;

/// <summary>
/// Porta de persistência dos usuários (plan 14.4). Sem remoção: usuários só são inativados (Art. V.2).
/// </summary>
public interface IUserRepository
{
    /// <summary>Busca pelo login já normalizado (<see cref="User.NormalizeLogin"/>), inclusive inativos.</summary>
    Task<User?> FindByLoginAsync(string normalizedLogin, CancellationToken cancellationToken = default);

    void Add(User user);
}
