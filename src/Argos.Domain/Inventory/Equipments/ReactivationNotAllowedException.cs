using Argos.Domain.Common;

namespace Argos.Domain.Inventory.Equipments;

/// <summary>
/// Reativação (Desativado → Operacional) pedida sem a permissão <c>resources.reactivate</c> (RN-05; D-47;
/// verificação 4 de plan 15.3). A Aplicação a traduz em <c>ForbiddenOperationException</c>, que vira 403 com o
/// motivo; o Domínio não conhece as exceções da Aplicação (Art. III.2).
/// </summary>
public sealed class ReactivationNotAllowedException()
    : DomainException("Somente o Administrador pode reativar um equipamento desativado.");
