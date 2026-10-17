using Argos.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Argos.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Rejeita alteração ou remoção de registros de histórico <see cref="IAppendOnly"/> (Art. V.1).
/// </summary>
internal sealed class AppendOnlyInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Reject(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Reject(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Reject(DbContext? context)
    {
        var changed = context?.ChangeTracker.Entries<IAppendOnly>()
            .FirstOrDefault(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (changed is not null)
        {
            throw new InvalidOperationException(
                $"Registros de histórico não podem ser alterados nem removidos ({changed.Metadata.ClrType.Name}).");
        }
    }
}
