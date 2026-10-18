using Argos.Domain.Common;

namespace Argos.Domain.Inventory.Support;

/// <summary>
/// Órgão responsável por equipamentos, bases e agentes (RF-019, RN-22; plan 15.2.2). Um órgão inativo não recebe
/// novos equipamentos, bases ou agentes; os existentes continuam ligados a ele. Nunca é excluído (RN-23).
/// </summary>
public sealed class Organization : IAuditable, IDeactivatable
{
    public const int NameMinLength = 3;
    public const int NameMaxLength = 120;
    public const int AcronymMinLength = 2;
    public const int AcronymMaxLength = 15;

    // Usado pelo EF Core ao materializar.
    private Organization()
    {
    }

    private Organization(Guid id, string name, string acronym)
    {
        Id = id;
        SetName(name);
        SetAcronym(acronym);
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string NameKey { get; private set; } = string.Empty;

    /// <summary>Sigla, como "SMM" ou "GCM".</summary>
    public string Acronym { get; private set; } = string.Empty;

    public string AcronymKey { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset? DeactivatedAt { get; private set; }

    public string? DeactivatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; private set; }

    public string? UpdatedBy { get; private set; }

    public static Organization Create(string name, string acronym)
    {
        var (validName, validAcronym) = Validate(name, acronym);
        return new Organization(Guid.NewGuid(), validName, validAcronym);
    }

    /// <summary>Atualiza nome e sigla. Devolve as alterações para o registro cadastral (D-49).</summary>
    public IReadOnlyList<FieldChange> Update(string name, string acronym)
    {
        var (validName, validAcronym) = Validate(name, acronym);
        var changes = new List<FieldChange>();

        if (validName != Name)
        {
            changes.Add(new FieldChange("name", "Nome", Name, validName));
            SetName(validName);
        }

        if (validAcronym != Acronym)
        {
            changes.Add(new FieldChange("acronym", "Sigla", Acronym, validAcronym));
            SetAcronym(validAcronym);
        }

        return changes;
    }

    public void Inactivate(DateTimeOffset at, string by)
    {
        if (!IsActive)
        {
            throw new InventoryRuleException("O órgão já está inativo.");
        }

        IsActive = false;
        DeactivatedAt = at.ToUniversalTime();
        DeactivatedBy = InventoryAuthor.Valid(by);
    }

    private static (string Name, string Acronym) Validate(string name, string acronym)
    {
        var errors = new FieldErrors();
        var validName = errors.RequiredText("name", name, NameMinLength, NameMaxLength, "o nome do órgão");
        var validAcronym = errors.RequiredText("acronym", acronym, AcronymMinLength, AcronymMaxLength, "a sigla");
        errors.ThrowIfAny();

        return (validName, validAcronym);
    }

    private void SetName(string name)
    {
        Name = name;
        NameKey = TextNormalization.Normalize(name);
    }

    private void SetAcronym(string acronym)
    {
        Acronym = acronym;
        AcronymKey = TextNormalization.Normalize(acronym);
    }
}
