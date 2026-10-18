using Argos.Contracts.Access;
using Argos.Domain.Access;

namespace Argos.Application.Access;

/// <summary>
/// Traduz a matriz do Domínio (<see cref="RolePermissions"/>) para os nomes de <c>Argos.Contracts</c>:
/// a API registra uma política por item e o login entrega à Web as permissões do perfil (D-32; plan 14.3).
/// </summary>
public static class AccessPolicyCatalog
{
    /// <summary>Uma política por permissão, com os perfis que a têm.</summary>
    public static IReadOnlyList<AccessPolicy> Policies { get; } =
        Enum.GetValues<Permission>()
            .Select(permission => new AccessPolicy(
                PermissionName(permission),
                RolePermissions.RolesWith(permission).Select(RoleName).ToArray()))
            .ToArray();

    /// <summary>Nomes das permissões do perfil, na ordem do enum <see cref="Permission"/>.</summary>
    public static IReadOnlyList<string> PermissionsFor(UserRole role) =>
        Enum.GetValues<Permission>()
            .Where(permission => RolePermissions.Grants(role, permission))
            .Select(PermissionName)
            .ToArray();

    /// <summary>Perfis (nomes de <see cref="UserRoles"/>) que têm a permissão de nome informado.</summary>
    public static IReadOnlyList<string> RolesWith(string permissionName) =>
        Policies.FirstOrDefault(policy => policy.Name == permissionName)?.Roles
        ?? throw new ArgumentOutOfRangeException(nameof(permissionName), permissionName, "Permissão desconhecida.");

    public static string PermissionName(Permission permission) => permission switch
    {
        Permission.OperationsView => Permissions.OperationsView,
        Permission.AlertsHandle => Permissions.AlertsHandle,
        Permission.ResourceStatusChange => Permissions.ResourceStatusChange,
        Permission.ResourceReactivate => Permissions.ResourceReactivate,
        Permission.AgentPositionsView => Permissions.AgentPositionsView,
        Permission.InventoryView => Permissions.InventoryView,
        Permission.InventoryManage => Permissions.InventoryManage,
        Permission.SettingsManage => Permissions.SettingsManage,
        Permission.PredictiveView => Permissions.PredictiveView,
        Permission.RecommendationsDecide => Permissions.RecommendationsDecide,
        Permission.HistoryView => Permissions.HistoryView,
        _ => throw new ArgumentOutOfRangeException(nameof(permission), permission, "Permissão desconhecida."),
    };

    public static string RoleName(UserRole role) => role switch
    {
        UserRole.Administrator => UserRoles.Administrator,
        UserRole.Operator => UserRoles.Operator,
        UserRole.Manager => UserRoles.Manager,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Perfil desconhecido."),
    };
}

/// <summary>Política nomeada pela permissão e os perfis (nomes de <see cref="UserRoles"/>) que a satisfazem.</summary>
public sealed record AccessPolicy(string Name, IReadOnlyList<string> Roles);
