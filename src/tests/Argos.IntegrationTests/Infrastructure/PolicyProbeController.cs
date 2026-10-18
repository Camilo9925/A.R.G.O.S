using Argos.Contracts.Access;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Argos.IntegrationTests.Infrastructure;

/// <summary>
/// Sonda de políticas (D-44; plan 14.12): uma ação por política do catálogo, para verificar por HTTP a matriz da
/// RN-26 registrada na API antes de existirem os endpoints de negócio. Existe só neste projeto e entra na API em teste
/// como <c>ApplicationPart</c> (<see cref="ApiFixture"/>). Cada ação responde <c>204</c> quando a política é satisfeita.
/// </summary>
[ApiController]
[Route(RoutePrefix)]
public sealed class PolicyProbeController : ControllerBase
{
    private const string RoutePrefix = "api/test/policy-probe";

    /// <summary>Caminho da ação que exige a política de nome informado.</summary>
    public static string PathFor(string policy) => $"/{RoutePrefix}/{policy}";

    [HttpGet(Permissions.OperationsView)]
    [Authorize(Policy = Permissions.OperationsView)]
    public IActionResult OperationsView() => NoContent();

    [HttpGet(Permissions.AlertsHandle)]
    [Authorize(Policy = Permissions.AlertsHandle)]
    public IActionResult AlertsHandle() => NoContent();

    [HttpGet(Permissions.ResourceStatusChange)]
    [Authorize(Policy = Permissions.ResourceStatusChange)]
    public IActionResult ResourceStatusChange() => NoContent();

    [HttpGet(Permissions.ResourceReactivate)]
    [Authorize(Policy = Permissions.ResourceReactivate)]
    public IActionResult ResourceReactivate() => NoContent();

    [HttpGet(Permissions.AgentPositionsView)]
    [Authorize(Policy = Permissions.AgentPositionsView)]
    public IActionResult AgentPositionsView() => NoContent();

    [HttpGet(Permissions.InventoryView)]
    [Authorize(Policy = Permissions.InventoryView)]
    public IActionResult InventoryView() => NoContent();

    [HttpGet(Permissions.InventoryManage)]
    [Authorize(Policy = Permissions.InventoryManage)]
    public IActionResult InventoryManage() => NoContent();

    [HttpGet(Permissions.SettingsManage)]
    [Authorize(Policy = Permissions.SettingsManage)]
    public IActionResult SettingsManage() => NoContent();

    [HttpGet(Permissions.PredictiveView)]
    [Authorize(Policy = Permissions.PredictiveView)]
    public IActionResult PredictiveView() => NoContent();

    [HttpGet(Permissions.RecommendationsDecide)]
    [Authorize(Policy = Permissions.RecommendationsDecide)]
    public IActionResult RecommendationsDecide() => NoContent();

    [HttpGet(Permissions.HistoryView)]
    [Authorize(Policy = Permissions.HistoryView)]
    public IActionResult HistoryView() => NoContent();
}
