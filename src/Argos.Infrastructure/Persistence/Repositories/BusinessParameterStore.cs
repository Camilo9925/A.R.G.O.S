using Argos.Application.Common.Abstractions;
using Argos.Domain.Common.Parameters;
using Microsoft.EntityFrameworkCore;

namespace Argos.Infrastructure.Persistence.Repositories;

/// <summary>
/// Parâmetros de negócio gravados em <c>business_parameters</c> (RN-34; D-55; plan 15.10), lidos com cache em memória.
/// Só entra no cache o valor encontrado no banco: enquanto a chave não foi gravada, vale o padrão do catálogo e a próxima
/// leitura volta ao banco, então a carga que grava a chave é vista sem invalidação. A invalidação de um valor alterado
/// entra com a edição (RF-021).
/// </summary>
internal sealed class BusinessParameterStore(ArgosDbContext dbContext, BusinessParameterCache cache) : IBusinessParameters
{
    public async Task<int> GetAsync(BusinessParameter parameter, CancellationToken cancellationToken = default)
    {
        if (cache.TryGet(parameter, out var cached))
        {
            return cached;
        }

        var definition = BusinessParameterCatalog.Get(parameter);
        var stored = await dbContext.BusinessParameters
            .AsNoTracking()
            .Where(value => value.Key == definition.Key)
            .Select(value => (decimal?)value.Value)
            .SingleOrDefaultAsync(cancellationToken);

        if (stored is not { } value)
        {
            return definition.Default;
        }

        var parsed = decimal.ToInt32(value);
        cache.Set(parameter, parsed);
        return parsed;
    }

    public async Task<IReadOnlyCollection<BusinessParameter>> ListStoredAsync(CancellationToken cancellationToken = default)
    {
        var keys = await dbContext.BusinessParameters
            .AsNoTracking()
            .Select(value => value.Key)
            .ToListAsync(cancellationToken);

        return keys
            .Select(BusinessParameterCatalog.FindByKey)
            .OfType<BusinessParameterDefinition>()
            .Select(definition => definition.Parameter)
            .ToArray();
    }

    public void Add(BusinessParameter parameter, int value)
    {
        var definition = BusinessParameterCatalog.Get(parameter);
        dbContext.BusinessParameters.Add(new BusinessParameterValue(definition.Key, value));
    }
}
