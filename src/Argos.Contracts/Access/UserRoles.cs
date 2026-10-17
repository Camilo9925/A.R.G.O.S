namespace Argos.Contracts.Access;

/// <summary>
/// Perfis de usuário (RF-022), como aparecem no token e nos contratos. A exibição em pt-BR fica na Web.
/// </summary>
public static class UserRoles
{
    public const string Administrator = "Administrator";
    public const string Operator = "Operator";
    public const string Manager = "Manager";

    public static IReadOnlyList<string> All { get; } = [Administrator, Operator, Manager];
}
