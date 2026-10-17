using System.Net;
using System.Net.Http.Json;
using Argos.Contracts.Access;
using Argos.Contracts.Realtime;
using Argos.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.SignalR.Client;

namespace Argos.IntegrationTests.Diagnostics;

/// <summary>
/// Caminho feliz do endpoint de diagnóstico (Art. XII.3): o sinal de vida aceito pela API chega ao cliente do hub
/// (plan 4.4: Simulador → API → Aplicação → SignalR → Web). O Simulador envia com a chave de sistema (D-34) e o cliente
/// do hub conecta com o token de um usuário (D-35).
/// </summary>
public sealed class TechnicalHeartbeatsTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    public const string HeartbeatsPath = "/api/diagnostics/heartbeats";
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Post_ValidHeartbeat_ReturnsAcceptedAndNotifiesHubClients()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var received = new TaskCompletionSource<TechnicalHeartbeatNotification>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var connection = Fixture.CreateHubConnection(Fixture.Tokens.For(UserRoles.Operator));
        connection.On<TechnicalHeartbeatNotification>(
            OperationsHubContract.Messages.TechnicalHeartbeatReceived,
            notification => received.TrySetResult(notification));
        await connection.StartAndSynchronizeAsync(cancellationToken);

        var heartbeat = NewHeartbeat();

        using var client = Fixture.CreateSystemClient();
        using var response = await client.PostAsJsonAsync(HeartbeatsPath, heartbeat, cancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var notification = await received.Task.WaitAsync(DeliveryTimeout, cancellationToken);
        Assert.Equal(heartbeat.Source, notification.Source);
        Assert.Equal(heartbeat.Sequence, notification.Sequence);
        Assert.Equal(heartbeat.SentAt, notification.SentAt);
        Assert.Equal(TimeSpan.Zero, notification.ReceivedAt.Offset);
    }

    public static TechnicalHeartbeat NewHeartbeat() => new()
    {
        Source = "integration-test",
        Sequence = 7,
        SentAt = new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero),
    };
}
