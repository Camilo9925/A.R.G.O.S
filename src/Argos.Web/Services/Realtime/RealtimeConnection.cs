using System.Net;
using System.Text.Json.Serialization;
using Argos.Contracts.Realtime;
using Argos.Web.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;

namespace Argos.Web.Services.Realtime;

/// <summary>
/// Conexão com o hub da Central (plan 4.2, 14.8), presa à sessão: conecta com o token só quando há sessão e
/// desconecta no logout. Reconecta sozinha; se a reconexão automática desistir, continua tentando em segundo plano,
/// para voltar a "Conectado" sem recarregar a página. Se a conexão cair porque o token venceu, encerra a sessão em
/// vez de tentar de novo (D-37).
/// </summary>
public sealed class RealtimeConnection : IAsyncDisposable
{
    private static readonly TimeSpan RestartInterval = TimeSpan.FromSeconds(5);

    private readonly HubConnection _connection;
    private readonly SessionStore _sessionStore;
    private readonly SessionService _sessionService;
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private CancellationTokenSource? _session;
    private Task? _restartLoop;
    private bool _hasConnected;

    public RealtimeConnection(
        ApiEndpoint apiEndpoint,
        SessionStore sessionStore,
        SessionService sessionService,
        AuthenticationStateProvider authenticationStateProvider)
    {
        _sessionStore = sessionStore;
        _sessionService = sessionService;
        _authenticationStateProvider = authenticationStateProvider;

        _connection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(apiEndpoint.BaseAddress, OperationsHubContract.Path.TrimStart('/')),
                options => options.AccessTokenProvider = () => Task.FromResult(_sessionStore.Current?.AccessToken))
            .WithAutomaticReconnect()
            // A API manda os enums como texto (JsonConfiguration); sem o conversor, OperationsChanged não é lido.
            .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();

        // Um handler por mensagem (plan 16.11). A Central escuta os eventos; o indicador do cabeçalho só precisa de
        // LastTelemetryAt, que avança a cada lote aceito, mesmo sem posições.
        _connection.On<TelemetryBatchProcessed>(
            OperationsHubContract.Messages.TelemetryBatchProcessed,
            message =>
            {
                NoteTelemetry(message.ReceivedAt);
                TelemetryReceived?.Invoke(message);
            });

        _connection.On<AgentPositionsUpdated>(
            OperationsHubContract.Messages.AgentPositionsUpdated,
            message => AgentPositionsReceived?.Invoke(message));

        _connection.On<OperationsChanged>(
            OperationsHubContract.Messages.OperationsChanged,
            message => OperationsChangedReceived?.Invoke(message));

        _connection.Reconnecting += _ =>
        {
            SetState(RealtimeConnectionState.Reconnecting);
            return Task.CompletedTask;
        };

        _connection.Reconnected += _ =>
        {
            SetState(RealtimeConnectionState.Connected);
            Reconnected?.Invoke();
            return Task.CompletedTask;
        };

        _connection.Closed += OnClosedAsync;

        _authenticationStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
    }

    /// <summary>Disparado a cada mudança de estado ou mensagem recebida.</summary>
    public event Action? Changed;

    public RealtimeConnectionState State { get; private set; } = RealtimeConnectionState.Connecting;

    /// <summary>Horário da última telemetria aceita (RF-031; D-73): do último lote recebido ou do retrato da Central.</summary>
    public DateTimeOffset? LastTelemetryAt { get; private set; }

    /// <summary>Depois de cada lote aceito, para todos os perfis.</summary>
    public event Action<TelemetryBatchProcessed>? TelemetryReceived;

    /// <summary>Posições de agentes e de viaturas; só chega a quem tem <c>agents.positions.view</c> (RN-27).</summary>
    public event Action<AgentPositionsUpdated>? AgentPositionsReceived;

    /// <summary>Uma escrita da operação mudou o que a Central mostra (D-80).</summary>
    public event Action<OperationsChanged>? OperationsChangedReceived;

    /// <summary>A conexão voltou depois de cair: quem mostra a Central busca um novo retrato (RF-042).</summary>
    public event Action? Reconnected;

    /// <summary>
    /// Avança o horário da última telemetria, nunca para trás: o retrato da Central o informa na abertura, antes do
    /// próximo lote.
    /// </summary>
    public void NoteTelemetry(DateTimeOffset? receivedAt)
    {
        if (receivedAt is { } value && (LastTelemetryAt is null || value > LastTelemetryAt))
        {
            LastTelemetryAt = value;
            Changed?.Invoke();
        }
    }

    /// <summary>Inicia a conexão sem bloquear a interface, se houver sessão. Chamadas repetidas são ignoradas.</summary>
    public void Start()
    {
        if (_sessionStore.Current is null || _session is not null)
        {
            return;
        }

        _session = new CancellationTokenSource();
        StartRestartLoop();
    }

    /// <summary>Encerra a conexão (logout ou fim da sessão).</summary>
    public async Task StopAsync()
    {
        if (_session is not { } session)
        {
            return;
        }

        // O laço da sessão anterior termina sozinho pelo cancelamento; uma nova sessão começa outro.
        _session = null;
        _restartLoop = null;
        await session.CancelAsync();
        session.Dispose();

        await _connection.StopAsync();
        LastTelemetryAt = null;
        _hasConnected = false;
        SetState(RealtimeConnectionState.Connecting);
    }

    private async Task OnClosedAsync(Exception? error)
    {
        // Parada pedida pelo logout: nada a fazer.
        if (_session is null)
        {
            return;
        }

        // O hub fecha a conexão quando o token vence (CloseOnAuthenticationExpiration).
        if (_sessionStore.Current is null)
        {
            await _sessionService.EndAsync(SessionEndReason.Expired);
            return;
        }

        SetState(RealtimeConnectionState.Interrupted);
        StartRestartLoop();
    }

    private void OnAuthenticationStateChanged(Task<AuthenticationState> stateTask) =>
        _ = HandleAuthenticationStateChangedAsync(stateTask);

    private async Task HandleAuthenticationStateChangedAsync(Task<AuthenticationState> stateTask)
    {
        var state = await stateTask;
        if (state.User.Identity?.IsAuthenticated == true)
        {
            Start();
        }
        else
        {
            await StopAsync();
        }
    }

    private void StartRestartLoop()
    {
        if (_session is not { } session || _restartLoop is { IsCompleted: false })
        {
            return;
        }

        _restartLoop = RestartUntilConnectedAsync(session.Token);
    }

    private async Task RestartUntilConnectedAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _connection.State == HubConnectionState.Disconnected)
        {
            try
            {
                await _connection.StartAsync(cancellationToken);
                SetState(RealtimeConnectionState.Connected);

                // A primeira conexão sem falha anterior não é "reconexão": a Central já busca o retrato ao abrir.
                var reconnected = _hasConnected || State == RealtimeConnectionState.Interrupted;
                _hasConnected = true;
                if (reconnected)
                {
                    Reconnected?.Invoke();
                }

                return;
            }
            catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
            {
                // Token recusado na negociação: a sessão acabou.
                await _sessionService.EndAsync(SessionEndReason.Expired);
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
        _authenticationStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
        if (_session is { } session)
        {
            _session = null;
            await session.CancelAsync();
            session.Dispose();
        }

        await _connection.DisposeAsync();
    }
}
