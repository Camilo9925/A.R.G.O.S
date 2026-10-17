using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Argos.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Rejeita qualquer exclusão física: registros são inativados, nunca apagados (Art. V.2, RN-23, D-21).
/// </summary>
internal sealed class DeleteBlockingInterceptor : SaveChangesInterceptor
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
        var deleted = context?.ChangeTracker.Entries().FirstOrDefault(entry => entry.State == EntityState.Deleted);

        if (deleted is not null)
        {
            throw new InvalidOperationException(
                $"Exclusão física não é permitida ({deleted.Metadata.ClrType.Name}). Inative o registro.");
        }
    }
}
