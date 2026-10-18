using Argos.Application.Inventory;
using Argos.Domain.Inventory;

namespace Argos.Infrastructure.Persistence.Repositories;

/// <summary>Registros cadastrais entram na unidade de trabalho do caso de uso (plan 15.2.11).</summary>
internal sealed class InventoryChangeLog(ArgosDbContext dbContext) : IInventoryChangeLog
{
    public void Add(InventoryChange change) => dbContext.InventoryChanges.Add(change);
}
