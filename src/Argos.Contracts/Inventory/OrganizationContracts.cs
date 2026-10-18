using System.ComponentModel.DataAnnotations;

namespace Argos.Contracts.Inventory;

/// <summary>Órgão responsável (RF-019, RN-22; plan 15.14).</summary>
public sealed record OrganizationData
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Acronym { get; init; } = string.Empty;

    public bool IsActive { get; init; }
}

/// <summary>Cadastro ou edição de órgão (RF-019).</summary>
public sealed record OrganizationRequest
{
    [Required(ErrorMessage = "Informe o nome do órgão.")]
    [StringLength(120, MinimumLength = 3, ErrorMessage = "O nome deve ter entre {2} e {1} caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a sigla.")]
    [StringLength(15, MinimumLength = 2, ErrorMessage = "A sigla deve ter entre {2} e {1} caracteres.")]
    public string Acronym { get; set; } = string.Empty;
}
