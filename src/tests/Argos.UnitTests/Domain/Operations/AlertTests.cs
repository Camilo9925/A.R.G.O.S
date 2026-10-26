using Argos.Domain.Common;
using Argos.Domain.Inventory.Equipments;
using Argos.Domain.Operations;
using Argos.Domain.Operations.Alerts;
using Argos.Domain.Telemetry;
using static Argos.UnitTests.Domain.Inventory.InventoryTestData;

namespace Argos.UnitTests.Domain.Operations;

/// <summary>Alertas da Central (US-15, RF-035, RF-036; RN-18 a RN-21, RN-23; D-77; plan 16.3).</summary>
public sealed class AlertTests
{
    private static readonly AlertDefinition Stolen = new(AlertCause.EquipmentStolen, AlertSeverity.Critical);

    private static readonly AlertDefinition Malfunction = new(AlertCause.EquipmentMalfunction, AlertSeverity.High);

    private static Alert Open(AlertDefinition definition) =>
        Alert.Open(definition, ResourceKind.Equipment, Guid.NewGuid(), Now, "operador");

    // RN-18 (parcial)

    public static TheoryData<EquipmentStatus, AlertCause?, AlertSeverity?> Destinations() => new()
    {
        { EquipmentStatus.NotInstalled, null, null },
        { EquipmentStatus.Operational, null, null },
        { EquipmentStatus.Malfunction, AlertCause.EquipmentMalfunction, AlertSeverity.High },
        { EquipmentStatus.UnderMaintenance, null, null },
        { EquipmentStatus.Deactivated, null, null },
        { EquipmentStatus.Stolen, AlertCause.EquipmentStolen, AlertSeverity.Critical },
    };

    [Theory]
    [MemberData(nameof(Destinations))]
    public void Catalog_OnlyStolenAndMalfunctionRaiseAlerts_RN18(EquipmentStatus to, AlertCause? cause, AlertSeverity? severity)
    {
        var from = to == EquipmentStatus.Operational ? EquipmentStatus.Malfunction : EquipmentStatus.Operational;

        var definition = AlertCatalog.ForStatusChange(from, to);

        Assert.Equal(cause, definition?.Cause);
        Assert.Equal(severity, definition?.Severity);
        Assert.Equal(cause, AlertCatalog.ForStatusChange(null, to)?.Cause);
    }

    [Fact]
    public void Catalog_SameStatus_RaisesNothing_RN18()
    {
        Assert.Null(AlertCatalog.ForStatusChange(EquipmentStatus.Malfunction, EquipmentStatus.Malfunction));
    }

    [Fact]
    public void Open_StartsOpenWithOneOccurrence_RN18()
    {
        var resourceId = Guid.NewGuid();

        var alert = Alert.Open(Stolen, ResourceKind.Equipment, resourceId, Now, " operador ");

        Assert.Equal(
            (AlertCause.EquipmentStolen, AlertSeverity.Critical, AlertState.Open, resourceId, "operador", Now, Now, 1),
            (alert.Cause, alert.Severity, alert.State, alert.ResourceId, alert.OpenedBy, alert.OpenedAt, alert.LastOccurredAt, alert.OccurrenceCount));
        Assert.True(alert.IsOpen);
    }

    // RN-19

    [Fact]
    public void Recur_AddsOccurrenceAndAdvancesLastOccurrence_RN19()
    {
        var alert = Open(Malfunction);
        alert.Acknowledge(Now.AddMinutes(1), "operador");

        alert.Recur(Now.AddMinutes(10));
        alert.Recur(Now.AddMinutes(5));

        Assert.Equal(3, alert.OccurrenceCount);
        Assert.Equal(Now.AddMinutes(10), alert.LastOccurredAt);
        Assert.Equal(AlertState.Acknowledged, alert.State);
    }

    [Fact]
    public void Recur_OnResolved_IsRefused_RN19()
    {
        var alert = Open(Malfunction);
        alert.Resolve("Trocado o sensor.", Now, "operador");

        var exception = Assert.Throws<OperationsRuleException>(() => alert.Recur(Now.AddMinutes(1)));

        Assert.Equal("O alerta já foi resolvido.", exception.Message);
    }

    // RN-20

    [Fact]
    public void Acknowledge_RecordsWhoAndWhen_RN20()
    {
        var alert = Open(Stolen);

        alert.Acknowledge(Now.AddMinutes(2), "operador2");

        Assert.Equal((AlertState.Acknowledged, (DateTimeOffset?)Now.AddMinutes(2), "operador2"),
            (alert.State, alert.AcknowledgedAt, alert.AcknowledgedBy));
    }

    [Fact]
    public void Acknowledge_Twice_IsRefusedWithMessage_RN20()
    {
        var alert = Open(Stolen);
        alert.Acknowledge(Now, "operador");

        var exception = Assert.Throws<OperationsRuleException>(() => alert.Acknowledge(Now.AddMinutes(1), "outro"));

        Assert.Equal("O alerta já foi reconhecido.", exception.Message);
        Assert.Equal("operador", alert.AcknowledgedBy);
    }

    [Fact]
    public void AcknowledgeOrResolve_Resolved_IsRefusedWithMessage_RN20()
    {
        var alert = Open(Malfunction);
        alert.Resolve("Reparado.", Now, "operador");

        var acknowledge = Assert.Throws<OperationsRuleException>(() => alert.Acknowledge(Now, "operador"));
        var resolve = Assert.Throws<OperationsRuleException>(() => alert.Resolve("De novo.", Now, "operador"));

        Assert.Equal("O alerta já foi resolvido.", acknowledge.Message);
        Assert.Equal("O alerta já foi resolvido.", resolve.Message);
        Assert.Equal("Reparado.", alert.ResolutionNotes);
    }

