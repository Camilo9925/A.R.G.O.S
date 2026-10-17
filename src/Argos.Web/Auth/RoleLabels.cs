using Argos.Contracts.Access;

namespace Argos.Web.Auth;

/// <summary>
/// Nome dos perfis em pt-BR, só para exibição (Art. X.1, XIII.2).
/// </summary>
public static class RoleLabels
{
    public static string For(string? role) => role switch
    {
        UserRoles.Administrator => "Administrador",
        UserRoles.Operator => "Operador",
        UserRoles.Manager => "Gestor",
        _ => "Perfil desconhecido",
    };
}
