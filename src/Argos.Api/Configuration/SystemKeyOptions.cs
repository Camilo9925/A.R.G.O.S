using System.ComponentModel.DataAnnotations;

namespace Argos.Api.Configuration;

/// <summary>
/// Chave de sistema com que o PROMETHEUS (Simulador) se autentica (D-34; plan 14.6). Segredo, como a chave do JWT.
/// </summary>
internal sealed class SystemKeyOptions
{
    public const string SectionName = "SystemKey";

    [Required(ErrorMessage = "Informe SystemKey:Key (ARGOS_SYSTEM_KEY no .env, ou user-secrets em desenvolvimento).")]
    [MinLength(32, ErrorMessage = "SystemKey:Key deve ter no mínimo {1} caracteres.")]
    public string Key { get; set; } = string.Empty;
}
