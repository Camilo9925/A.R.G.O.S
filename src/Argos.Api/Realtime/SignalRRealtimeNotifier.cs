using Argos.Api.Hubs;
using Argos.Application.Common.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace Argos.Api.Realtime;

/// <summary>
/// Entrega as mensagens da Aplicação a todos os clientes do hub. Envio direto, sem lotes nem grupos na Fase 0 (D-30).
/// </summary>
internal sealed class SignalRRealtimeNotifier(IHubContext<OperationsHub> hubContext) : IRealtimeNotifier
{
    public Task PublishAsync<TMessage>(string messageName, TMessage message, CancellationToken cancellationToken = default) =>
        hubContext.Clients.All.SendAsync(messageName, message, cancellationToken);
}
