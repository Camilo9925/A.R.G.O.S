namespace Argos.Domain.Common;

/// <summary>
/// Acumula erros de campo para lançá-los de uma vez numa <see cref="DomainValidationException"/> (D-46): o formulário
/// mostra todos os campos a corrigir, e não só o primeiro.
/// </summary>
internal sealed class FieldErrors
{
    private readonly Dictionary<string, List<string>> _errors = [];

    public bool Any => _errors.Count > 0;

    public bool Has(string field) => _errors.ContainsKey(field);

    public void Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var messages))
        {
            messages = [];
            _errors[field] = messages;
        }

        messages.Add(message);
    }

    /// <summary>Acrescenta os erros de outra validação, com um prefixo opcional no nome dos campos.</summary>
    public void AddAll(DomainValidationException exception, string? prefix = null)
    {
        foreach (var (field, messages) in exception.Errors)
        {
            foreach (var message in messages)
            {
                Add(prefix is null ? field : $"{prefix}.{field}", message);
            }
        }
    }

    /// <summary>
    /// Texto obrigatório, sem espaços nas pontas, com tamanho entre <paramref name="min"/> e <paramref name="max"/>.
    /// Devolve o texto aparado, ou vazio quando inválido (o erro fica registrado).
    /// </summary>
    public string RequiredText(string field, string? value, int min, int max, string label)
    {
        var trimmed = (value ?? string.Empty).Trim();

        if (trimmed.Length == 0)
        {
            Add(field, $"Informe {label}.");
        }
        else if (trimmed.Length < min || trimmed.Length > max)
        {
            Add(field, min == max
                ? $"{Capitalize(label)} deve ter {max} caracteres."
                : $"{Capitalize(label)} deve ter entre {min} e {max} caracteres.");
        }

        return trimmed;
    }

    /// <summary>Texto opcional: vazio vira nulo; acima de <paramref name="max"/> caracteres é recusado.</summary>
    public string? OptionalText(string field, string? value, int max, string label)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > max)
        {
            Add(field, $"{Capitalize(label)} deve ter no máximo {max} caracteres.");
        }

        return trimmed;
    }

    public void ThrowIfAny()
    {
        if (Any)
        {
            throw new DomainValidationException(_errors.ToDictionary(error => error.Key, error => error.Value.ToArray()));
        }
    }

    private static string Capitalize(string label) => char.ToUpperInvariant(label[0]) + label[1..];
}
