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

    /// <param name="innerException">Erro do banco que revelou o conflito, quando a Infraestrutura o traduz (D-60).</param>
    public ConflictException(string message, Exception innerException)
        : this(message, new Dictionary<string, object?>(), innerException)
    {
    }

    public ConflictException(
        string message, IReadOnlyDictionary<string, object?> extensions, Exception? innerException = null)
        : base(message, innerException)
    {
        Extensions = extensions;
    }

    public IReadOnlyDictionary<string, object?> Extensions { get; }
}
