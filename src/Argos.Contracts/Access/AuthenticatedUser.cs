namespace Argos.Contracts.Access;

/// <summary>
/// Usuário autenticado, como a Web o exibe (plan 14.7). Nunca contém senha nem hash.
/// </summary>
public sealed record AuthenticatedUser
{
    public string Login { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary>Perfil, um dos valores de <see cref="UserRoles"/>.</summary>
    public string Role { get; init; } = string.Empty;
}
