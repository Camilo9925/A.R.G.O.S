using Argos.Domain.Common;

namespace Argos.Domain.Inventory.Equipments;

/// <summary>
/// Registro de uma mudança de situação (RF-008, US-01, US-04; plan 15.2.7), com instante, autor e justificativa.
/// O registro inicial do cadastro não tem situação anterior. Nunca é alterado nem removido (Art. V.1).
/// </summary>
public sealed class EquipmentStatusChange : IAppendOnly
{
    // Usado pelo EF Core ao materializar.
    private EquipmentStatusChange()
    {
    }

    private EquipmentStatusChange(
        Guid equipmentId,
        EquipmentStatus? fromStatus,
        EquipmentStatus toStatus,
        StatusChangeOrigin origin,
        string? justification,
        Guid? maintenanceId,
        DateTimeOffset occurredAt,
        string author)
    {
        Id = Guid.NewGuid();
        EquipmentId = equipmentId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Origin = origin;
        Justification = justification;
        MaintenanceId = maintenanceId;
        OccurredAt = occurredAt.ToUniversalTime();
        Author = author;
    }

    public Guid Id { get; private set; }

    public Guid EquipmentId { get; private set; }

    /// <summary>Nula no registro inicial (US-01).</summary>
    public EquipmentStatus? FromStatus { get; private set; }

    public EquipmentStatus ToStatus { get; private set; }

    public StatusChangeOrigin Origin { get; private set; }

    public string? Justification { get; private set; }

    /// <summary>Manutenção cuja conclusão causou a mudança (RN-11).</summary>
    public Guid? MaintenanceId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public string Author { get; private set; } = string.Empty;

    internal static EquipmentStatusChange Record(
        Guid equipmentId,
        EquipmentStatus? fromStatus,
        EquipmentStatus toStatus,
        StatusChangeOrigin origin,
        string? justification,
        Guid? maintenanceId,
        DateTimeOffset occurredAt,
        string author) =>
        new(equipmentId, fromStatus, toStatus, origin, justification, maintenanceId, occurredAt, author);
}
