using Argos.Application.Common.Abstractions;
using Argos.Application.Common.Exceptions;
using Argos.Application.Inventory.Equipments;
using Argos.Application.Inventory.Support;
using Argos.Contracts.Inventory;
using Argos.Domain.Common;
using Argos.Domain.Inventory;
using Argos.Domain.Inventory.Geography;

namespace Argos.Application.Inventory.Geography;

/// <summary>
/// Cadastro de bairros (RF-020, RN-02, RN-22; plan 15.2.1): cada bairro pertence a exatamente uma região ativa e tem o
/// centro dentro do município. Nome único sem diferença de maiúsculas e acentos (D-53). Mudar a região de um bairro
/// fica no registro cadastral, com os nomes das regiões (D-49, D-50).
/// </summary>
public sealed class NeighborhoodService(
    INeighborhoodRepository neighborhoods,
    IRegionRepository regions,
    IEquipmentRepository equipments,
    IOperationalBaseRepository bases,
    MunicipalCalendar calendar,
    InventoryReferenceNames referenceNames,
    IInventoryQueries queries,
    IInventoryChangeLog changeLog,
    IUnitOfWork unitOfWork,
    ICurrentActor actor,
    TimeProvider timeProvider)
{
    private const string CenterField = "center";

    public Task<IReadOnlyList<NeighborhoodData>> ListAsync(
        Guid? regionId, bool includeInactive, CancellationToken cancellationToken = default) =>
        queries.ListNeighborhoodsAsync(regionId, includeInactive, cancellationToken);

    public async Task<NeighborhoodData> CreateAsync(NeighborhoodRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var region = await FindRegionAsync(request.RegionId, cancellationToken);
        var center = Center(request.Center);
        var neighborhood = Neighborhood.Create(
            request.Name, region, center, await calendar.GetMunicipalityAsync(cancellationToken));
        await EnsureUniqueNameAsync(neighborhood, cancellationToken);

        neighborhoods.Add(neighborhood);
        changeLog.Add(InventoryChange.Created(
            InventoryEntityType.Neighborhood, neighborhood.Id, timeProvider.GetUtcNow(), actor.Name));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAsync(neighborhood.Id, cancellationToken);
    }

    public async Task<NeighborhoodData> UpdateAsync(
        Guid id, NeighborhoodRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var neighborhood = await FindAsync(id, cancellationToken);
        var region = await FindRegionAsync(request.RegionId, cancellationToken);
        var center = Center(request.Center);
        var changes = neighborhood.Update(
            request.Name, region, center, await calendar.GetMunicipalityAsync(cancellationToken));

        if (changes.Count > 0)
        {
            await EnsureUniqueNameAsync(neighborhood, cancellationToken);
            changeLog.Add(InventoryChange.Updated(
                InventoryEntityType.Neighborhood,
                id,
                await referenceNames.ResolveAsync(changes, cancellationToken),
                timeProvider.GetUtcNow(),
                actor.Name));
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Inativa o bairro, recusada se houver equipamento ou base ativa nele (plan 15.2.1; agentes na Fase 3).</summary>
    public async Task InactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var neighborhood = await FindAsync(id, cancellationToken);
        var hasActiveDependents = await equipments.AnyActiveInNeighborhoodAsync(id, cancellationToken)
            || await bases.AnyActiveInNeighborhoodAsync(id, cancellationToken);
        var now = timeProvider.GetUtcNow();

        neighborhood.Inactivate(hasActiveDependents, now, actor.Name);
        changeLog.Add(InventoryChange.Inactivated(InventoryEntityType.Neighborhood, id, null, now, actor.Name));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>O centro escolhido no mapa, com os erros em <c>center</c>, <c>center.latitude</c> e <c>center.longitude</c>.</summary>
    private static GeoPoint Center(GeoPointInput? input) =>
        input is null
            ? throw new DomainValidationException(CenterField, "Escolha o ponto central do bairro.")
            : DomainFields.Under(CenterField, () => GeoPoint.Create(input.Latitude ?? double.NaN, input.Longitude ?? double.NaN));

    /// <summary>Região informada, ou nulo para que o Domínio aponte o campo (RN-22).</summary>
    private async Task<Region?> FindRegionAsync(Guid? regionId, CancellationToken cancellationToken) =>
        regionId is { } id
            ? await regions.FindByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Região não encontrada.")
            : null;

    private async Task<Neighborhood> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await neighborhoods.FindByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Bairro não encontrado.");

    private async Task<NeighborhoodData> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await queries.GetNeighborhoodAsync(id, cancellationToken) ?? throw new NotFoundException("Bairro não encontrado.");

    private async Task EnsureUniqueNameAsync(Neighborhood neighborhood, CancellationToken cancellationToken)
    {
        var existing = await neighborhoods.FindByNameKeyAsync(neighborhood.NameKey, cancellationToken);
        if (existing is not null && existing.Id != neighborhood.Id)
        {
            throw new ConflictException("Já existe um bairro com esse nome.");
        }
    }
}
