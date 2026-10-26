using Argos.Domain.Common;
using Argos.Domain.Inventory;
using Argos.Domain.Inventory.Agents;
using Argos.Domain.Telemetry;
using static Argos.UnitTests.Domain.Inventory.AgentTestData;
using static Argos.UnitTests.Domain.Inventory.InventoryTestData;

namespace Argos.UnitTests.Domain.Telemetry;

/// <summary>
/// Telemetria mínima e situação reportada (RF-007, RF-015, RF-023, RF-024; RN-12, RN-13; D-70, D-71; plan 16.2, 16.3).
/// </summary>
public sealed class TelemetryTests
{
    private static readonly DateTimeOffset Received = Now.AddSeconds(2);

    // D-71: o estado atual só avança.

    [Fact]
    public void LiveState_NewerPosition_Advances_D71()
    {
        var state = ResourceLiveState.FromPosition(ResourceKind.Equipment, Guid.NewGuid(), -22.20, -49.65, Now, Received);

        var advanced = state.ApplyPosition(-22.21, -49.66, Now.AddSeconds(5), Received.AddSeconds(5));

        Assert.True(advanced);
        Assert.Equal((-22.21, -49.66), (state.Latitude!.Value, state.Longitude!.Value));
        Assert.Equal(Now.AddSeconds(5), state.PositionRecordedAt);
        Assert.Equal(Now.AddSeconds(5), state.LastCommunicationAt);
        Assert.Equal(Received.AddSeconds(5), state.UpdatedAt);
    }

    [Fact]
    public void LiveState_OlderPosition_DoesNotChangeAnything_D71()
    {
        var state = ResourceLiveState.FromPosition(ResourceKind.Agent, Guid.NewGuid(), -22.20, -49.65, Now, Received);

        var advanced = state.ApplyPosition(-22.30, -49.70, Now.AddSeconds(-5), Received.AddSeconds(5));

        Assert.False(advanced);
        Assert.Equal((-22.20, -49.65, Now, Now, Received),
            (state.Latitude!.Value, state.Longitude!.Value, state.PositionRecordedAt!.Value, state.LastCommunicationAt, state.UpdatedAt));
    }

    [Fact]
    public void LiveState_Heartbeat_AdvancesOnlyTheLastCommunication_D71()
    {
        var state = ResourceLiveState.FromHeartbeat(ResourceKind.Equipment, Guid.NewGuid(), Now, Received);

        Assert.True(state.ApplyHeartbeat(Now.AddMinutes(1), Received.AddMinutes(1)));
        Assert.False(state.ApplyHeartbeat(Now.AddSeconds(30), Received.AddMinutes(2)));

        Assert.Null(state.PositionRecordedAt);
        Assert.Null(state.Latitude);
        Assert.Equal(Now.AddMinutes(1), state.LastCommunicationAt);
        Assert.Equal(Received.AddMinutes(1), state.UpdatedAt);
    }

    [Theory]
    [InlineData(-90.01, 0, "latitude", "A latitude deve estar entre -90 e 90.")]
    [InlineData(90.01, 0, "latitude", "A latitude deve estar entre -90 e 90.")]
    [InlineData(0, -180.01, "longitude", "A longitude deve estar entre -180 e 180.")]
    [InlineData(0, 180.01, "longitude", "A longitude deve estar entre -180 e 180.")]
    [InlineData(double.NaN, 0, "latitude", "A latitude deve estar entre -90 e 90.")]
    public void InvalidCoordinates_AreRefused_D71(double latitude, double longitude, string field, string message)
    {
        var state = ResourceLiveState.FromHeartbeat(ResourceKind.Equipment, Guid.NewGuid(), Now, Received);

        var onState = Assert.Throws<DomainValidationException>(
            () => state.ApplyPosition(latitude, longitude, Now.AddSeconds(5), Received));
        var onReading = Assert.Throws<DomainValidationException>(() => TelemetryReading.Position(
            ResourceKind.Equipment, Guid.NewGuid(), latitude, longitude, Now, Received, isSimulated: true, "PROMETHEUS"));

        Assert.Equal([message], onState.Errors[field]);
        Assert.Equal([message], onReading.Errors[field]);
        Assert.Null(state.PositionRecordedAt);
    }

