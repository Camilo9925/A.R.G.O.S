using Argos.Contracts.Inventory;
using Argos.Contracts.Operations;
using Argos.Web.Interop;
using Argos.Web.Services;
using Argos.Web.Theme;

namespace Argos.Web.Components.Operations;

/// <summary>
/// Monta os marcadores do mapa a partir do retrato (plan 16.12). Só aparência: ícone do tipo ou da função, situação em
/// cor + ícone e contorno tracejado para "Ainda não instalado" (Art. X.2, XI.2). Quem decide o que aparece é a API.
/// </summary>
public static class OperationsMarkers
{
    public const string EquipmentPrefix = "e:";
    public const string AgentPrefix = "a:";

    public static string EquipmentKey(Guid id) => EquipmentPrefix + id;

    public static string AgentKey(Guid id) => AgentPrefix + id;

    public static IReadOnlyList<MapMarker> From(OperationsSnapshot snapshot) =>
    [
        .. snapshot.Equipment.Select(ForEquipment),
        .. snapshot.Agents.Select(ForAgent),
    ];

    private static MapMarker ForEquipment(EquipmentMarker equipment)
    {
        var status = StatusTokens.For(equipment.Status);
        var alert = equipment.OpenAlertSeverity is { } severity ? AlertSeverityTokens.For(severity) : null;
        var need = HighlightedNeed(equipment.MaintenanceNeed);
        var classes = "argos-poi argos-poi--equipment"
            + (status.IsOutlined ? " argos-poi--outlined" : string.Empty)
            + (alert is not null ? " argos-poi--alert" : string.Empty);
        var style = $"--argos-marker-color:{status.Color}"
            + (alert is not null ? $";--argos-alert-color:{alert.Color}" : string.Empty)
            + (need is not null ? $";--argos-need-color:{need.Color}" : string.Empty);
        var badge = Svg(status.Icon, 12);

        // US-16: selo quadrado no canto superior esquerdo, distinto do contorno redondo do alerta e do selo da situação.
        var needBadge = need is not null
            ? $"<span class=\"argos-poi-need\">{Svg(need.Icon, 11)}</span>"
            : string.Empty;

        var html = $"<div class=\"{classes}\" style=\"{style}\">{Svg(InventoryLabels.IconFor(equipment.TypeIcon), 20)}"
            + $"<span class=\"argos-poi-badge\">{badge}</span>{needBadge}</div>";

        var label = $"{equipment.TypeName} {equipment.SerialNumber}, {status.Label}"
            + (alert is not null ? $", alerta {alert.Label.ToLowerInvariant()} aberto" : string.Empty)
            + (need is not null ? $", manutenção {need.Label.ToLowerInvariant()}" : string.Empty);

        return new MapMarker(
            EquipmentKey(equipment.Id), equipment.Latitude, equipment.Longitude, html, status.Color,
            equipment.SeverityRank, badge, status.Label, label);
    }

    /// <summary>
    /// US-16 (parcial): só Imediata, Vencida e Próxima ganham destaque no marcador; Programada e Nenhuma ficam sem selo para
    /// não poluir o mapa. A necessidade vem pronta da API (D-54).
    /// </summary>
    public static StatusToken? HighlightedNeed(MaintenanceNeed need) =>
        need is MaintenanceNeed.Immediate or MaintenanceNeed.Overdue or MaintenanceNeed.Upcoming
            ? MaintenanceNeedTokens.For(need)
            : null;

    private static MapMarker ForAgent(AgentMarker agent)
    {
        var role = AgentRoleTokens.For(agent.Role);
        var status = AgentStatusTokens.For(agent.Status);
        var badge = Svg(status.Icon, 12);

        var html = $"<div class=\"argos-poi argos-poi--agent\" style=\"--argos-marker-color:{status.Color}\">{Svg(role.Icon, 20)}"
            + $"<span class=\"argos-poi-badge\">{badge}</span></div>";

        return new MapMarker(
            AgentKey(agent.Id), agent.Latitude, agent.Longitude, html, status.Color, agent.SeverityRank, badge, status.Label,
            $"{agent.DisplayName}, {role.Label}, {status.Label}");
    }

    /// <summary>Os ícones do MudBlazor são o miolo de um SVG 24x24; a cor vem do <c>color</c> do contêiner.</summary>
    private static string Svg(string icon, int size) =>
        $"<svg viewBox=\"0 0 24 24\" width=\"{size}\" height=\"{size}\" fill=\"currentColor\" aria-hidden=\"true\" focusable=\"false\">{icon}</svg>";
}
