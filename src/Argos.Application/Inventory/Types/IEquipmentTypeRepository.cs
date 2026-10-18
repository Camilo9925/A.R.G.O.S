using Argos.Domain.Inventory.Types;

namespace Argos.Application.Inventory.Types;

/// <summary>Porta de persistência dos tipos de equipamento, com as definições das características (plan 15.12).</summary>
public interface IEquipmentTypeRepository
{
    Task<EquipmentType?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Tipo pela chave estável, como <c>traffic-radar</c>.</summary>
    Task<EquipmentType?> FindByKeyAsync(string key, CancellationToken cancellationToken = default);

    void Add(EquipmentType type);
}
