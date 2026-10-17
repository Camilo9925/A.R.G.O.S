namespace Argos.Application.Common.Exceptions;

/// <summary>
/// Violação de regra de negócio sem campo associado, como uma transição recusada. Vira 409 com o motivo (Art. XII.4, D-05).
/// </summary>
public sealed class BusinessRuleException(string message) : Exception(message);
