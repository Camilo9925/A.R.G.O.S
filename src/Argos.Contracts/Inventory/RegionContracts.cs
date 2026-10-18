using System.ComponentModel.DataAnnotations;

namespace Argos.Contracts.Inventory;

/// <summary>Região (zona) do município (RF-020; plan 15.14). Só as zonas carregadas têm contorno (D-50).</summary>
public sealed record RegionData
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>Anéis fechados do contorno, ou nulo numa região criada pela interface.</summary>
    public IReadOnlyList<IReadOnlyList<GeoPointData>>? Boundary { get; init; }

    public bool IsActive { get; init; }
}

/// <summary>Cadastro ou edição de região (RF-020).</summary>
public sealed record RegionRequest
{
    [Required(ErrorMessage = "Informe o nome da região.")]
    [StringLength(60, MinimumLength = 2, ErrorMessage = "O nome deve ter entre {2} e {1} caracteres.")]
    public string Name { get; set; } = string.Empty;
}
