using Argos.Application.Common.Abstractions;
using Argos.Domain.Access;

namespace Argos.Application.Inventory.Equipments;

/// <summary>
/// O que o autor da requisição pode fazer com a situação de um equipamento, pela matriz do Domínio (RN-26; D-47).
/// Um ator sem perfil (<see cref="ICurrentActor.Role"/> nulo: sistema ou PROMETHEUS) não ganha nenhuma das duas; a
/// carga inicial passa <c>canReactivate = true</c> de forma explícita, sem passar por aqui.
/// </summary>
public static class EquipmentPermissions
{
    /// <summary>RN-05: reativar (Desativado → Operacional) exige <c>resources.reactivate</c>.</summary>
    public static bool CanReactivate(ICurrentActor actor) => Grants(actor, Permission.ResourceReactivate);

    /// <summary>US-04: alterar a situação exige <c>resources.status.change</c>.</summary>
    public static bool CanChangeStatus(ICurrentActor actor) => Grants(actor, Permission.ResourceStatusChange);

    private static bool Grants(ICurrentActor actor, Permission permission)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return actor.Role is { } role && RolePermissions.Grants(role, permission);
    }
}
