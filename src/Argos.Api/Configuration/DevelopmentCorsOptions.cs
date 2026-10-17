using System.ComponentModel.DataAnnotations;

namespace Argos.Api.Configuration;

/// <summary>
/// Origem do servidor de desenvolvimento da Web, liberada por CORS só no ambiente Development (D-29).
/// </summary>
internal sealed class DevelopmentCorsOptions
{
    public const string SectionName = "DevelopmentCors";

    [Required(ErrorMessage = "Informe DevelopmentCors:WebOrigin com a origem do servidor de desenvolvimento da Web.")]
    [Url(ErrorMessage = "DevelopmentCors:WebOrigin deve ser uma URL válida.")]
    public string WebOrigin { get; set; } = string.Empty;
}
