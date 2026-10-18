using Argos.Domain.Common;
using Argos.Domain.Inventory.Geography;

namespace Argos.Domain.Inventory.Support;

/// <summary>
/// Base operacional de um órgão (RF-007, RF-014, RF-017, RF-019; D-57; plan 15.2.3): o lugar de equipamentos móveis e
/// de agentes. A região e o bairro de um recurso móvel vêm do bairro da base (D-65). Nunca é excluída (RN-23).
/// </summary>
public sealed class OperationalBase : IAuditable, IDeactivatable
{
    public const int NameMinLength = 3;
    public const int NameMaxLength = 80;

    // Usado pelo EF Core ao materializar.
    private OperationalBase()
    {
    }

    private OperationalBase(Guid id, string name, Guid organizationId, Location location)
    {
        Id = id;
        SetName(name);
        OrganizationId = organizationId;
        Location = location;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Nome normalizado (D-53): único por órgão.</summary>
    public string NameKey { get; private set; } = string.Empty;

    public Guid OrganizationId { get; private set; }

    public Location Location { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public DateTimeOffset? DeactivatedAt { get; private set; }

    public string? DeactivatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; private set; }

    public string? UpdatedBy { get; private set; }

    /// <param name="organization">Órgão escolhido, ou nulo se não foi informado (RN-22).</param>
    /// <param name="location">Localização já validada por <see cref="Location.Create"/> (RN-02, RN-03).</param>
    public static OperationalBase Create(string name, Organization? organization, Location location)
    {
        var validName = Validate(name, organization, currentOrganizationId: null);
        ArgumentNullException.ThrowIfNull(location);

        return new OperationalBase(Guid.NewGuid(), validName, organization!.Id, location);
    }

    /// <summary>Atualiza nome, órgão e localização. Devolve as alterações para o registro cadastral (D-49).</summary>
    public IReadOnlyList<FieldChange> Update(string name, Organization? organization, Location location)
    {
        var validName = Validate(name, organization, OrganizationId);
        ArgumentNullException.ThrowIfNull(location);

        var changes = new List<FieldChange>();

        if (validName != Name)
        {
            changes.Add(new FieldChange("name", "Nome", Name, validName));
            SetName(validName);
        }

        if (organization!.Id != OrganizationId)
        {
            changes.Add(new FieldChange(
                "organizationId", "Órgão", OrganizationId.ToString(), organization.Id.ToString()));
            OrganizationId = organization.Id;
        }

        changes.AddRange(Location.Changes(Location, location));
        Location = location;
        return changes;
    }

    /// <summary>Inativa a base, recusada se houver equipamento ou agente ativo nela (plan 15.2.3).</summary>
    public void Inactivate(bool hasActiveDependents, DateTimeOffset at, string by)
    {
        if (!IsActive)
        {
            throw new InventoryRuleException("A base já está inativa.");
        }

        if (hasActiveDependents)
        {
            throw new InventoryRuleException("A base tem equipamentos ou agentes ativos e não pode ser inativada.");
        }

        IsActive = false;
        DeactivatedAt = at.ToUniversalTime();
        DeactivatedBy = InventoryAuthor.Valid(by);
    }

    /// <summary>Órgão obrigatório e ativo; um órgão que ficou inativo continua aceito para a base que já é dele.</summary>
    private static string Validate(string name, Organization? organization, Guid? currentOrganizationId)
    {
        var errors = new FieldErrors();
        var validName = errors.RequiredText("name", name, NameMinLength, NameMaxLength, "o nome da base");

        if (organization is null)
        {
            errors.Add("organizationId", "Escolha o órgão da base.");
        }
        else if (!organization.IsActive && organization.Id != currentOrganizationId)
        {
            errors.Add("organizationId", "O órgão escolhido está inativo.");
        }

        errors.ThrowIfAny();
        return validName;
    }

    private void SetName(string name)
    {
        Name = name;
        NameKey = TextNormalization.Normalize(name);
    }
}
