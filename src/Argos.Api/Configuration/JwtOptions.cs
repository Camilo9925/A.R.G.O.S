using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Argos.Api.Configuration;

/// <summary>
/// Emissão e validação do token de acesso (D-09, D-35; plan 14.6). A chave de assinatura é segredo:
/// vem do <c>.env</c> no Compose ou de user-secrets em desenvolvimento, nunca de arquivo versionado (Art. III.9).
/// </summary>
internal sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required(ErrorMessage = "Informe Jwt:Issuer.")]
    public string Issuer { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe Jwt:Audience.")]
    public string Audience { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe Jwt:SigningKey (ARGOS_JWT_SIGNING_KEY no .env, ou user-secrets em desenvolvimento).")]
    [MinLength(32, ErrorMessage = "Jwt:SigningKey deve ter no mínimo {1} caracteres.")]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 8, ErrorMessage = "Jwt:LifetimeHours deve estar entre {1} e {2}.")]
    public int LifetimeHours { get; set; } = 8;

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}
