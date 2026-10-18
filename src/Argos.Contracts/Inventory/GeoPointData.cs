using System.ComponentModel.DataAnnotations;

namespace Argos.Contracts.Inventory;

/// <summary>Coordenada devolvida pela API, em graus decimais.</summary>
public sealed record GeoPointData(double Latitude, double Longitude);

/// <summary>
/// Coordenada informada num formulário, como o centro de um bairro (RF-005). Estar dentro do município (RN-02) é
/// verificado pelo Domínio.
/// </summary>
public sealed record GeoPointInput
{
    [Required(ErrorMessage = "Informe a latitude.")]
    [Range(-90d, 90d, ErrorMessage = "Informe uma latitude entre -90 e 90.")]
    public double? Latitude { get; set; }

    [Required(ErrorMessage = "Informe a longitude.")]
    [Range(-180d, 180d, ErrorMessage = "Informe uma longitude entre -180 e 180.")]
    public double? Longitude { get; set; }
}
