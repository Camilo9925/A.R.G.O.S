using Argos.Application.Common.Abstractions;
using Argos.Application.Common.Exceptions;
using Argos.Contracts.Realtime;

namespace Argos.Application.Diagnostics;

/// <summary>
/// Recebe o sinal de vida técnico do Simulador e o repassa à Central em tempo real (temporário, Fase 0; plan 4.4).
/// </summary>
public sealed class TechnicalHeartbeatService(IRealtimeNotifier notifier, TimeProvider timeProvider)
{
    public Task ReceiveAsync(TechnicalHeartbeat heartbeat, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(heartbeat);

        var sentAt = heartbeat.SentAt
            ?? throw new ValidationFailedException("sentAt", "O instante de envio é obrigatório.");

        var notification = new TechnicalHeartbeatNotification(
            heartbeat.Source,
            heartbeat.Sequence,
            sentAt.ToUniversalTime(),
            timeProvider.GetUtcNow());

        return notifier.PublishAsync(
            RealtimeAudience.Everyone,
            OperationsHubContract.Messages.TechnicalHeartbeatReceived,
            notification,
            cancellationToken);
    }
}
