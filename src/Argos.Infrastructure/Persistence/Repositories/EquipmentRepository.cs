using Argos.Application.Inventory.Equipments;
using Argos.Domain.Inventory.Equipments;
using Argos.Infrastructure.Persistence.Configurations.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Argos.Infrastructure.Persistence.Repositories;

/// <summary>
/// O agregado vem com as manutenções abertas, as que as regras usam (plan 15.12); o histórico de situação não é
/// carregado e só recebe registros novos.
/// </summary>
internal sealed class EquipmentRepository(ArgosDbContext dbContext) : IEquipmentRepository
{
    public Task<Equipment?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Equipment
            .Include(equipment => equipment.Maintenances.Where(maintenance => maintenance.State == MaintenanceState.Open))
            .SingleOrDefaultAsync(equipment => equipment.Id == id, cancellationToken);

    public Task<Guid?> FindIdByBrandAndSerialAsync(
        string brandKey, string serialKey, CancellationToken cancellationToken = default) =>
        dbContext.Equipment
            .Where(equipment => equipment.BrandKey == brandKey && equipment.SerialKey == serialKey)
            .Select(equipment => (Guid?)equipment.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> MaintenanceExistsAsync(
        Guid equipmentId, Guid maintenanceId, CancellationToken cancellationToken = default) =>
        dbContext.Maintenances.AnyAsync(
            maintenance => maintenance.Id == maintenanceId && maintenance.EquipmentId == equipmentId, cancellationToken);

    public Task<bool> AnyActiveInNeighborhoodAsync(Guid neighborhoodId, CancellationToken cancellationToken = default) =>
        dbContext.Equipment.AnyAsync(
            equipment => equipment.IsActive && equipment.Location!.NeighborhoodId == neighborhoodId, cancellationToken);

    public Task<bool> AnyActiveInBaseAsync(Guid baseId, CancellationToken cancellationToken = default) =>
        dbContext.Equipment.AnyAsync(equipment => equipment.IsActive && equipment.BaseId == baseId, cancellationToken);

    /// <summary>O <c>xmin</c> lido com o agregado, que também é o usado na gravação (D-60).</summary>
    public uint VersionOf(Equipment equipment) =>
        dbContext.Entry(equipment).Property<uint>(EquipmentConfiguration.VersionProperty).OriginalValue;

    public void Add(Equipment equipment) => dbContext.Equipment.Add(equipment);
}
