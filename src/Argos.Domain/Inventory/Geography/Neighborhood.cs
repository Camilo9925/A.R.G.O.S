using Argos.Domain.Common;

namespace Argos.Domain.Inventory.Geography;

/// <summary>
/// Bairro (RF-020, RN-02, RN-22; D-50; plan 15.2.1): atributo com ponto central, sem polígono. Pertence a
/// exatamente uma região ativa. Nunca é excluído, só inativado (RN-23).
/// </summary>
public sealed class Neighborhood : IAuditable, IDeactivatable
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 80;

    // Usado pelo EF Core ao materializar.
    private Neighborhood()
    {
    }

    private Neighborhood(Guid id, string name, Guid regionId, GeoPoint center)
    {
        Id = id;
        SetName(name);
        RegionId = regionId;
        Center = center;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Nome normalizado (D-53), para a unicidade e a pesquisa por bairro (US-03).</summary>
    public string NameKey { get; private set; } = string.Empty;

    public Guid RegionId { get; private set; }

    public GeoPoint Center { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public DateTimeOffset? DeactivatedAt { get; private set; }

    public string? DeactivatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; private set; }

    public string? UpdatedBy { get; private set; }

    /// <param name="region">Região escolhida, ou nulo se não foi informada (RN-22).</param>
    public static Neighborhood Create(string name, Region? region, GeoPoint center, Municipality municipality)
    {
        var validName = Validate(name, region, center, municipality);
        return new Neighborhood(Guid.NewGuid(), validName, region!.Id, center);
    }

    /// <summary>Atualiza nome, região e centro. Devolve as alterações para o registro cadastral (D-49).</summary>
    public IReadOnlyList<FieldChange> Update(string name, Region? region, GeoPoint center, Municipality municipality)
    {
        var validName = Validate(name, region, center, municipality);
        var changes = new List<FieldChange>();

        if (validName != Name)
        {
            changes.Add(new FieldChange("name", "Nome", Name, validName));
            SetName(validName);
        }

        if (region!.Id != RegionId)
        {
            changes.Add(new FieldChange("regionId", "Região", RegionId.ToString(), region.Id.ToString()));
            RegionId = region.Id;
        }

        if (center != Center)
        {
            changes.Add(new FieldChange("center", "Ponto central", Center.ToString(), center.ToString()));
            Center = center;
        }

        return changes;
    }

    /// <summary>Inativa o bairro, recusada se houver equipamento, base ou agente ativo nele (plan 15.2.1).</summary>
    public void Inactivate(bool hasActiveDependents, DateTimeOffset at, string by)
    {
        if (!IsActive)
        {
            throw new InventoryRuleException("O bairro já está inativo.");
        }

        if (hasActiveDependents)
        {
            throw new InventoryRuleException(
                "O bairro tem equipamentos, bases ou agentes ativos e não pode ser inativado.");
        }

        IsActive = false;
        DeactivatedAt = at.ToUniversalTime();
        DeactivatedBy = InventoryAuthor.Valid(by);
    }

    private static string Validate(string name, Region? region, GeoPoint center, Municipality municipality)
    {
        var errors = new FieldErrors();
        var validName = errors.RequiredText("name", name, NameMinLength, NameMaxLength, "o nome do bairro");

        if (region is null)
        {
            errors.Add("regionId", "Escolha a região do bairro.");
        }
        else if (!region.IsActive)
        {
            errors.Add("regionId", "A região escolhida está inativa.");
        }

        if (!municipality.Contains(center))
        {
            errors.Add("center", "O ponto central deve ficar dentro do município.");
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
