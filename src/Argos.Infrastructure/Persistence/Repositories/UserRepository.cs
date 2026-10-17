using Argos.Application.Access;
using Argos.Domain.Access;
using Microsoft.EntityFrameworkCore;

namespace Argos.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(ArgosDbContext dbContext) : IUserRepository
{
    public Task<User?> FindByLoginAsync(string normalizedLogin, CancellationToken cancellationToken = default) =>
        dbContext.Users.SingleOrDefaultAsync(user => user.Login == normalizedLogin, cancellationToken);

    public void Add(User user) => dbContext.Users.Add(user);
}
