using Argos.Domain.Common;

namespace Argos.Domain.Inventory.Equipments;

/// <summary>Tipo de manutenção (RN-08).</summary>
public enum MaintenanceKind
{
    /// <summary>Corretiva, sem data futura.</summary>
    Immediate,

    /// <summary>Com data prevista.</summary>
    Scheduled,

    /// <summary>Com data prevista e periodicidade; a próxima é programada na conclusão (RN-09).</summary>
    Periodic,
}

/// <summary>Estado de uma manutenção (RN-08): aberta até ser concluída ou cancelada, sem voltar a abrir.</summary>
public enum MaintenanceState
{
    Open,
    Completed,
    Cancelled,
}

/// <summary>Resultado da conclusão (US-05, RN-11).</summary>
public enum MaintenanceResult
{
    Success,
    Failure,
}

/// <summary>De onde veio a manutenção (plan 15.2.8).</summary>
public enum MaintenanceOrigin
{
    /// <summary>Registrada por uma pessoa (US-05) ou como manutenção inicial do cadastro (RF-002).</summary>
    Manual,

    /// <summary>Aberta pela entrada em Mau funcionamento (RN-07) ou pela conclusão sem sucesso (RN-11).</summary>
    AutomaticOnMalfunction,

    /// <summary>Próxima periódica, programada na conclusão da anterior (RN-09).</summary>
    PeriodicRescheduling,
}

/// <summary>
/// Manutenção de um equipamento, entidade do agregado <see cref="Equipment"/> (RF-009, RN-08 a RN-11; plan 15.2.8).
/// Os dados de abertura nunca mudam, e os de conclusão ou de cancelamento são gravados uma única vez, numa única
/// transição a partir de aberta (Art. V.1; D-49). Só o agregado muda o estado.
/// </summary>
public sealed class Maintenance : IAuditable
{
    // Usado pelo EF Core ao materializar.
    private Maintenance()
    {
    }

    private Maintenance(
        Guid equipmentId,
        MaintenanceKind kind,
        DateOnly? dueDate,
        int? periodicityDays,
        string? description,
        MaintenanceOrigin origin,
        Guid? previousMaintenanceId,
        DateTimeOffset openedAt,
        string openedBy)
    {
        Id = Guid.NewGuid();
        EquipmentId = equipmentId;
        Kind = kind;
        State = MaintenanceState.Open;
        DueDate = dueDate;
        PeriodicityDays = periodicityDays;
        Description = description;
        Origin = origin;
        PreviousMaintenanceId = previousMaintenanceId;
        OpenedAt = openedAt.ToUniversalTime();
        OpenedBy = openedBy;
    }

    public Guid Id { get; private set; }

    public Guid EquipmentId { get; private set; }

    public MaintenanceKind Kind { get; private set; }

    public MaintenanceState State { get; private set; }

    public string? Description { get; private set; }

    /// <summary>Data prevista, no calendário do município (D-54); ausente na imediata.</summary>
    public DateOnly? DueDate { get; private set; }

    public int? PeriodicityDays { get; private set; }

    public MaintenanceOrigin Origin { get; private set; }

    /// <summary>Manutenção periódica anterior, quando esta foi programada pela RN-09.</summary>
    public Guid? PreviousMaintenanceId { get; private set; }

    public DateTimeOffset OpenedAt { get; private set; }

    public string OpenedBy { get; private set; } = string.Empty;

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? CompletedBy { get; private set; }

    public MaintenanceResult? Result { get; private set; }

    public string? CompletionNotes { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public string? CancelledBy { get; private set; }

    public string? CancellationReason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; private set; }

    public string? UpdatedBy { get; private set; }

    public bool IsOpen => State == MaintenanceState.Open;

    internal static Maintenance Open(
        Guid equipmentId,
        MaintenanceKind kind,
        DateOnly? dueDate,
        int? periodicityDays,
        string? description,
        MaintenanceOrigin origin,
        Guid? previousMaintenanceId,
        DateTimeOffset openedAt,
        string openedBy) =>
        new(equipmentId, kind, dueDate, periodicityDays, description, origin, previousMaintenanceId, openedAt, openedBy);

    internal void Complete(MaintenanceResult result, DateTimeOffset completedAt, string? notes, string by)
    {
        EnsureOpen();
        State = MaintenanceState.Completed;
        Result = result;
        CompletedAt = completedAt.ToUniversalTime();
        CompletedBy = by;
        CompletionNotes = notes;
    }

    internal void Cancel(string reason, DateTimeOffset at, string by)
    {
        EnsureOpen();
        State = MaintenanceState.Cancelled;
        CancelledAt = at.ToUniversalTime();
        CancelledBy = by;
        CancellationReason = reason;
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new InventoryRuleException("A manutenção já foi encerrada e não volta a abrir.");
        }
    }
}
