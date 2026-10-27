using Argos.Contracts.Inventory;
using Argos.Contracts.Operations;
using Argos.Web.Interop;
using Argos.Web.Theme;

namespace Argos.Web.Components.Operations;

/// <summary>
/// Situação de uma região no recorte atual da Central (US-17, RF-039, RN-24). Os números são os dos equipamentos e dos
/// alertas que o retrato traz, ou seja, já refletem os filtros ativos.
/// </summary>
/// <param name="AvailabilityPercent">RN-14 sobre os equipamentos da região; nula sem equipamento que deveria estar operando.</param>
/// <param name="Unavailable">Mau funcionamento + Em manutenção + Furtado (RN-15).</param>
/// <param name="OpenAlerts">Alertas abertos da região que atendem ao filtro (RN-21).</param>
public sealed record RegionSituation(
    Guid RegionId,
    string RegionName,
    RegionClassification Classification,
    decimal? AvailabilityPercent,
    int TotalEquipment,
    int Unavailable,
    int OpenAlerts,
    int CriticalAlerts,
    int AgentsInActivity);

/// <summary>
/// Quadro "Situação por região" (US-17, RF-039, RN-24; plan 16.12), calculado no cliente a partir do retrato (marcadores
/// de equipamento, alertas e agentes por região): a API ainda não devolve a classificação por região. A Web só
/// referencia os Contratos (Art. III.6), por isso a fórmula de disponibilidade replica a de
/// <c>Argos.Domain.Operations.AvailabilityCalculator</c> (RN-14, RN-15):
/// <c>Operacionais ÷ (Total − Ainda não instalados − Desativados) × 100</c>, a uma casa, com o meio para cima.
/// </summary>
public static class RegionSituations
{
    /// <summary>RN-24: abaixo disto a região é Crítica.</summary>
    public const decimal CriticalBelow = 75m;

    /// <summary>RN-24: a partir disto, e sem alerta crítico aberto, a região é Normal.</summary>
    public const decimal NormalFrom = 90m;

    /// <summary>
    /// Uma linha por região com equipamento, alerta ou agente no recorte, da mais grave para a menos grave (classificação
    /// e depois a menor disponibilidade). As regiões e seus ids vêm de <see cref="OperationsIndicators.AgentsInActivityByRegion"/>,
    /// que lista todas, inclusive com zero.
    /// </summary>
    public static IReadOnlyList<RegionSituation> From(OperationsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var equipmentByRegion = snapshot.Equipment.ToLookup(equipment => equipment.RegionName, StringComparer.Ordinal);

        // Alerta crítico listado só por causa da RN-21 (fora do filtro) não conta: o quadro reflete o recorte.
        var alertsByRegion = snapshot.Alerts.Where(alert => !alert.OutsideFilter).ToLookup(alert => alert.RegionName, StringComparer.Ordinal);

        return
        [
            .. snapshot.Indicators.AgentsInActivityByRegion
                .Select(region => Build(region, equipmentByRegion[region.RegionName].ToArray(), alertsByRegion[region.RegionName].ToArray()))
                .Where(situation => situation.TotalEquipment > 0 || situation.OpenAlerts > 0 || situation.AgentsInActivity > 0)
                .OrderByDescending(situation => situation.Classification)
                .ThenBy(situation => situation.AvailabilityPercent ?? decimal.MaxValue)
                .ThenBy(situation => situation.RegionName, StringComparer.CurrentCultureIgnoreCase),
        ];
    }

    private static RegionSituation Build(
        RegionAgentCount region, IReadOnlyList<EquipmentMarker> equipment, IReadOnlyList<AlertSummary> alerts)
    {
        var operational = equipment.Count(item => item.Status == EquipmentStatus.Operational);
        var unavailable = equipment.Count(item =>
            item.Status is EquipmentStatus.Malfunction or EquipmentStatus.UnderMaintenance or EquipmentStatus.Stolen);
        var expectedToOperate = equipment.Count(item => item.Status is not (EquipmentStatus.NotInstalled or EquipmentStatus.Deactivated));

        decimal? availability = expectedToOperate > 0
            ? Math.Round(operational * 100m / expectedToOperate, 1, MidpointRounding.AwayFromZero)
            : null;
        var critical = alerts.Count(alert => alert.Severity == AlertSeverity.Critical);

        return new RegionSituation(
            region.RegionId, region.RegionName, Classify(availability, critical), availability,
            equipment.Count, unavailable, alerts.Count, critical, region.AgentsInActivity);
    }

    /// <summary>
    /// RN-24: Crítica com disponibilidade abaixo de 75% ou algum alerta crítico aberto na região; Atenção de 75% a 89,9%;
    /// Normal a partir de 90% e sem alerta crítico. Sem equipamento que deveria operar (disponibilidade "—"), só o
    /// alerta crítico classifica a região; sem ele, fica "Sem dados no recorte", nunca Normal. Compara o valor já
    /// arredondado a uma casa, o mesmo que a tela mostra.
    /// </summary>
    public static RegionClassification Classify(decimal? availabilityPercent, int criticalAlerts) =>
        criticalAlerts > 0 || availabilityPercent < CriticalBelow ? RegionClassification.Critical
        : availabilityPercent is null ? RegionClassification.NoData
        : availabilityPercent < NormalFrom ? RegionClassification.Attention
        : RegionClassification.Normal;

    /// <summary>
    /// Pinta as regiões no mapa da Central pela classificação (RN-24): só as que têm contorno cadastrado; as escolhidas
    /// no filtro de região ganham o contorno mais grosso. Os polígonos não respondem ao clique: as regiões cobrem a cidade
    /// inteira, e um clique perdido ao lado de um marcador filtraria a Central; o filtro fica no quadro. O JS só desenha o
    /// que recebe (plan 17.12).
    /// </summary>
    public static IReadOnlyList<MapRegion> ToMapRegions(
        IReadOnlyList<RegionSituation> situations, IReadOnlyList<RegionData> regions, IReadOnlyCollection<Guid> selected)
    {
        var boundaries = regions.Where(region => region.Boundary is { Count: > 0 }).ToDictionary(region => region.Id);

        return
        [
            .. situations
                .Where(situation => boundaries.ContainsKey(situation.RegionId))
                .Select(situation =>
                {
                    var token = RegionClassificationTokens.For(situation.Classification);
                    return new MapRegion(
                        situation.RegionId.ToString(),
                        boundaries[situation.RegionId].Boundary!
                            .Select(ring => ring.Select(point => new[] { point.Latitude, point.Longitude }).ToArray())
                            .ToArray(),
                        token.Color,
                        $"{situation.RegionName} · {token.Label}",
                        IsDashed: false,
                        IsSelected: selected.Contains(situation.RegionId),
                        FillOpacity: 0.2,
                        IsInteractive: false);
                }),
        ];
    }
}
