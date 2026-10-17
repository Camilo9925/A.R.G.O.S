namespace Argos.Web.Services;

/// <summary>
/// Endereço base da API, usado pelos clientes HTTP e SignalR.
/// </summary>
public sealed record ApiEndpoint(Uri BaseAddress);