    [Fact]
    public void LiveState_LimitCoordinates_AreAccepted_D71()
    {
        var state = ResourceLiveState.FromPosition(ResourceKind.Equipment, Guid.NewGuid(), -90, 180, Now, Received);

        Assert.Equal((-90d, 180d), (state.Latitude!.Value, state.Longitude!.Value));
    }

    [Fact]
    public void Readings_PositionHasCoordinatesAndHeartbeatDoesNot_ArtV1()
    {
        var id = Guid.NewGuid();
        var position = TelemetryReading.Position(ResourceKind.Agent, id, -22.2, -49.6, Now, Received, isSimulated: true, " PROMETHEUS ");
        var heartbeat = TelemetryReading.Heartbeat(ResourceKind.Equipment, id, Now, Received, isSimulated: true, "PROMETHEUS");

        Assert.IsAssignableFrom<IAppendOnly>(position);
        Assert.Equal((TelemetryReadingKind.Position, "PROMETHEUS", true), (position.Kind, position.Author, position.IsSimulated));
        Assert.Equal((-22.2, -49.6), (position.Latitude!.Value, position.Longitude!.Value));
        Assert.Equal(TelemetryReadingKind.Heartbeat, heartbeat.Kind);
        Assert.Null(heartbeat.Latitude);
        Assert.Null(heartbeat.Longitude);
        Assert.Equal((Now, Received), (heartbeat.RecordedAt, heartbeat.ReceivedAt));
    }

    // RN-12: situação reportada pelo PROMETHEUS.

    [Fact]
    public void ReportedStatus_IsRecordedWithTelemetryOriginAndPrometheusAuthor_RN12()
    {
        var agent = NewAgent();
        var reportedAt = Now.AddHours(1);

        agent.ChangeStatus(AgentStatus.OnDuty, reportedAt, "PROMETHEUS", AgentStatusChangeOrigin.Telemetry);

        var change = agent.StatusChanges.Last();
        Assert.Equal(AgentStatus.OnDuty, agent.Status);
        Assert.Equal(
            ((AgentStatus?)AgentStatus.OffDuty, AgentStatus.OnDuty, AgentStatusChangeOrigin.Telemetry, "PROMETHEUS", reportedAt),
            (change.FromStatus, change.ToStatus, change.Origin, change.Author, change.OccurredAt));
    }

    [Fact]
    public void ReportedStatus_SameAsCurrent_IsRefused_RN12()
    {
        var agent = NewAgent();

        var exception = Assert.Throws<InventoryRuleException>(
            () => agent.ChangeStatus(AgentStatus.OffDuty, Now, "PROMETHEUS", AgentStatusChangeOrigin.Telemetry));

        Assert.Equal("O agente já está nessa situação.", exception.Message);
    }

    [Fact]
    public void ChangeStatus_WithoutOrigin_IsManual_AndRegistrationOriginIsRefused_RN12()
    {
        var agent = NewAgent();

        agent.ChangeStatus(AgentStatus.OnDuty, Now, "operador");

        Assert.Equal(AgentStatusChangeOrigin.Manual, agent.StatusChanges.Last().Origin);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => agent.ChangeStatus(AgentStatus.OnBreak, Now, "operador", AgentStatusChangeOrigin.Registration));
    }

    // RN-13: em atividade = Em serviço ou Em atendimento.

    [Theory]
    [InlineData(AgentStatus.OnDuty, true)]
    [InlineData(AgentStatus.Responding, true)]
    [InlineData(AgentStatus.OnBreak, false)]
    [InlineData(AgentStatus.OffDuty, false)]
    [InlineData(AgentStatus.OnLeave, false)]
    public void InActivity_OnlyOnDutyAndResponding_RN13(AgentStatus status, bool expected)
    {
        Assert.Equal(expected, AgentActivity.IsInActivity(status));
    }
}
