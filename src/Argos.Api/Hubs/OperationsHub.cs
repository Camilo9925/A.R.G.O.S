using Argos.Api.Realtime;
using Argos.Contracts.Access;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Argos.Api.Hubs;

/// <summary>
/// Hub da Central de Operações. Só empurra notificações; comandos passam por controllers (plan 4.2).
/// Exige a permissão de ver a Central e põe cada conexão no grupo do seu perfil (D-38; plan 14.8).
/// </summary>
[Authorize(Policy = Permissions.OperationsView)]
internal sealed class OperationsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var role = Context.User?.FindFirst(AccessClaimTypes.Role)?.Value;
        if (role is not null && UserRoles.All.Contains(role))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.ForRole(role));
        }

        await base.OnConnectedAsync();
    }
}
