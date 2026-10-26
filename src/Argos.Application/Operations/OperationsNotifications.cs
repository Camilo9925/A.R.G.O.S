using Argos.Application.Common.Abstractions;
using Argos.Contracts.Operations;
using Argos.Contracts.Realtime;

namespace Argos.Application.Operations;

/// <summary>
/// Aviso <c>OperationsChanged</c> (D-80; plan 16.8, 16.11): publicado para todos, sempre <b>depois</b> do
/// <c>SaveChanges</c> do caso de uso, e nunca quando a gravação falha. A Web busca um novo retrato ao recebê-lo.
/// </summary>
public static class OperationsNotifications
{
    public static Task PublishOperationsChangedAsync(
        this IRealtimeNotifier notifier, OperationsChangeKind kind, DateTimeOffset occurredAt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notifier);

        return notifier.PublishAsync(
            RealtimeAudience.Everyone,
            OperationsHubContract.Messages.OperationsChanged,
            new OperationsChanged { Kind = kind, OccurredAt = occurredAt },
            cancellationToken);
    }
}
