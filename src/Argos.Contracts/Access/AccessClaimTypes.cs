namespace Argos.Contracts.Access;

/// <summary>
/// Nomes das claims do usuário autenticado, no token da API e no estado de autenticação da Web (plan 14.6, 14.11).
/// </summary>
public static class AccessClaimTypes
{
    public const string Login = "login";
    public const string Name = "name";
    public const string Role = "role";

    /// <summary>Uma claim por permissão; usada só na Web, a partir da resposta do login (D-42).</summary>
    public const string Permission = "permission";
}
