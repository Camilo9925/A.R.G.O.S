using Argos.Domain.Inventory.Equipments;
using Argos.Domain.Operations;

namespace Argos.Application.Seeding.Operations;

/// <summary>Versões da carga da Central (plan 16.9; D-78, D-94).</summary>
public static class OperationsSeed
{
    /// <summary>Fase 4: escala de referência simulada dos agentes ativos (A-22; D-94).</summary>
    public const string DutyRosterVersion = "operations-duty-roster-v1";

    /// <summary>Fase 4: alertas dos equipamentos ativos em Mau funcionamento ou Furtado (A-29; D-78).</summary>
    public const string AlertsVersion = "operations-alerts-v1";
}

/// <summary>
/// Versão <c>operations-duty-roster-v1</c>: o turno de cada agente, pelas chaves de negócio (órgão + matrícula),
/// calculado pelo Migrator com o gerador da Simulação, que a Aplicação não referencia.
/// </summary>
public sealed record DutyRosterSeed(IReadOnlyList<DutyShiftSeed> Shifts);

public sealed record DutyShiftSeed(string OrganizationAcronym, string RegistrationNumber, DutyShift Shift);

/// <summary>
/// Porta das cargas da Central (plan 16.9): a escala só recebe registros novos (Art. V.1) e os alertas da carga partem
/// dos equipamentos ativos nas situações que abrem alerta (RN-18). Sem remoção.
/// </summary>
public interface IOperationsSeedStore
{
    /// <summary>Ids dos equipamentos <b>ativos</b> nas situações informadas, em ordem de id.</summary>
    Task<IReadOnlyList<Guid>> ListActiveEquipmentIdsAsync(
        IReadOnlyCollection<EquipmentStatus> statuses, CancellationToken cancellationToken = default);

    void AddDutyShift(AgentDutyShift shift);
}
