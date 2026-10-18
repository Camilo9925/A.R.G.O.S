using Argos.Domain.Common;

namespace Argos.Domain.Inventory;

/// <summary>Cadastro a que se refere um registro de alteração (plan 15.2.11).</summary>
public enum InventoryEntityType
{
    Equipment,
    Agent,
    Organization,
    Region,
    Neighborhood,
    OperationalBase,
    EquipmentType,
}

/// <summary>Natureza do registro de alteração.</summary>
public enum InventoryChangeKind
{
    Created,
    Updated,
    Inactivated,
}

/// <summary>
/// Registro de uma alteração cadastral (Art. V.1, VIII.4; US-08; D-49; plan 15.2.11), com instante, autor e os campos
/// alterados em texto de exibição. É a fonte das "alterações de cadastro" da linha do tempo e da auditoria de
/// cadastros. O cadastro de equipamento e de agente não gera <see cref="InventoryChangeKind.Created"/>, porque o
/// registro inicial de situação já o representa. Nunca é alterado nem removido.
/// </summary>
public sealed class InventoryChange : IAppendOnly
{
    // Usado pelo EF Core ao materializar.
    private InventoryChange()
    {
    }

    private InventoryChange(
        InventoryEntityType entityType,
        Guid entityId,
        InventoryChangeKind kind,
        IReadOnlyList<FieldChange> changes,
        string? reason,
        DateTimeOffset occurredAt,
        string author)
    {
        Id = Guid.NewGuid();
        EntityType = entityType;
        EntityId = entityId;
        Kind = kind;
        Changes = changes;
        Reason = reason;
        OccurredAt = occurredAt.ToUniversalTime();
        Author = InventoryAuthor.Valid(author);
    }

    public Guid Id { get; private set; }

    public InventoryEntityType EntityType { get; private set; }

    public Guid EntityId { get; private set; }

    public InventoryChangeKind Kind { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public string Author { get; private set; } = string.Empty;

    /// <summary>Motivo opcional, como o da inativação (RN-35).</summary>
    public string? Reason { get; private set; }

    public IReadOnlyList<FieldChange> Changes { get; private set; } = [];

    public static InventoryChange Created(InventoryEntityType entityType, Guid entityId, DateTimeOffset at, string by) =>
        new(entityType, entityId, InventoryChangeKind.Created, [], null, at, by);

    /// <summary>Edição com ao menos um campo alterado; sem alteração, nada é gravado (plan 15.2.6).</summary>
    public static InventoryChange Updated(
        InventoryEntityType entityType, Guid entityId, IReadOnlyList<FieldChange> changes, DateTimeOffset at, string by)
    {
        if (changes.Count == 0)
        {
            throw new ArgumentException("Uma edição sem campo alterado não gera registro.", nameof(changes));
        }

        return new InventoryChange(entityType, entityId, InventoryChangeKind.Updated, changes.ToArray(), null, at, by);
    }

    public static InventoryChange Inactivated(
        InventoryEntityType entityType, Guid entityId, string? reason, DateTimeOffset at, string by) =>
        new(entityType, entityId, InventoryChangeKind.Inactivated, [], string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), at, by);
}
