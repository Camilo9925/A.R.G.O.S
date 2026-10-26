using Argos.Application.Common.Abstractions;
using Argos.Application.Inventory.Equipments;
using Argos.Application.Operations;
using Argos.Application.Seeding.Inventory;
using Argos.Domain.Inventory.Equipments;
using Argos.Domain.Operations.Alerts;

namespace Argos.Application.Seeding.Operations;

/// <summary>
/// Carga <c>operations-alerts-v1</c> (Fase 4; A-29; D-78; plan 16.6, 16.9): abre, para cada equipamento <b>ativo</b> em
/// Mau funcionamento ou Furtado, o alerta da causa (RN-18) no instante da última mudança de situação
/// (<c>StatusChangedAt</c>), com autor <c>simulação</c> (Art. IX.2), pelo mesmo <see cref="EquipmentAlertRaiser"/> dos
/// casos de uso. Exige <c>inventory-inactivations-v1</c> e roda depois da escala; tudo, com o registro da versão, num
/// só <c>SaveChanges</c>, e rodar de novo com a mesma semente não grava nada. Com a semente 42: 18 alertas, 15 Altos e
/// 3 Críticos (plan 16.15).
/// </summary>
public sealed class OperationsAlertSeeder(
    IOperationsSeedStore store,
    IEquipmentRepository equipments,
    EquipmentAlertRaiser alertRaiser,
    ISeedLedger ledger,
    IUnitOfWork unitOfWork)
{
    private static readonly EquipmentStatus[] AlertingStatuses = [EquipmentStatus.Malfunction, EquipmentStatus.Stolen];

    /// <param name="seed">Semente da simulação, registrada com a versão (plan 7.4).</param>
    /// <exception cref="InvalidOperationException">Inativações ainda não aplicadas ou versão aplicada com outra semente.</exception>
    public async Task<SeedResult> SeedAsync(int seed, CancellationToken cancellationToken = default)
    {
        if (!await InventorySeed.ShouldApplyAsync(ledger, OperationsSeed.AlertsVersion, seed, cancellationToken))
        {
            return SeedResult.Skipped(OperationsSeed.AlertsVersion);
        }

        if (await ledger.FindAppliedSeedAsync(InventorySeed.InactivationsVersion, cancellationToken) is null)
        {
            throw new InvalidOperationException(
                $"Aplique a carga {InventorySeed.InactivationsVersion} antes de {OperationsSeed.AlertsVersion}.");
        }

        var critical = 0;
        var high = 0;

        foreach (var id in await store.ListActiveEquipmentIdsAsync(AlertingStatuses, cancellationToken))
        {
            var equipment = await equipments.FindByIdAsync(id, cancellationToken);
            var alert = equipment is null
                ? null
                : await alertRaiser.RaiseAsync(
                    equipment, previousStatus: null, equipment.StatusChangedAt, InventorySeed.Author, cancellationToken);

            if (alert?.Severity == AlertSeverity.Critical)
            {
                critical++;
            }
            else if (alert?.Severity == AlertSeverity.High)
            {
                high++;
            }
        }

        ledger.Record(OperationsSeed.AlertsVersion, seed);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SeedResult(
            OperationsSeed.AlertsVersion,
            false,
            new Dictionary<string, int> { ["critical"] = critical, ["high"] = high });
    }
}
