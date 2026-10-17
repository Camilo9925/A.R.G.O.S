using System.ComponentModel.DataAnnotations;

namespace Argos.Contracts.Access;

/// <summary>
/// Credenciais enviadas ao login (plan 14.7). As propriedades têm <c>set</c> porque o formulário de login da Web
/// as liga diretamente aos campos.
/// </summary>
public sealed record LoginRequest
{
    [Required(ErrorMessage = "Informe o usuário.")]
    [StringLength(50, ErrorMessage = "O usuário deve ter no máximo {1} caracteres.")]
    public string Login { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    [StringLength(128, ErrorMessage = "A senha deve ter no máximo {1} caracteres.")]
    public string Password { get; set; } = string.Empty;
}
