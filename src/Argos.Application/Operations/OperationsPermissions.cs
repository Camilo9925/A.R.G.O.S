using Argos.Application.Common.Abstractions;
using Argos.Domain.Access;

namespace Argos.Application.Operations;

/// <summary>
/// O que o autor da requisição vê e faz na Central, pela matriz do Domínio (RN-26, RN-27; D-59). Um ator sem perfil
/// (sistema ou PROMETHEUS) não ganha nenhuma das duas: a permissão nunca é inferida da falta de perfil.
/// </summary>
public static class OperationsPermissions
{
    /// <summary>RN-27: posição individual de agentes (e de viaturas e motos com tripulação, A-38).</summary>
    public static bool CanSeeAgentPositions(ICurrentActor actor) => Grants(actor, Permission.AgentPositionsView);

    /// <summary>US-15: reconhecer e resolver alertas.</summary>
    public static bool CanHandleAlerts(ICurrentActor actor) => Grants(actor, Permission.AlertsHandle);

    private static bool Grants(ICurrentActor actor, Permission permission)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return actor.Role is { } role && RolePermissions.Grants(role, permission);
    }
}
