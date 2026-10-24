using Argos.Domain.Common;
using Argos.Domain.Inventory.Agents;
using Argos.Domain.Inventory.Equipments;
using Argos.Infrastructure.Persistence.Configurations.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Argos.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Preenche a coluna-sombra <c>search_text</c> dos equipamentos, com o texto normalizado de número de série, marca,
/// modelo, rua e referência, e dos agentes, com nome, nome de exibição e matrícula (D-15, D-53; plan 15.8). O Domínio
/// não a conhece; a busca compara o termo normalizado da mesma forma, então "sao" encontra "São".
/// </summary>
internal sealed class SearchTextInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Fill(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Fill(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static string SearchTextOf(Equipment equipment) =>
        TextNormalization.Normalize(string.Join(
            ' ',
            equipment.SerialNumber,
            equipment.Brand,
            equipment.Model,
            equipment.Location?.Street,
            equipment.Location?.Reference));

    private static string SearchTextOf(Agent agent) =>
        TextNormalization.Normalize(string.Join(' ', agent.FullName, agent.DisplayName, agent.RegistrationNumber));

    private static void Fill(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<Equipment>())
        {
            Fill(entry, EquipmentConfiguration.SearchTextProperty, SearchTextOf(entry.Entity));
        }

        foreach (var entry in context.ChangeTracker.Entries<Agent>())
        {
            Fill(entry, AgentConfiguration.SearchTextProperty, SearchTextOf(entry.Entity));
        }
    }

    /// <summary>Só grava quando o texto mudou, para não marcar como alterada uma linha que não mudou.</summary>
    private static void Fill<TEntity>(EntityEntry<TEntity> entry, string property, string current)
        where TEntity : class
    {
        if (entry.State is not (EntityState.Added or EntityState.Modified))
        {
            return;
        }

        var searchText = entry.Property<string>(property);
        if (searchText.CurrentValue != current)
        {
            searchText.CurrentValue = current;
        }
    }
}
