using Argos.Api.Hubs;
using Argos.Application.Access;
using Argos.Application.Common.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace Argos.Api.Realtime;

/// <summary>
/// Entrega as mensagens da Aplicação pelo hub. Para todos, envia a todas as conexões (todas autenticadas); para uma
/// permissão, envia só aos grupos dos perfis que a têm, segundo o catálogo (D-38; RN-27). Envio direto, sem lotes (D-30).
/// </summary>
internal sealed class SignalRRealtimeNotifier(IHubContext<OperationsHub> hubContext) : IRealtimeNotifier
{
    public Task PublishAsync<TMessage>(
        RealtimeAudience audience,
        string messageName,
        TMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audience);

        var clients = audience.Permission is { } permission
            ? hubContext.Clients.Groups(AccessPolicyCatalog.RolesWith(permission).Select(RealtimeGroups.ForRole).ToArray())
            : hubContext.Clients.All;

        return clients.SendAsync(messageName, message, cancellationToken);
    }
}
