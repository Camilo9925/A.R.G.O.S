namespace Argos.Application.Common.Exceptions;

/// <summary>
/// Conflito com o estado atual, como duplicidade ou concorrência otimista. Vira 409 (Art. XII.4). As extensões
/// entram no <c>ProblemDetails</c>, como o <c>existingEquipmentId</c> da RN-01 (plan 15.7).
/// </summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message)
        : this(message, new Dictionary<string, object?>())
    {
    }

    public ConflictException(string message, IReadOnlyDictionary<string, object?> extensions)
        : base(message)
    {
        Extensions = extensions;
    }

    public IReadOnlyDictionary<string, object?> Extensions { get; }
}
