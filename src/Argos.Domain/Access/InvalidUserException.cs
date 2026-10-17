using Argos.Domain.Common;

namespace Argos.Domain.Access;

/// <summary>
/// Dados ou operação inválidos para um usuário (plan 14.2).
/// </summary>
public sealed class InvalidUserException(string message) : DomainException(message);
