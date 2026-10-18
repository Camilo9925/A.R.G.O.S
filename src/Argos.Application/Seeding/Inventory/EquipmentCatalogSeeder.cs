using Argos.Application.Common.Abstractions;
using Argos.Application.Inventory.Equipments;
using Argos.Application.Inventory.Geography;
using Argos.Application.Inventory.Support;
using Argos.Application.Inventory.Types;
using Argos.Domain.Common;
using Argos.Domain.Inventory.Equipments;
using Argos.Domain.Inventory.Geography;
using Argos.Domain.Inventory.Support;
using Argos.Domain.Inventory.Types;

namespace Argos.Application.Seeding.Inventory;

/// <summary>
/// Carga <c>inventory-catalog-v1</c> (D-56; plan 15.11.3, 15.11.4): os equipamentos, pelas mesmas operações do Domínio
/// dos casos de uso (RN-05, RN-07, RN-09 e RN-11 valem também na carga), com os instantes relativos à data-âncora e o
/// autor <c>simulação</c> nos registros de negócio (Art. IX.2). A carga é um ator de sistema: passa
/// <c>canReactivate = true</c> de forma explícita (D-47). Tudo, com o registro da versão, é gravado num só
/// <c>SaveChanges</c>; rodar de novo com a mesma semente não grava nada.
/// </summary>
public sealed class EquipmentCatalogSeeder(
    IMunicipalityRepository municipalities,
    INeighborhoodRepository neighborhoods,
    IOrganizationRepository organizations,
    IOperationalBaseRepository bases,
    IEquipmentTypeRepository types,
    IEquipmentRepository equipments,
    ISeedLedger ledger,
    IUnitOfWork unitOfWork)
{
    /// <param name="seed">Semente da simulação, registrada com a versão (plan 7.4).</param>
    /// <param name="anchor">Data-âncora da simulação (D-03): o histórico termina nela.</param>
    public async Task<SeedResult> SeedAsync(
        EquipmentCatalogSeed catalog, int seed, DateTimeOffset anchor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (!await InventorySeed.ShouldApplyAsync(ledger, InventorySeed.CatalogVersion, seed, cancellationToken))
        {
            return SeedResult.Skipped(InventorySeed.CatalogVersion);
        }

        var municipality = await municipalities.GetAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                $"Aplique a carga {InventorySeed.ReferenceVersion} antes de {InventorySeed.CatalogVersion}.");
        var anchorDate = municipality.DateOf(anchor);
        var references = new References(neighborhoods, organizations, bases, types);
        var created = 0;

        foreach (var item in catalog.Equipment)
        {
            var type = await references.TypeAsync(item.TypeKey, cancellationToken);
            var organization = await references.OrganizationAsync(item.OrganizationAcronym, cancellationToken);
            var location = item.Location is { } seedLocation
                ? Location.Create(
                    municipality,
                    await references.NeighborhoodAsync(seedLocation.NeighborhoodName, cancellationToken),
                    seedLocation.ZipCode,
                    seedLocation.Street,
                    seedLocation.Reference,
                    seedLocation.Latitude,
                    seedLocation.Longitude)
                : null;
            var operationalBase = item.BaseName is { } baseName
                ? await references.BaseAsync(organization, baseName, cancellationToken)
                : null;

            var registeredAt = anchor - item.RegisteredBeforeAnchor;
            var equipment = Equipment.Register(
                type,
                item.SerialNumber,
                item.Brand,
                item.Model,
                organization,
                location,
                operationalBase,
                item.Attributes,
                item.InitialStatus,
                registeredAt,
                InventorySeed.Author);

            if (item.PeriodicMaintenance is { } periodic)
            {
                equipment.RegisterMaintenance(
                    MaintenanceKind.Periodic,
                    anchorDate.AddDays(periodic.DueInDays),
                    periodic.PeriodicityDays,
                    periodic.Description,
                    municipality.DateOf(registeredAt),
                    registeredAt,
                    InventorySeed.Author);
            }

            if (item.StatusChange is { } change)
            {
                equipment.ChangeStatus(
                    change.Target, change.Justification, canReactivate: true, anchor - change.BeforeAnchor, InventorySeed.Author);
            }

            equipments.Add(equipment);
            created++;
        }

        ledger.Record(InventorySeed.CatalogVersion, seed);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SeedResult(InventorySeed.CatalogVersion, false, new Dictionary<string, int> { ["equipment"] = created });
    }

    /// <summary>Cadastros de referência pelo nome, lidos uma vez cada durante a carga.</summary>
    private sealed class References(
        INeighborhoodRepository neighborhoods,
        IOrganizationRepository organizations,
        IOperationalBaseRepository bases,
        IEquipmentTypeRepository types)
    {
        private readonly Dictionary<string, EquipmentType> _types = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Organization> _organizations = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Neighborhood> _neighborhoods = new(StringComparer.Ordinal);
        private readonly Dictionary<(Guid, string), OperationalBase> _bases = [];

        public Task<EquipmentType> TypeAsync(string key, CancellationToken cancellationToken) =>
            GetAsync(_types, key, () => types.FindByKeyAsync(key, cancellationToken), "Tipo");

        public Task<Organization> OrganizationAsync(string acronym, CancellationToken cancellationToken) =>
            GetAsync(
                _organizations,
                acronym,
                () => organizations.FindByAcronymKeyAsync(TextNormalization.Normalize(acronym), cancellationToken),
                "Órgão");

        public Task<Neighborhood> NeighborhoodAsync(string name, CancellationToken cancellationToken) =>
            GetAsync(
                _neighborhoods,
                name,
                () => neighborhoods.FindByNameKeyAsync(TextNormalization.Normalize(name), cancellationToken),
                "Bairro");

        public Task<OperationalBase> BaseAsync(Organization organization, string name, CancellationToken cancellationToken) =>
            GetAsync(
                _bases,
                (organization.Id, name),
                () => bases.FindByNameKeyAsync(organization.Id, TextNormalization.Normalize(name), cancellationToken),
                $"Base do órgão {organization.Acronym}");

        private static async Task<T> GetAsync<TKey, T>(
            Dictionary<TKey, T> cache, TKey key, Func<Task<T?>> find, string label)
            where TKey : notnull
            where T : class
        {
            if (!cache.TryGetValue(key, out var item))
            {
                item = await find()
                    ?? throw new InvalidOperationException($"{label} \"{key}\" não existe; aplique antes a carga {InventorySeed.ReferenceVersion}.");
                cache[key] = item;
            }

            return item;
        }
    }
}
