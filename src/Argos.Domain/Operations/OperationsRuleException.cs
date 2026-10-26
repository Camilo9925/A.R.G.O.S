using Argos.Domain.Common;

namespace Argos.Domain.Operations;

/// <summary>
/// Regra da Central violada sem campo associado, como reconhecer um alerta já reconhecido (RN-20). Vira 409 com o
/// motivo (Art. XII.4); os erros com campo usam <see cref="DomainValidationException"/>.
/// </summary>
public sealed class OperationsRuleException(string message) : DomainException(message);
