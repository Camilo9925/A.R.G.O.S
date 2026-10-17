using System.Net;
using System.Net.Http.Json;
using Argos.Application.Common.Abstractions;
using Argos.Contracts.Access;
using Argos.Contracts.Realtime;
using Argos.IntegrationTests.Diagnostics;
using Argos.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Argos.IntegrationTests.Realtime;

/// <summary>
/// Hub da Central (plan 4.2, 14.8): autenticação na negociação, grupos por perfil e público das mensagens
/// (RN-27; Art. VIII.2; D-10, D-38).
/// </summary>
public sealed class OperationsHubTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    private const string NegotiatePath = OperationsHubContract.Path + "/negotiate?negotiateVersion=1";

    // Mensagem de sondagem, só deste teste; o conteúdo diz para quem ela foi publicada.
    private const string AudienceProbeMessage = "AudienceProbe";
    private const string RestrictedProbe = "agents.positions.view";
    private const string EveryoneProbe = "todos";

    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Negotiate_WithoutToken_ReturnsUnauthorizedProblem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = Fixture.Factory.CreateClient();

        using var response = await client.PostAsync(NegotiatePath, content: null, cancellationToken);

        await ProblemAssert.IsUnauthenticatedAsync(response, cancellationToken);
    }

    [Fact]
    public async Task Negotiate_WithExpiredToken_ReturnsUnauthorizedProblem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = Fixture.Factory.CreateClient();

        // Como na Web: o token vai em access_token, mecanismo do SignalR para WebSockets (D-39).
        var path = $"{NegotiatePath}&access_token={Fixture.Tokens.ExpiredFor(UserRoles.Operator)}";
        using var response = await client.PostAsync(path, content: null, cancellationToken);

        await ProblemAssert.IsUnauthenticatedAsync(response, cancellationToken);
    }

    [Fact]
    public async Task Negotiate_WithSystemKey_ReturnsForbiddenProblem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = Fixture.CreateSystemClient();

        using var response = await client.PostAsync(NegotiatePath, content: null, cancellationToken);

        await ProblemAssert.IsForbiddenAsync(response, cancellationToken);
    }

    /// <summary>
    /// RN-27: a posição individual de agentes só chega a Administrador e Operador, nunca ao Gestor. A Aplicação publica
    /// primeiro a mensagem restrita a <c>agents.positions.view</c> e depois uma para todos. O SignalR mantém a ordem
    /// por conexão, então, quando a mensagem para todos chega, a restrita já teria chegado antes dela: o Gestor
    /// recebe só a segunda, sem depender de tempo de espera (plan 14.12).
    /// </summary>
    [Fact]
    public async Task Publish_RestrictedToAgentPositions_ReachesAdministratorAndOperatorButNeverManager_RN27()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var administrator = await ConnectRecorderAsync(UserRoles.Administrator, cancellationToken);
        await using var @operator = await ConnectRecorderAsync(UserRoles.Operator, cancellationToken);
        await using var manager = await ConnectRecorderAsync(UserRoles.Manager, cancellationToken);

        var notifier = Fixture.Factory.Services.GetRequiredService<IRealtimeNotifier>();
        await notifier.PublishAsync(
            RealtimeAudience.WithPermission(Permissions.AgentPositionsView), AudienceProbeMessage, RestrictedProbe, cancellationToken);
        await notifier.PublishAsync(RealtimeAudience.Everyone, AudienceProbeMessage, EveryoneProbe, cancellationToken);

        Assert.Equal([RestrictedProbe, EveryoneProbe], await administrator.UntilEveryoneProbeAsync(cancellationToken));
        Assert.Equal([RestrictedProbe, EveryoneProbe], await @operator.UntilEveryoneProbeAsync(cancellationToken));
        Assert.Equal([EveryoneProbe], await manager.UntilEveryoneProbeAsync(cancellationToken));
    }

    [Fact]
    public async Task TechnicalHeartbeat_ReachesEveryRole()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connections = new List<HubConnection>();
        var deliveries = new List<Task<TechnicalHeartbeatNotification>>();

        try
        {
            foreach (var role in UserRoles.All)
            {
                var received = new TaskCompletionSource<TechnicalHeartbeatNotification>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var connection = Fixture.CreateHubConnection(Fixture.Tokens.For(role));
                connections.Add(connection);
                connection.On<TechnicalHeartbeatNotification>(
                    OperationsHubContract.Messages.TechnicalHeartbeatReceived,
                    notification => received.TrySetResult(notification));
                await connection.StartAndSynchronizeAsync(cancellationToken);
                deliveries.Add(received.Task);
            }

            var heartbeat = TechnicalHeartbeatsTests.NewHeartbeat();
            using var client = Fixture.CreateSystemClient();
            using var response = await client.PostAsJsonAsync(TechnicalHeartbeatsTests.HeartbeatsPath, heartbeat, cancellationToken);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

            var notifications = await Task.WhenAll(deliveries).WaitAsync(DeliveryTimeout, cancellationToken);
            Assert.All(notifications, notification => Assert.Equal(heartbeat.Sequence, notification.Sequence));
        }
        finally
        {
            foreach (var connection in connections)
            {
                await connection.DisposeAsync();
            }
        }
    }

    private async Task<AudienceRecorder> ConnectRecorderAsync(string role, CancellationToken cancellationToken)
    {
        var recorder = new AudienceRecorder(Fixture.CreateHubConnection(Fixture.Tokens.For(role)));
        await recorder.Connection.StartAndSynchronizeAsync(cancellationToken);
        return recorder;
    }

    /// <summary>Registra, em ordem, as mensagens de sondagem recebidas por uma conexão.</summary>
    private sealed class AudienceRecorder : IAsyncDisposable
    {
        private readonly List<string> _received = [];
        private readonly TaskCompletionSource _everyoneProbeReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public AudienceRecorder(HubConnection connection)
        {
            Connection = connection;
            connection.On<string>(AudienceProbeMessage, probe =>
            {
                lock (_received)
                {
                    _received.Add(probe);
                }

                if (probe == EveryoneProbe)
                {
                    _everyoneProbeReceived.TrySetResult();
                }
            });
        }

        public HubConnection Connection { get; }

        /// <summary>Espera a mensagem para todos e devolve tudo o que chegou até ela.</summary>
        public async Task<string[]> UntilEveryoneProbeAsync(CancellationToken cancellationToken)
        {
            await _everyoneProbeReceived.Task.WaitAsync(DeliveryTimeout, cancellationToken);
            lock (_received)
            {
                return [.. _received];
            }
        }

        public ValueTask DisposeAsync() => Connection.DisposeAsync();
    }
}
