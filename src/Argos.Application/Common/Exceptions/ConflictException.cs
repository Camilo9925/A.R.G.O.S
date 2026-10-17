namespace Argos.Application.Common.Exceptions;

/// <summary>
/// Conflito com o estado atual, como duplicidade ou concorrência otimista. Vira 409 (Art. XII.4).
/// </summary>
public sealed class ConflictException(string message) : Exception(message);
