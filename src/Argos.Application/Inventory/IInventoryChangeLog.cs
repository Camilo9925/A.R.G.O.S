using Argos.Domain.Inventory;

namespace Argos.Application.Inventory;

/// <summary>
/// Registro append-only das alterações cadastrais do Inventário (Art. V.1, VIII.4; D-49; plan 15.2.11). Os registros
/// entram na unidade de trabalho do caso de uso e são gravados no mesmo <c>SaveChanges</c>.
/// </summary>
public interface IInventoryChangeLog
{
    void Add(InventoryChange change);
}
