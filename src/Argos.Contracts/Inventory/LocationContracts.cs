using System.ComponentModel.DataAnnotations;

namespace Argos.Contracts.Inventory;

/// <summary>
/// Localização informada no formulário (RF-003 a RF-005; plan 15.14). Cidade e UF não entram: são sempre as do
/// município (RF-004; D-50). Os erros do Domínio chegam com o prefixo do campo que contém o objeto, como
/// <c>location.latitude</c> (D-46).
/// </summary>
public sealed record LocationInput
{
    [Required(ErrorMessage = "Informe o CEP.")]
    [RegularExpression(@"^\s*\d{5}-?\d{3}\s*$", ErrorMessage = "Informe o CEP com 8 dígitos, no formato 00000-000.")]
    public string ZipCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a rua.")]
    [StringLength(120, MinimumLength = 3, ErrorMessage = "A rua deve ter entre {2} e {1} caracteres.")]
    public string Street { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escolha o bairro.")]
    public Guid? NeighborhoodId { get; set; }

    /// <summary>Texto livre obrigatório, como "número 181", "oposto ao número 181", "KM 203" ou "S/N" (RN-03).</summary>
    [Required(ErrorMessage = "Informe a referência do local.")]
    [StringLength(120, ErrorMessage = "A referência deve ter no máximo {1} caracteres.")]
    public string Reference { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a latitude.")]
    [Range(-90d, 90d, ErrorMessage = "Informe uma latitude entre -90 e 90.")]
    public double? Latitude { get; set; }

    [Required(ErrorMessage = "Informe a longitude.")]
    [Range(-180d, 180d, ErrorMessage = "Informe uma longitude entre -180 e 180.")]
    public double? Longitude { get; set; }
}

/// <summary>Localização devolvida pela API: a da entrada, mais bairro, região, cidade e UF (plan 15.14).</summary>
public sealed record LocationData
{
    /// <summary>CEP no formato de exibição, <c>00000-000</c>.</summary>
    public string ZipCode { get; init; } = string.Empty;

    public string Street { get; init; } = string.Empty;

    public Guid NeighborhoodId { get; init; }

    public string NeighborhoodName { get; init; } = string.Empty;

    public Guid RegionId { get; init; }

    public string RegionName { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string State { get; init; } = string.Empty;

    public string Reference { get; init; } = string.Empty;

    public double Latitude { get; init; }

    public double Longitude { get; init; }
}

/// <summary>
/// Sugestão de endereço a partir do CEP (RF-004; D-52): rua, bairro, região, cidade e UF, ajustáveis no formulário
/// (cidade e UF não), e o ponto de referência que centraliza o mapa.
/// </summary>
public sealed record ZipCodeSuggestion
{
    /// <summary>CEP no formato de exibição, <c>00000-000</c>.</summary>
    public string ZipCode { get; init; } = string.Empty;

    public string Street { get; init; } = string.Empty;

    public Guid NeighborhoodId { get; init; }

    public string NeighborhoodName { get; init; } = string.Empty;

    public Guid RegionId { get; init; }

    public string RegionName { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string State { get; init; } = string.Empty;

    public double Latitude { get; init; }

    public double Longitude { get; init; }
}
