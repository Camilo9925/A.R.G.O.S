using Argos.Application.Common.Exceptions;
using Argos.Contracts.Inventory;

namespace Argos.Application.Inventory.Types;

/// <summary>
/// Consulta dos tipos de equipamento com as definições das características (RF-006, US-02; plan 15.5). A criação de
/// tipos personalizados (US-10) entra na Fase 3.
/// </summary>
public sealed class EquipmentTypeService(IInventoryQueries queries)
{
    public Task<IReadOnlyList<EquipmentTypeData>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default) =>
        queries.ListEquipmentTypesAsync(includeInactive, cancellationToken);

    public async Task<EquipmentTypeData> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await queries.GetEquipmentTypeAsync(id, cancellationToken)
            ?? throw new NotFoundException("Tipo de equipamento não encontrado.");
}
