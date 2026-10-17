using Argos.Application.Common.Abstractions;
using Argos.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Argos.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Preenche data UTC e autor de criação e de alteração das entidades <see cref="IAuditable"/> (Art. V.1, VIII.4).
/// </summary>
internal sealed class AuditingInterceptor(TimeProvider timeProvider, ICurrentActor currentActor) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(nameof(IAuditable.CreatedAt)).CurrentValue = now;
                    entry.Property(nameof(IAuditable.CreatedBy)).CurrentValue = currentActor.Name;
                    break;

                case EntityState.Modified:
                    entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
                    entry.Property(nameof(IAuditable.CreatedBy)).IsModified = false;
                    entry.Property(nameof(IAuditable.UpdatedAt)).CurrentValue = now;
                    entry.Property(nameof(IAuditable.UpdatedBy)).CurrentValue = currentActor.Name;
                    break;
            }
        }
    }
}
