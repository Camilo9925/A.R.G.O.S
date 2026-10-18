using System.Collections.Concurrent;
using Argos.Domain.Common.Parameters;

namespace Argos.Infrastructure.Persistence.Repositories;

/// <summary>Cache dos valores gravados dos parâmetros, compartilhado por todas as requisições do processo (plan 6.4).</summary>
internal sealed class BusinessParameterCache
{
    private readonly ConcurrentDictionary<BusinessParameter, int> _values = new();

    public bool TryGet(BusinessParameter parameter, out int value) => _values.TryGetValue(parameter, out value);

    public void Set(BusinessParameter parameter, int value) => _values[parameter] = value;
}
