using Argos.Domain.Common;

namespace Argos.Domain.Inventory.Geography;

/// <summary>
/// Região (zona) do município (RF-020, RN-22; D-50; plan 15.2.1). As cinco zonas carregadas têm contorno; uma região
/// criada pela interface não tem e não é desenhada no mapa. Nunca é excluída, só inativada (RN-23).
/// </summary>
public sealed class Region : IAuditable, IDeactivatable
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 60;

    // Usado pelo EF Core ao materializar.
    private Region()
    {
    }

    private Region(Guid id, string name, GeoPolygon? boundary)
    {
        Id = id;
        SetName(name);
        Boundary = boundary;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Nome normalizado (D-53): a unicidade é sem diferença de maiúsculas e acentos, inclusive entre inativas.</summary>
    public string NameKey { get; private set; } = string.Empty;

    public GeoPolygon? Boundary { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset? DeactivatedAt { get; private set; }

    public string? DeactivatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; private set; }

    public string? UpdatedBy { get; private set; }

    public static Region Create(string name, GeoPolygon? boundary = null)
    {
        var errors = new FieldErrors();
        var validName = errors.RequiredText("name", name, NameMinLength, NameMaxLength, "o nome da região");
        errors.ThrowIfAny();

        return new Region(Guid.NewGuid(), validName, boundary);
    }

    /// <summary>Troca o nome. Devolve a alteração para o registro cadastral; sem alteração, lista vazia (D-49).</summary>
    public IReadOnlyList<FieldChange> Rename(string name)
    {
        var errors = new FieldErrors();
        var validName = errors.RequiredText("name", name, NameMinLength, NameMaxLength, "o nome da região");
        errors.ThrowIfAny();

        if (validName == Name)
        {
            return [];
        }

        var change = new FieldChange("name", "Nome", Name, validName);
        SetName(validName);
        return [change];
    }

    /// <summary>Inativa a região, recusada se houver bairro ativo nela (plan 15.2.1).</summary>
    public void Inactivate(bool hasActiveNeighborhoods, DateTimeOffset at, string by)
    {
        if (!IsActive)
        {
            throw new InventoryRuleException("A região já está inativa.");
        }

        if (hasActiveNeighborhoods)
        {
            throw new InventoryRuleException("A região tem bairros ativos e não pode ser inativada.");
        }

        IsActive = false;
        DeactivatedAt = at.ToUniversalTime();
        DeactivatedBy = InventoryAuthor.Valid(by);
    }

    private void SetName(string name)
    {
        Name = name;
        NameKey = TextNormalization.Normalize(name);
    }
}
