using Argos.Domain.Common;

namespace Argos.Application.Common.Exceptions;

/// <summary>
/// Junta os erros de campo de vários passos de um caso de uso, como a localização e o cadastro do equipamento, para
/// lançá-los de uma vez numa <see cref="DomainValidationException"/> (D-46; US-01: o formulário mostra todos os campos
/// a corrigir). Erros sem campo continuam saindo na hora.
/// </summary>
internal sealed class DomainErrorCollector
{
    private readonly Dictionary<string, List<string>> _errors = [];

    public bool Any => _errors.Count > 0;

    public void Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var messages))
        {
            messages = [];
            _errors[field] = messages;
        }

        messages.Add(message);
    }

    /// <summary>Se já há erro no campo ou em algum campo dentro dele (<c>location</c> ou <c>location.zipCode</c>).</summary>
    public bool HasUnder(string field) =>
        _errors.Keys.Any(key => key == field || key.StartsWith($"{field}.", StringComparison.Ordinal));

    /// <summary>
    /// Executa <paramref name="build"/> e guarda os erros de campo, devolvendo o padrão quando há erro.
    /// <paramref name="skip"/> descarta erros que repetem outro já guardado, como "informe a localização" quando a
    /// localização já tem erros nos seus campos.
    /// </summary>
    public T? Try<T>(Func<T> build, Func<string, bool>? skip = null)
    {
        try
        {
            return build();
        }
        catch (DomainValidationException exception)
        {
            Merge(exception, skip);
            return default;
        }
    }

    public async Task<T?> TryAsync<T>(Func<Task<T>> build)
    {
        try
        {
            return await build();
        }
        catch (DomainValidationException exception)
        {
            Merge(exception, skip: null);
            return default;
        }
    }

    public void ThrowIfAny()
    {
        if (Any)
        {
            throw new DomainValidationException(_errors.ToDictionary(error => error.Key, error => error.Value.ToArray()));
        }
    }

    private void Merge(DomainValidationException exception, Func<string, bool>? skip)
    {
        foreach (var (field, messages) in exception.Errors)
        {
            if (skip?.Invoke(field) == true)
            {
                continue;
            }

            foreach (var message in messages)
            {
                Add(field, message);
            }
        }
    }
}
