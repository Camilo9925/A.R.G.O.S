namespace Argos.Application.Common.Exceptions;

/// <summary>
/// Dados inválidos com campo associado. Vira 400 com os campos inválidos (Art. XII.4).
/// </summary>
public sealed class ValidationFailedException : Exception
{
    public ValidationFailedException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] })
    {
    }

    public ValidationFailedException(IReadOnlyDictionary<string, string[]> errors)
        : base("Um ou mais campos são inválidos.")
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
