using Argos.Application.Access;
using Argos.Domain.Access;

namespace Argos.UnitTests.Application.Fakes;

/// <summary>
/// Repositório de usuários em memória.
/// </summary>
internal sealed class FakeUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];

    public Task<User?> FindByLoginAsync(string normalizedLogin, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.SingleOrDefault(user => user.Login == normalizedLogin));

    public void Add(User user) => Users.Add(user);
}