    [Fact]
    public void Resolve_CriticalNotAcknowledged_IsRefusedWithMessage_RN20()
    {
        var alert = Open(Stolen);

        var exception = Assert.Throws<OperationsRuleException>(() => alert.Resolve("Recuperada.", Now, "operador"));

        Assert.Equal("Alertas críticos precisam ser reconhecidos antes de serem resolvidos.", exception.Message);
        Assert.Equal(AlertState.Open, alert.State);
    }

    [Fact]
    public void Resolve_CriticalAfterAcknowledgement_And_NotCriticalDirectly_RN20()
    {
        var critical = Open(Stolen);
        critical.Acknowledge(Now, "operador");
        critical.Resolve("  Recuperada pela PM.  ", Now.AddHours(1), "operador");

        var high = Open(Malfunction);
        high.Resolve("Reparado.", Now.AddHours(1), "gestor");

        Assert.Equal(
            (AlertState.Resolved, (DateTimeOffset?)Now.AddHours(1), "operador", "Recuperada pela PM."),
            (critical.State, critical.ResolvedAt, critical.ResolvedBy, critical.ResolutionNotes));
        Assert.Equal(AlertState.Resolved, high.State);
        Assert.False(high.IsOpen);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_WithoutNotes_IsRefusedOnTheField_RN20(string? notes)
    {
        var alert = Open(Malfunction);

        var exception = Assert.Throws<DomainValidationException>(() => alert.Resolve(notes, Now, "operador"));

        Assert.Equal(["Informe a observação da resolução."], exception.Errors["notes"]);
        Assert.Equal(AlertState.Open, alert.State);
    }

    [Fact]
    public void Resolve_NotesUpTo500Characters_RN20()
    {
        var tooLong = Open(Malfunction);
        var exception = Assert.Throws<DomainValidationException>(() => tooLong.Resolve(new string('x', 501), Now, "operador"));

        var limit = Open(Malfunction);
        limit.Resolve(new string('x', 500), Now, "operador");

        Assert.Equal(["A observação deve ter no máximo 500 caracteres."], exception.Errors["notes"]);
        Assert.Equal(AlertState.Resolved, limit.State);
    }

    public static TheoryData<AlertState, AlertSeverity, AlertAction[]> ActionsByStateAndSeverity() => new()
    {
        { AlertState.Open, AlertSeverity.Critical, [AlertAction.Acknowledge] },
        { AlertState.Open, AlertSeverity.High, [AlertAction.Acknowledge, AlertAction.Resolve] },
        { AlertState.Open, AlertSeverity.Low, [AlertAction.Acknowledge, AlertAction.Resolve] },
        { AlertState.Acknowledged, AlertSeverity.Critical, [AlertAction.Resolve] },
        { AlertState.Acknowledged, AlertSeverity.High, [AlertAction.Resolve] },
        { AlertState.Resolved, AlertSeverity.Critical, [] },
        { AlertState.Resolved, AlertSeverity.High, [] },
    };

    [Theory]
    [MemberData(nameof(ActionsByStateAndSeverity))]
    public void AllowedActions_ByStateAndSeverity_RN20(AlertState state, AlertSeverity severity, AlertAction[] expected)
    {
        Assert.Equal(expected, Alert.ActionsFor(state, severity));
    }

    [Fact]
    public void AllowedActions_FollowTheAggregate_RN20()
    {
        var alert = Open(Stolen);
        Assert.Equal([AlertAction.Acknowledge], alert.AllowedActions);

        alert.Acknowledge(Now, "operador");
        Assert.Equal([AlertAction.Resolve], alert.AllowedActions);

        alert.Resolve("Recuperada.", Now, "operador");
        Assert.Empty(alert.AllowedActions);
    }

    [Fact]
    public void Alert_HasNoDeletionOperation_RN23()
    {
        var methods = typeof(Alert).GetMethods().Select(method => method.Name);

        Assert.DoesNotContain(methods, name =>
            name.Contains("Delete", StringComparison.Ordinal) || name.Contains("Remove", StringComparison.Ordinal));
    }

    // RN-21

    [Theory]
    [InlineData(true, AlertSeverity.Critical, AlertListing.Listed)]
    [InlineData(true, AlertSeverity.High, AlertListing.Listed)]
    [InlineData(false, AlertSeverity.Critical, AlertListing.ListedOutsideFilter)]
    [InlineData(false, AlertSeverity.High, AlertListing.Hidden)]
    [InlineData(false, AlertSeverity.Low, AlertListing.Hidden)]
    public void ListPolicy_CriticalAlwaysListed_OthersOnlyInsideTheFilter_RN21(
        bool matchesFilter, AlertSeverity severity, AlertListing expected)
    {
        Assert.Equal(expected, AlertListPolicy.Include(matchesFilter, severity));
    }

    [Fact]
    public void ListPolicy_OrdersBySeverityThenMostRecentThenId_RF035()
    {
        var first = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var items = new[]
        {
            (Name: "alta antiga", Severity: AlertSeverity.High, Last: Now, Id: first),
            (Name: "crítica antiga", Severity: AlertSeverity.Critical, Last: Now, Id: first),
            (Name: "alta recente 2", Severity: AlertSeverity.High, Last: Now.AddMinutes(5), Id: second),
            (Name: "alta recente 1", Severity: AlertSeverity.High, Last: Now.AddMinutes(5), Id: first),
            (Name: "crítica recente", Severity: AlertSeverity.Critical, Last: Now.AddMinutes(1), Id: second),
        };

        var ordered = AlertListPolicy.Order(items, item => item.Severity, item => item.Last, item => item.Id).Select(item => item.Name);

        Assert.Equal(["crítica recente", "crítica antiga", "alta recente 1", "alta recente 2", "alta antiga"], ordered);
    }
}
