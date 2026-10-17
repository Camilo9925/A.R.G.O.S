namespace Argos.Application.Common.Abstractions;

/// <summary>
/// Porta de publicação de mensagens de tempo real para os clientes da Central (plan 4.2).
/// A Aplicação decide o que publicar e para quem (<see cref="RealtimeAudience"/>, D-38); a API entrega pelo SignalR.
/// </summary>
public interface IRealtimeNotifier
{
    Task PublishAsync<TMessage>(
        RealtimeAudience audience,
        string messageName,
        TMessage message,
        CancellationToken cancellationToken = default);
}
