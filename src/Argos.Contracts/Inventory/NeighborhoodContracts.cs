using System.ComponentModel.DataAnnotations;

namespace Argos.Contracts.Inventory;

/// <summary>Bairro, com a região a que pertence (RF-020, RN-22; plan 15.14).</summary>
public sealed record NeighborhoodData
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public Guid RegionId { get; init; }

    public string RegionName { get; init; } = string.Empty;

    public GeoPointData Center { get; init; } = new(0, 0);

    public bool IsActive { get; init; }
}

/// <summary>
/// Cadastro ou edição de bairro: nome, região e centro escolhido no mapa (RF-005, RF-020). Os erros do centro chegam
/// em <c>center</c> (fora do município) ou em <c>center.latitude</c> e <c>center.longitude</c>.
/// </summary>
public sealed record NeighborhoodRequest
{
    [Required(ErrorMessage = "Informe o nome do bairro.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "O nome deve ter entre {2} e {1} caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escolha a região do bairro.")]
    public Guid? RegionId { get; set; }

    [Required(ErrorMessage = "Escolha o ponto central do bairro.")]
    public GeoPointInput? Center { get; set; }
}
