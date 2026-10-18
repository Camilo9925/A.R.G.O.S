namespace Argos.Contracts.Inventory;

/// <summary>Registro do histórico de situação (RF-008, US-04; plan 15.2.7). O registro inicial não tem situação anterior.</summary>
public sealed record StatusChangeData
{
    public Guid Id { get; init; }

    public EquipmentStatus? FromStatus { get; init; }

    public EquipmentStatus ToStatus { get; init; }

    public StatusChangeOrigin Origin { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public string Author { get; init; } = string.Empty;

    public string? Justification { get; init; }

    /// <summary>Manutenção cuja conclusão causou a mudança (RN-11).</summary>
    public Guid? MaintenanceId { get; init; }
}

/// <summary>Manutenção de um equipamento, aberta ou encerrada (RF-009, US-05; plan 15.2.8).</summary>
public sealed record MaintenanceData
{
    public Guid Id { get; init; }

    public MaintenanceKind Kind { get; init; }

    public MaintenanceState State { get; init; }

    public string? Description { get; init; }

    public DateOnly? DueDate { get; init; }

    public int? PeriodicityDays { get; init; }

    public MaintenanceOrigin Origin { get; init; }

    public Guid? PreviousMaintenanceId { get; init; }

    /// <summary>Necessidade que esta manutenção representa, para o selo de vencida ou próxima (RN-10); nula se encerrada.</summary>
    public MaintenanceNeed? Need { get; init; }

    public DateTimeOffset OpenedAt { get; init; }

    public string OpenedBy { get; init; } = string.Empty;

    public DateTimeOffset? CompletedAt { get; init; }

    public string? CompletedBy { get; init; }

    public MaintenanceResult? Result { get; init; }

    public string? CompletionNotes { get; init; }

    public DateTimeOffset? CancelledAt { get; init; }

    public string? CancelledBy { get; init; }

    public string? CancellationReason { get; init; }
}

/// <summary>
/// Resultado da conclusão (plan 15.14): a manutenção concluída, a situação em que o equipamento ficou (RN-11) e a
/// próxima periódica programada (RN-09), quando houver.
/// </summary>
public sealed record MaintenanceCompletionData
{
    public MaintenanceData Completed { get; init; } = new();

    public EquipmentStatus Status { get; init; }

    public MaintenanceData? NextPeriodic { get; init; }
}
