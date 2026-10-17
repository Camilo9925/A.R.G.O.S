using System.Net;
using System.Net.Http.Json;
using Argos.Contracts.Realtime;
using Argos.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Argos.IntegrationTests.Diagnostics;

/// <summary>
/// Caminho feliz do endpoint de diagnóstico (Art. XII.3): o sinal de vida aceito pela API chega ao cliente do hub
/// (plan 4.4: Simulador → API → Aplicação → SignalR → Web).
/// </summary>
public sealed class TechnicalHeartbeatsTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    private const string HeartbeatsPath = "/api/diagnostics/heartbeats";
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Post_ValidHeartbeat_ReturnsAcceptedAndNotifiesHubClients()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var received = new TaskCompletionSource<TechnicalHeartbeatNotification>(TaskCreationOptions.RunContinuationsAsynchronously);

        // O handler do TestServer não aceita WebSockets; o long polling exercita o mesmo hub.
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(Fixture.Factory.Server.BaseAddress, OperationsHubContract.Path), options =>
            {
                options.HttpMessageHandlerFactory = _ => Fixture.Factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
        connection.On<TechnicalHeartbeatNotification>(
            OperationsHubContract.Messages.TechnicalHeartbeatReceived,
            notification => received.TrySetResult(notification));
        await connection.StartAsync(cancellationToken);

        var heartbeat = new TechnicalHeartbeat
        {
            Source = "integration-test",
            Sequence = 7,
            SentAt = new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero),
        };

        using var client = Fixture.Factory.CreateClient();
        using var response = await client.PostAsJsonAsync(HeartbeatsPath, heartbeat, cancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var notification = await received.Task.WaitAsync(DeliveryTimeout, cancellationToken);
        Assert.Equal(heartbeat.Source, notification.Source);
        Assert.Equal(heartbeat.Sequence, notification.Sequence);
        Assert.Equal(heartbeat.SentAt, notification.SentAt);
        Assert.Equal(TimeSpan.Zero, notification.ReceivedAt.Offset);
    }
}
