using Argos.Contracts.Access;

namespace Argos.IntegrationTests.Access;

/// <summary>
/// Matriz perfil × permissão da RN-26 (spec v1.1.3), escrita à mão a partir da tabela de plan 14.3, e não lida do
/// catálogo: assim os testes conferem a API contra a regra, e não a API contra ela mesma.
/// </summary>
internal static class Rn26Matrix
{
    private static readonly Dictionary<string, string[]> RolesByPermission = new()
    {
        [Permissions.OperationsView] = [UserRoles.Administrator, UserRoles.Operator, UserRoles.Manager],
        [Permissions.AlertsHandle] = [UserRoles.Administrator, UserRoles.Operator],
        [Permissions.ResourceStatusChange] = [UserRoles.Administrator, UserRoles.Operator],
        [Permissions.ResourceReactivate] = [UserRoles.Administrator],
        [Permissions.AgentPositionsView] = [UserRoles.Administrator, UserRoles.Operator],
        [Permissions.InventoryView] = [UserRoles.Administrator, UserRoles.Manager],
        [Permissions.InventoryManage] = [UserRoles.Administrator],
        [Permissions.SettingsManage] = [UserRoles.Administrator],
        [Permissions.PredictiveView] = [UserRoles.Administrator, UserRoles.Manager],
        [Permissions.RecommendationsDecide] = [UserRoles.Administrator, UserRoles.Manager],
        [Permissions.HistoryView] = [UserRoles.Administrator, UserRoles.Operator, UserRoles.Manager],
    };

    public static IReadOnlyCollection<string> AllPermissions => RolesByPermission.Keys;

    public static bool Grants(string role, string permission) => RolesByPermission[permission].Contains(role);

    /// <summary>Permissões do perfil, na ordem da tabela de plan 14.3.</summary>
    public static IReadOnlyList<string> PermissionsFor(string role) =>
        RolesByPermission.Where(entry => entry.Value.Contains(role)).Select(entry => entry.Key).ToArray();

    /// <summary>Cada perfil × cada permissão, com o resultado esperado.</summary>
    public static TheoryData<string, string, bool> Cases()
    {
        var cases = new TheoryData<string, string, bool>();
        foreach (var role in UserRoles.All)
        {
            foreach (var permission in RolesByPermission.Keys)
            {
                cases.Add(role, permission, Grants(role, permission));
            }
        }

        return cases;
    }
}
