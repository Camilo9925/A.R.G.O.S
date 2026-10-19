namespace Argos.Web.Services.Api;

/// <summary>
/// Erros por campo devolvidos pela API, para cada campo do formulário mostrar o seu (Art. X.6; D-46). Os nomes são os
/// caminhos do contrato em camelCase; um prefixo permite que um componente aninhado (localização, características)
/// procure só os seus.
/// </summary>
public sealed class FieldErrors
{
    private readonly Dictionary<string, string[]> _errors = new(StringComparer.OrdinalIgnoreCase);

    public bool Any => _errors.Count > 0;

    public void Clear() => _errors.Clear();

    public void Load(IReadOnlyDictionary<string, string[]> errors)
    {
        _errors.Clear();
        foreach (var (field, messages) in errors)
        {
            _errors[field] = messages;
        }
    }

    public bool Has(string field) => _errors.ContainsKey(field);

    public string? For(string field) => _errors.TryGetValue(field, out var messages) ? string.Join(" ", messages) : null;

    /// <summary>Mensagens dos campos que nenhum controle do formulário mostra, para o aviso geral.</summary>
    public IEnumerable<string> Except(IEnumerable<string> shownPrefixes)
    {
        var prefixes = shownPrefixes.ToArray();
        return _errors
            .Where(entry => !prefixes.Any(prefix =>
                entry.Key.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                || entry.Key.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase)))
            .SelectMany(entry => entry.Value);
    }
}
