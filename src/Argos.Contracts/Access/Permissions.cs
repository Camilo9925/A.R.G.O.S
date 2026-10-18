namespace Argos.Contracts.Access;

/// <summary>
/// Permissões de usuário por capacidade (RN-26, RN-27; plan 14.3, D-32). Cada nome é também o nome da política
/// de autorização na API e na Web. Os itens são exatamente os do enum <c>Permission</c> do Domínio, com o mesmo
/// nome; um teste unitário garante a paridade nos dois sentidos.
/// </summary>
public static class Permissions
{
    public const string OperationsView = "operations.view";
    public const string AlertsHandle = "alerts.handle";
    public const string ResourceStatusChange = "resources.status.change";
    public const string ResourceReactivate = "resources.reactivate";
    public const string AgentPositionsView = "agents.positions.view";
    public const string InventoryView = "inventory.view";
    public const string InventoryManage = "inventory.manage";
    public const string SettingsManage = "settings.manage";
    public const string PredictiveView = "predictive.view";
    public const string RecommendationsDecide = "recommendations.decide";
    public const string HistoryView = "history.view";

    public static IReadOnlyList<string> All { get; } =
    [
        OperationsView,
        AlertsHandle,
        ResourceStatusChange,
        ResourceReactivate,
        AgentPositionsView,
        InventoryView,
        InventoryManage,
        SettingsManage,
        PredictiveView,
        RecommendationsDecide,
        HistoryView,
    ];
}
