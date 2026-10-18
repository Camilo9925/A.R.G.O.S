using Argos.Application.Inventory.Types;
using Argos.Domain.Inventory.Types;
using Microsoft.EntityFrameworkCore;

namespace Argos.Infrastructure.Persistence.Repositories;

/// <summary>
/// O tipo vem sempre com as definições das características, na ordem de exibição: o Domínio valida e descreve os
/// valores nessa ordem (plan 15.2.5).
/// </summary>
internal sealed class EquipmentTypeRepository(ArgosDbContext dbContext) : IEquipmentTypeRepository
{
    public Task<EquipmentType?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        WithAttributes().SingleOrDefaultAsync(type => type.Id == id, cancellationToken);

    public Task<EquipmentType?> FindByKeyAsync(string key, CancellationToken cancellationToken = default) =>
        WithAttributes().SingleOrDefaultAsync(type => type.Key == key, cancellationToken);

    public void Add(EquipmentType type) => dbContext.EquipmentTypes.Add(type);

    private IQueryable<EquipmentType> WithAttributes() =>
        dbContext.EquipmentTypes.Include(type => type.Attributes.OrderBy(attribute => attribute.Order));
}
