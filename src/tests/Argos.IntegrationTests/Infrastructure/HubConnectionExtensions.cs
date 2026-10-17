using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace Argos.IntegrationTests.Infrastructure;

internal static class HubConnectionExtensions
{
    // O OperationsHub não tem métodos; invocar um nome inexistente devolve erro, que aqui é esperado.
    private const string SynchronizationMethod = "__test_synchronize";

    /// <summary>
    /// Inicia a conexão e só retorna depois que o hub terminou o <c>OnConnectedAsync</c>, isto é, depois que a conexão
    /// entrou no grupo do seu perfil (plan 14.8). O servidor responde ao handshake antes do <c>OnConnectedAsync</c>, mas
    /// só processa invocações depois dele; a resposta de uma invocação é, portanto, a barreira, sem espera fixa.
    /// </summary>
    public static async Task StartAndSynchronizeAsync(this HubConnection connection, CancellationToken cancellationToken)
    {
        await connection.StartAsync(cancellationToken);

        try
        {
            await connection.InvokeAsync(SynchronizationMethod, cancellationToken);
        }
        catch (HubException)
        {
            // Esperado: o método não existe. O que importa é que o servidor já respondeu.
        }
    }
}
