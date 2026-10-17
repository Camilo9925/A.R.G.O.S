namespace Argos.Application.Common.Exceptions;

/// <summary>
/// Recurso inexistente. Vira 404 (Art. XII.4).
/// </summary>
public sealed class NotFoundException(string message) : Exception(message);
