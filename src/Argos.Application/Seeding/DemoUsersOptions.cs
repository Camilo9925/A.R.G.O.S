using System.ComponentModel.DataAnnotations;

namespace Argos.Application.Seeding;

/// <summary>
/// Os três usuários de demonstração, um por perfil (RF-053; D-40; plan 14.10). Logins e senhas vêm do <c>.env</c>;
/// os nomes, do <c>appsettings.json</c> do Migrator.
/// </summary>
public sealed class DemoUsersOptions
{
    public const string SectionName = "DemoUsers";

    [Required(ErrorMessage = "Configure DemoUsers:Administrator.")]
    public DemoUserOptions? Administrator { get; set; }

    [Required(ErrorMessage = "Configure DemoUsers:Operator.")]
    public DemoUserOptions? Operator { get; set; }

    [Required(ErrorMessage = "Configure DemoUsers:Manager.")]
    public DemoUserOptions? Manager { get; set; }
}

/// <summary>
/// Um usuário de demonstração. O conjunto de caracteres é restrito para dispensar escapes no JSON gerado pelo nginx (D-40).
/// </summary>
public sealed class DemoUserOptions
{
    [Required(ErrorMessage = "Informe o login.")]
    [RegularExpression("^[a-z0-9._-]{3,50}$",
        ErrorMessage = "O login deve ter de 3 a 50 caracteres: letras minúsculas sem acento, números, ponto, hífen ou sublinhado.")]
    public string Login { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "O nome deve ter entre {2} e {1} caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    [RegularExpression("^[A-Za-z0-9._-]{8,64}$",
        ErrorMessage = "A senha deve ter de 8 a 64 caracteres: letras sem acento, números, ponto, hífen ou sublinhado.")]
    public string Password { get; set; } = string.Empty;
}
