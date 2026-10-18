using Argos.Application.Inventory.Geography;
using Argos.Domain.Inventory.Geography;

namespace Argos.Infrastructure.Persistence.Repositories;

internal sealed class ZipCodeRepository(ArgosDbContext dbContext) : IZipCodeRepository
{
    public async Task<ZipCodeEntry?> FindAsync(ZipCode zipCode, CancellationToken cancellationToken = default) =>
        await dbContext.ZipCodes.FindAsync([zipCode], cancellationToken);

    public void Add(ZipCodeEntry entry) => dbContext.ZipCodes.Add(entry);
}
