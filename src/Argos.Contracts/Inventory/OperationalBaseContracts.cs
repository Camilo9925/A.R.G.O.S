using System.ComponentModel.DataAnnotations;

namespace Argos.Contracts.Inventory;

/// <summary>Base operacional de um órgão (RF-019; D-57; plan 15.14).</summary>
public sealed record OperationalBaseData
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public Guid OrganizationId { get; init; }

    public string OrganizationAcronym { get; init; } = string.Empty;

    public LocationData Location { get; init; } = new();

    public bool IsActive { get; init; }
}

/// <summary>Cadastro ou edição de base operacional: nome, órgão e localização (RF-019).</summary>
public sealed record OperationalBaseRequest
{
    [Required(ErrorMessage = "Informe o nome da base.")]
    [StringLength(80, MinimumLength = 3, ErrorMessage = "O nome deve ter entre {2} e {1} caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escolha o órgão da base.")]
    public Guid? OrganizationId { get; set; }

    [Required(ErrorMessage = "Informe a localização da base.")]
    public LocationInput? Location { get; set; }
}
