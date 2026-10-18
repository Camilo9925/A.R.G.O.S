namespace Argos.Domain.Common;

/// <summary>
/// Violação de regra de domínio ligada a um ou mais campos (D-46; plan 15.7). O nome do campo é o caminho em
/// camelCase igual ao do contrato (<c>serialNumber</c>, <c>attributes.maxSpeedKmh</c>). Vira 400 com os campos;
/// as demais <see cref="DomainException"/>, sem campo, continuam 409.
/// </summary>
public sealed class DomainValidationException : DomainException
{
    public DomainValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] })
    {
    }

    public DomainValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("Um ou mais campos são inválidos.")
    {
        if (errors.Count == 0)
        {
            throw new ArgumentException("Informe ao menos um campo inválido.", nameof(errors));
        }

        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
