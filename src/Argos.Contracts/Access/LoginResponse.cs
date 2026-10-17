namespace Argos.Contracts.Access;

/// <summary>
/// Resultado do login (plan 14.7): o token de acesso, sua expiração, o usuário e as permissões do seu perfil.
/// A Web guarda esta resposta como sessão e decide o que exibir pela lista de permissões (D-42).
/// </summary>
public sealed record LoginResponse
{
    public string AccessToken { get; init; } = string.Empty;

    public string TokenType { get; init; } = "Bearer";

    /// <summary>Instante UTC em que o token deixa de valer (D-09: 8 h, sem refresh).</summary>
    public DateTimeOffset ExpiresAt { get; init; }

    public AuthenticatedUser User { get; init; } = new();

    /// <summary>Nomes de <see cref="Permissions"/> concedidos ao perfil do usuário (RN-26).</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];
}
