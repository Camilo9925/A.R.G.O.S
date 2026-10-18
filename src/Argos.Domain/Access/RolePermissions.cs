namespace Argos.Domain.Access;

/// <summary>
/// Matriz perfil × permissão da RN-26 (spec v1.1.3), incluindo a reativação só do Administrador (RN-05, D-47) e o
/// recorte da RN-27 sobre a posição de agentes.
/// É a única fonte dessa regra: API e Web a recebem já traduzida pela Aplicação (D-32).
/// </summary>
public static class RolePermissions
{
    private static readonly IReadOnlyDictionary<UserRole, IReadOnlySet<Permission>> Matrix =
        new Dictionary<UserRole, IReadOnlySet<Permission>>
        {
            // Administrador: todos os cadastros, tipos e parâmetros, a reativação de equipamento desativado (D-47),
            // além de tudo que os demais perfis fazem.
            [UserRole.Administrator] = new HashSet<Permission>(Enum.GetValues<Permission>()),

            // Operador: Central, alertas, situação de equipamentos e agentes, histórico e posição de agentes (RN-27).
            [UserRole.Operator] = new HashSet<Permission>
            {
                Permission.OperationsView,
                Permission.AlertsHandle,
                Permission.ResourceStatusChange,
                Permission.AgentPositionsView,
                Permission.HistoryView,
            },

            // Gestor: Central, inventário, histórico, indicadores e previsões, e decide sobre recomendações.
            // Não vê posições individuais de agentes, só quantidades por região (RN-27).
            [UserRole.Manager] = new HashSet<Permission>
            {
                Permission.OperationsView,
                Permission.InventoryView,
                Permission.HistoryView,
                Permission.PredictiveView,
                Permission.RecommendationsDecide,
            },
        };

    /// <summary>Permissões concedidas ao perfil.</summary>
    public static IReadOnlySet<Permission> For(UserRole role) =>
        Matrix.TryGetValue(role, out var permissions)
            ? permissions
            : throw new ArgumentOutOfRangeException(nameof(role), role, "Perfil desconhecido.");

    /// <summary>Indica se o perfil tem a permissão.</summary>
    public static bool Grants(UserRole role, Permission permission) => For(role).Contains(permission);

    /// <summary>Perfis que têm a permissão, na ordem do enum <see cref="UserRole"/>.</summary>
    public static IReadOnlyList<UserRole> RolesWith(Permission permission) =>
        Enum.GetValues<UserRole>().Where(role => Grants(role, permission)).ToArray();
}
