using Argos.Contracts.Realtime;
using Microsoft.AspNetCore.SignalR.Client;

namespace Argos.Web.Services.Realtime;

/// <summary>
/// Conexão com o hub da Central (plan 4.2). Reconecta sozinha; se a reconexão automática desistir,
/// continua tentando em segundo plano, para voltar a "Conectado" sem recarregar a página.
/// </summary>
public sealed class RealtimeConnection : IAsyncDisposable
{
    private static readonly TimeSpan RestartInterval = TimeSpan.FromSeconds(5);

    private readonly HubConnection _connection;
    private readonly CancellationTokenSource _disposing = new();
    private Task? _restartLoop;

    public RealtimeConnection(ApiEndpoint apiEndpoint)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(new Uri(apiEndpoint.BaseAddress, OperationsHubContract.Path.TrimStart('/')))
            .WithAutomaticReconnect()
            .Build();

        _connection.On<TechnicalHeartbeatNotification>(
            OperationsHubContract.Messages.TechnicalHeartbeatReceived,
            notification =>
            {
                LastHeartbeat = notification;
                Changed?.Invoke();
            });

        _connection.Reconnecting += _ =>
        {
            SetState(RealtimeConnectionState.Reconnecting);
            return Task.CompletedTask;
        };

        _connection.Reconnected += _ =>
        {
            SetState(RealtimeConnectionState.Connected);
            return Task.CompletedTask;
        };

        _connection.Closed += _ =>
        {
            SetState(RealtimeConnectionState.Interrupted);
            StartRestartLoop();
            return Task.CompletedTask;
        };
    }

    /// <summary>Disparado a cada mudança de estado ou mensagem recebida.</summary>
    public event Action? Changed;

    public RealtimeConnectionState State { get; private set; } = RealtimeConnectionState.Connecting;

    public TechnicalHeartbeatNotification? LastHeartbeat { get; private set; }

    /// <summary>Inicia a conexão sem bloquear a interface. Chamadas repetidas são ignoradas.</summary>
    public void Start()
    {
        if (_restartLoop is null && _connection.State == HubConnectionState.Disconnected)
        {
            StartRestartLoop();
        }
    }

    private void StartRestartLoop()
    {
        if (_restartLoop is { IsCompleted: false })
        {
            return;
        }

        _restartLoop = RestartUntilConnectedAsync(_disposing.Token);
    }

    private async Task RestartUntilConnectedAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _connection.State == HubConnectionState.Disconnected)
        {
            try
            {
                await _connection.StartAsync(cancellationToken);
                SetState(RealtimeConnectionState.Connected);
                return;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                SetState(RealtimeConnectionState.Interrupted);
            }

            try
            {
                await Task.Delay(RestartInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private void SetState(RealtimeConnectionState state)
    {
        State = state;
        Changed?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        await _disposing.CancelAsync();
        await _connection.DisposeAsync();
        _disposing.Dispose();
    }
}
