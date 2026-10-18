using Argos.Domain.Common;

namespace Argos.Domain.Inventory;

/// <summary>
/// Regra do Inventário violada sem campo associado, como uma transição recusada ou uma inativação com dependentes
/// ativos. Vira 409 com o motivo (Art. XII.4; D-05). Os erros com campo usam <see cref="DomainValidationException"/>.
/// </summary>
public sealed class InventoryRuleException(string message) : DomainException(message);
