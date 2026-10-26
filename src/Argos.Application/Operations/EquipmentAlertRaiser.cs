using Argos.Domain.Inventory.Equipments;
using Argos.Domain.Operations.Alerts;
using Argos.Domain.Telemetry;

namespace Argos.Application.Operations;

/// <summary>
/// Passo comum de geração de alertas (RF-035, RN-18, RN-19; D-77; plan 16.6). Para uma mudança de situação já aplicada
/// ao equipamento, pede a causa ao <see cref="AlertCatalog"/>; com causa, atualiza o alerta aberto da mesma causa
/// (<see cref="Alert.Recur"/>, RN-19) ou abre um novo. Roda na mesma unidade de trabalho da mudança, para o alerta e a
/// situação serem gravados juntos: quem chama confirma com um só <c>SaveChanges</c>.
/// <para>
/// Chamado por <c>EquipmentStatusService</c> (mudança manual, inclusive pela Central), <c>MaintenanceService</c>
/// (conclusão sem sucesso, RN-11), <c>EquipmentService</c> (cadastro em Mau funcionamento, D-66) e pela carga
/// <c>operations-alerts-v1</c>.
/// </para>
/// </summary>
public sealed class EquipmentAlertRaiser(IAlertRepository alerts)
{
    /// <summary>
    /// Conflito do índice único parcial do alerta aberto (RN-19; plan 16.9): duas mudanças simultâneas tentaram abrir o
    /// mesmo alerta. A unidade de trabalho devolve 409 com esta mensagem.
    /// </summary>
    public const string DuplicateOpenAlertMessage = "Já existe um alerta aberto para essa causa neste recurso.";

    /// <param name="equipment">O equipamento já na situação nova.</param>
    /// <param name="previousStatus">Situação antes da mudança; nula no cadastro.</param>
    /// <param name="at">Instante da mudança.</param>
    /// <param name="by">Autor da mudança, que também abre o alerta.</param>
    /// <returns>O alerta aberto ou atualizado, ou nulo se a mudança não gera alerta.</returns>
    public async Task<Alert?> RaiseAsync(
        Equipment equipment, EquipmentStatus? previousStatus, DateTimeOffset at, string by, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(equipment);

        if (AlertCatalog.ForStatusChange(previousStatus, equipment.Status) is not { } definition)
        {
            return null;
        }

        var open = await alerts.FindOpenAsync(ResourceKind.Equipment, equipment.Id, definition.Cause, cancellationToken);
        if (open is not null)
        {
            open.Recur(at);
            return open;
        }

        var alert = Alert.Open(definition, ResourceKind.Equipment, equipment.Id, at, by);
        alerts.Add(alert);
        return alert;
    }
}
