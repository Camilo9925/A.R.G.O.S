using Argos.Domain.Inventory.Equipments;

namespace Argos.Domain.Operations;

/// <summary>Indicadores de disponibilidade calculados sobre as contagens por situação (RN-14, RN-15).</summary>
/// <param name="AvailabilityPercent">
/// Disponibilidade em %, arredondada a uma casa; nula quando não há equipamento que deveria estar operando.
/// </param>
public sealed record AvailabilitySummary(int Total, int Operational, int Unavailable, decimal? AvailabilityPercent);

/// <summary>
/// RN-14 e RN-15 (US-12; plan 16.3), num lugar só, para a Central e qualquer outro lugar exibirem o mesmo número:
/// <list type="bullet">
/// <item>indisponíveis = Mau funcionamento + Em manutenção + Furtado (RN-15);</item>
/// <item>disponibilidade = Operacionais ÷ (Total − Ainda não instalados − Desativados) × 100, arredondada a uma casa,
/// com o meio para cima; nula com denominador zero (a Web mostra "—", RN-14).</item>
/// </list>
/// Recebe as contagens só dos equipamentos ativos, já filtradas (RN-16).
/// </summary>
public static class AvailabilityCalculator
{
    /// <summary>Situações que contam como indisponíveis (RN-15).</summary>
    public static readonly IReadOnlyList<EquipmentStatus> UnavailableStatuses =
        [EquipmentStatus.Malfunction, EquipmentStatus.UnderMaintenance, EquipmentStatus.Stolen];

    /// <param name="countsByStatus">Quantidade por situação; a situação ausente conta zero.</param>
    public static AvailabilitySummary Calculate(IReadOnlyDictionary<EquipmentStatus, int> countsByStatus)
    {
        ArgumentNullException.ThrowIfNull(countsByStatus);

        int Count(EquipmentStatus status) => countsByStatus.TryGetValue(status, out var count) ? count : 0;

        var total = countsByStatus.Values.Sum();
        var operational = Count(EquipmentStatus.Operational);
        var unavailable = UnavailableStatuses.Sum(Count);
        var expectedToOperate = total - Count(EquipmentStatus.NotInstalled) - Count(EquipmentStatus.Deactivated);

        decimal? percent = expectedToOperate > 0
            ? Math.Round(operational * 100m / expectedToOperate, 1, MidpointRounding.AwayFromZero)
            : null;

        return new AvailabilitySummary(total, operational, unavailable, percent);
    }
}
