using Argos.Application.Common;
using Argos.Contracts.Operations;
using Argos.Domain.Operations.Alerts;
using DomainAlertCause = Argos.Domain.Operations.Alerts.AlertCause;
using DomainAlertSeverity = Argos.Domain.Operations.Alerts.AlertSeverity;

namespace Argos.Application.Operations;

/// <summary>Conversões das linhas da Central para os contratos (plan 16.10), com as regras de exibição por perfil.</summary>
internal static class OperationsMapping
{
    /// <summary>
    /// Posição de um recurso no mapa (plan 16.7): fixo na localização cadastral; móvel na posição atual, se houver,
    /// senão na base. RN-27 (A-38): viatura e moto (<c>CrewAssignable</c>) ficam sempre na base para quem não vê a
    /// posição de agentes, porque a posição delas revelaria a da tripulação.
    /// </summary>
    public static (GeoPosition Position, PositionSource Source, DateTimeOffset? RecordedAt) MapPosition(
        bool isFixed, bool crewAssignable, GeoPosition mapPosition, GeoPosition? livePosition, DateTimeOffset? liveRecordedAt, bool canSeeAgents)
    {
        if (isFixed)
        {
            return (mapPosition, PositionSource.Installed, null);
        }

        return livePosition is { } live && (!crewAssignable || canSeeAgents)
            ? (live, PositionSource.Live, liveRecordedAt)
            : (mapPosition, PositionSource.Base, null);
    }

    public static string TitleOf(DomainAlertCause cause) => cause switch
    {
        DomainAlertCause.EquipmentStolen => "Equipamento furtado",
        DomainAlertCause.EquipmentMalfunction => "Equipamento em mau funcionamento",
        _ => throw new ArgumentOutOfRangeException(nameof(cause), cause, "Causa desconhecida."),
    };

    /// <summary>
    /// Resumo do alerta (plan 16.10). As ações vêm do Domínio (RN-20) e saem vazias para quem não tem
    /// <c>alerts.handle</c> (D-59).
    /// </summary>
    public static AlertSummary ToSummary(AlertRow row, bool outsideFilter, bool canHandleAlerts, bool canSeeAgents)
    {
        var (position, _, _) = MapPosition(
            isFixed: false, row.CrewAssignable, row.MapPosition, row.LivePosition, null, canSeeAgents);

        return new AlertSummary
        {
            Id = row.Id,
            Cause = EnumNames.Convert<Contracts.Operations.AlertCause>(row.Cause),
            Severity = EnumNames.Convert<Contracts.Operations.AlertSeverity>(row.Severity),
            State = EnumNames.Convert<Contracts.Operations.AlertState>(row.State),
            Title = TitleOf(row.Cause),
            ResourceKind = EnumNames.Convert<Contracts.Operations.ResourceKind>(row.ResourceKind),
            ResourceId = row.ResourceId,
            ResourceLabel = $"{row.TypeName} · {row.SerialNumber}",
            ResourceIsActive = row.ResourceIsActive,
            NeighborhoodName = row.NeighborhoodName,
            RegionName = row.RegionName,
            Latitude = position.Latitude,
            Longitude = position.Longitude,
            OpenedAt = row.OpenedAt,
            LastOccurredAt = row.LastOccurredAt,
            OccurrenceCount = row.OccurrenceCount,
            AcknowledgedAt = row.AcknowledgedAt,
            AcknowledgedBy = row.AcknowledgedBy,
            OutsideFilter = outsideFilter,
            AllowedActions = canHandleAlerts
                ? Alert.ActionsFor(row.State, row.Severity)
                    .Select(action => EnumNames.Convert<Contracts.Operations.AlertAction>(action))
                    .ToArray()
                : [],
        };
    }

    /// <summary>Ordem da lista (RF-035), pela política do Domínio.</summary>
    public static IEnumerable<AlertRow> Ordered(IEnumerable<AlertRow> rows) =>
        AlertListPolicy.Order(rows, row => row.Severity, row => row.LastOccurredAt, row => row.Id);

    public static IReadOnlyList<DomainAlertSeverity> AllSeverities { get; } = Enum.GetValues<DomainAlertSeverity>();
}
