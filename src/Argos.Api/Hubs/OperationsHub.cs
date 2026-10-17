using Microsoft.AspNetCore.SignalR;

namespace Argos.Api.Hubs;

/// <summary>
/// Hub da Central de Operações. Só empurra notificações; comandos passam por controllers (plan 4.2).
/// </summary>
internal sealed class OperationsHub : Hub;
