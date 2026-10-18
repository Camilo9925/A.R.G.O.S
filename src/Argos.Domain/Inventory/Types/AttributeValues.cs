using System.Collections;

namespace Argos.Domain.Inventory.Types;

/// <summary>
/// Valores das características de um equipamento, já validados e tipados pelo tipo do equipamento (D-48;
/// plan 15.2.5). Cada valor é <see cref="string"/> (Texto e Opção, que guarda a chave da opção), <see cref="long"/>
/// (Inteiro), <see cref="decimal"/> (Decimal), <see cref="bool"/> (Sim/não) ou uma lista de <see cref="string"/>
/// (Múltipla escolha, chaves na ordem das opções). Característica ausente não tem entrada. Imutável e comparável
/// por valor, para detectar alterações (D-49) e para o <c>ValueComparer</c> da persistência.
/// </summary>
public sealed class AttributeValues : IReadOnlyDictionary<string, object>, IEquatable<AttributeValues>
{
    private readonly IReadOnlyDictionary<string, object> _values;

    private AttributeValues(IReadOnlyDictionary<string, object> values) => _values = values;

    public static AttributeValues Empty { get; } = new(new Dictionary<string, object>());

    public int Count => _values.Count;

    public IEnumerable<string> Keys => _values.Keys;

    public IEnumerable<object> Values => _values.Values;

    public object this[string key] => _values[key];

    /// <summary>
    /// Reconstrói valores já validados, como os lidos do banco. Só aceita os tipos listados acima; a validação contra
    /// o tipo do equipamento é de <see cref="EquipmentType.ValidateAttributes"/>.
    /// </summary>
    public static AttributeValues Restore(IReadOnlyDictionary<string, object> values)
    {
        var copy = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var (key, value) in values)
        {
            copy[key] = value switch
            {
                string or long or decimal or bool => value,
                IEnumerable<string> list => (IReadOnlyList<string>)list.ToArray(),
                _ => throw new ArgumentException($"Tipo de valor não suportado na característica \"{key}\".", nameof(values)),
            };
        }

        return new AttributeValues(copy);
    }

    public bool ContainsKey(string key) => _values.ContainsKey(key);

    public bool TryGetValue(string key, out object value) => _values.TryGetValue(key, out value!);

    public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(AttributeValues? other) =>
        other is not null
        && Count == other.Count
        && _values.All(entry => other.TryGetValue(entry.Key, out var value) && ValueEquals(entry.Value, value));

    public override bool Equals(object? obj) => Equals(obj as AttributeValues);

    public override int GetHashCode()
    {
        var hash = 0;
        foreach (var key in _values.Keys)
        {
            hash ^= StringComparer.Ordinal.GetHashCode(key);
        }

        return hash;
    }

    internal static bool ValueEquals(object? left, object? right) => (left, right) switch
    {
        (IReadOnlyList<string> a, IReadOnlyList<string> b) => a.SequenceEqual(b),
        _ => Equals(left, right),
    };
}
