using Argos.Domain.Common;

namespace Argos.Domain.Operations;

/// <summary>Turno da escala de referência simulada (A-22; D-75, D-94; plan 16.2, 17.2), no fuso do município.</summary>
public enum DutyShift
{
    /// <summary>Manhã, das 06h às 14h.</summary>
    Morning,

    /// <summary>Tarde, das 14h às 22h.</summary>
    Afternoon,

    /// <summary>Noite, das 22h às 06h do dia seguinte.</summary>
    Night,
}

/// <summary>
/// Horários dos turnos (D-75; plan 16.2): os três cobrem as 24 h sem sobreposição, com o início incluído e o fim
/// excluído. A descoberta da telemetria usa <see cref="Covers"/> para dizer ao Simulador quem está no turno agora
/// (<c>scheduledOnDuty</c>, plan 16.4).
/// </summary>
public static class DutyShiftSchedule
{
    public static readonly TimeOnly MorningStart = new(6, 0);

    public static readonly TimeOnly AfternoonStart = new(14, 0);

    public static readonly TimeOnly NightStart = new(22, 0);

    /// <summary>Turno que cobre a hora local informada.</summary>
    public static DutyShift ShiftAt(TimeOnly localTime)
    {
        if (localTime >= MorningStart && localTime < AfternoonStart)
        {
            return DutyShift.Morning;
        }

        return localTime >= AfternoonStart && localTime < NightStart ? DutyShift.Afternoon : DutyShift.Night;
    }

    /// <summary>Se o turno cobre a hora local informada.</summary>
    public static bool Covers(DutyShift shift, TimeOnly localTime)
    {
        if (!Enum.IsDefined(shift))
        {
            throw new ArgumentOutOfRangeException(nameof(shift), shift, "Turno desconhecido.");
        }

        return ShiftAt(localTime) == shift;
    }
}

/// <summary>
/// Turno de um agente na escala de referência simulada (A-22; D-94; plan 16.2, 16.9): gravado uma vez pela carga
/// <c>operations-duty-roster-v1</c>, com o instante e o autor da atribuição, fora do agregado <c>Agent</c> para não
/// mexer na versão dele (D-60). Agente sem escala, cadastrado depois da carga, não é posto em serviço pelo Simulador.
/// </summary>
public sealed class AgentDutyShift : IAppendOnly
{
    // Usado pelo EF Core ao materializar.
    private AgentDutyShift()
    {
    }

    private AgentDutyShift(Guid agentId, DutyShift shift, DateTimeOffset assignedAt, string assignedBy)
    {
        AgentId = agentId;
        Shift = shift;
        AssignedAt = assignedAt.ToUniversalTime();
        AssignedBy = assignedBy;
    }

    public Guid AgentId { get; private set; }

    public DutyShift Shift { get; private set; }

    public DateTimeOffset AssignedAt { get; private set; }

    /// <summary>Autor da atribuição: <c>simulação</c> na carga.</summary>
    public string AssignedBy { get; private set; } = string.Empty;

    public static AgentDutyShift Assign(Guid agentId, DutyShift shift, DateTimeOffset at, string by)
    {
        if (!Enum.IsDefined(shift))
        {
            throw new ArgumentOutOfRangeException(nameof(shift), shift, "Turno desconhecido.");
        }

        if (string.IsNullOrWhiteSpace(by))
        {
            throw new ArgumentException("Informe o autor da operação.", nameof(by));
        }

        return new AgentDutyShift(agentId, shift, at, by.Trim());
    }
}
