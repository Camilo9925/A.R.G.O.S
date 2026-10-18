using Argos.Application.Common.Abstractions;
using Argos.Application.Inventory;
using Argos.Application.Inventory.Geography;
using Argos.Application.Inventory.Support;
using Argos.Application.Inventory.Types;
using Argos.Domain.Inventory;
using Argos.Domain.Inventory.Geography;
using Argos.Domain.Inventory.Support;
using Argos.Domain.Inventory.Types;

namespace Argos.Application.Seeding.Inventory;

/// <summary>
/// Carga <c>inventory-reference-v1</c> (D-56; plan 15.11.2, 15.11.4): município, zonas, bairros, CEPs, órgãos, bases e
/// os dez tipos do catálogo, criados pelas operações do Domínio (RN-02, RN-03 e RN-22 valem também aqui). Tudo, com o
/// registro da versão, é gravado num só <c>SaveChanges</c>; rodar de novo com a mesma semente não grava nada.
/// </summary>
public sealed class InventoryReferenceSeeder(
    IMunicipalityRepository municipalities,
    IRegionRepository regions,
    INeighborhoodRepository neighborhoods,
    IZipCodeRepository zipCodes,
    IOrganizationRepository organizations,
    IOperationalBaseRepository bases,
    IEquipmentTypeRepository types,
    IInventoryChangeLog changeLog,
    ISeedLedger ledger,
    IUnitOfWork unitOfWork)
{
    /// <param name="seed">Semente da simulação, registrada com a versão (plan 7.4).</param>
    /// <param name="registeredAt">
    /// Instante de negócio do cadastro dos registros, no registro cadastral com o autor <c>simulação</c>; relativo à
    /// data-âncora e anterior ao catálogo de equipamentos.
    /// </param>
    public async Task<SeedResult> SeedAsync(
        InventoryReferenceSeed data, int seed, DateTimeOffset registeredAt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (!await InventorySeed.ShouldApplyAsync(ledger, InventorySeed.ReferenceVersion, seed, cancellationToken))
        {
            return SeedResult.Skipped(InventorySeed.ReferenceVersion);
        }

        var municipality = Municipality.Create(
            data.Municipality.Name,
            data.Municipality.State,
            data.Municipality.TimeZoneId,
            GeoPolygon.FromRing(data.Municipality.Boundary.Select(Point)),
            Point(data.Municipality.Center));
        municipalities.Add(municipality);

        var regionsByName = new Dictionary<string, Region>(StringComparer.Ordinal);
        foreach (var regionSeed in data.Regions)
        {
            var region = Region.Create(regionSeed.Name, GeoPolygon.FromRing(regionSeed.Boundary.Select(Point)));
            regionsByName.Add(region.Name, region);
            regions.Add(region);
            Created(InventoryEntityType.Region, region.Id);
        }

        var neighborhoodsByName = new Dictionary<string, Neighborhood>(StringComparer.Ordinal);
        foreach (var neighborhoodSeed in data.Neighborhoods)
        {
            var neighborhood = Neighborhood.Create(
                neighborhoodSeed.Name,
                Lookup(regionsByName, neighborhoodSeed.RegionName, "Região"),
                Point(neighborhoodSeed.Center),
                municipality);
            neighborhoodsByName.Add(neighborhood.Name, neighborhood);
            neighborhoods.Add(neighborhood);
            Created(InventoryEntityType.Neighborhood, neighborhood.Id);
        }

        foreach (var zipCodeSeed in data.ZipCodes)
        {
            zipCodes.Add(ZipCodeEntry.Create(
                ZipCode.Parse(zipCodeSeed.ZipCode),
                zipCodeSeed.Street,
                Lookup(neighborhoodsByName, zipCodeSeed.NeighborhoodName, "Bairro"),
                Point(zipCodeSeed.ReferencePoint),
                municipality));
        }

        var organizationsByAcronym = new Dictionary<string, Organization>(StringComparer.Ordinal);
        foreach (var organizationSeed in data.Organizations)
        {
            var organization = Organization.Create(organizationSeed.Name, organizationSeed.Acronym);
            organizationsByAcronym.Add(organization.Acronym, organization);
            organizations.Add(organization);
            Created(InventoryEntityType.Organization, organization.Id);
        }

        foreach (var baseSeed in data.Bases)
        {
            var location = baseSeed.Location;
            var operationalBase = OperationalBase.Create(
                baseSeed.Name,
                Lookup(organizationsByAcronym, baseSeed.OrganizationAcronym, "Órgão"),
                Location.Create(
                    municipality,
                    Lookup(neighborhoodsByName, location.NeighborhoodName, "Bairro"),
                    location.ZipCode,
                    location.Street,
                    location.Reference,
                    location.Latitude,
                    location.Longitude));
            bases.Add(operationalBase);
            Created(InventoryEntityType.OperationalBase, operationalBase.Id);
        }

        var catalog = EquipmentTypeCatalog.CreateAll();
        foreach (var type in catalog)
        {
            types.Add(type);
            Created(InventoryEntityType.EquipmentType, type.Id);
        }

        ledger.Record(InventorySeed.ReferenceVersion, seed);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SeedResult(InventorySeed.ReferenceVersion, false, new Dictionary<string, int>
        {
            ["regions"] = data.Regions.Count,
            ["neighborhoods"] = data.Neighborhoods.Count,
            ["zipCodes"] = data.ZipCodes.Count,
            ["organizations"] = data.Organizations.Count,
            ["bases"] = data.Bases.Count,
            ["equipmentTypes"] = catalog.Count,
        });

        void Created(InventoryEntityType entityType, Guid id) =>
            changeLog.Add(InventoryChange.Created(entityType, id, registeredAt, InventorySeed.Author));
    }

    private static GeoPoint Point(SeedPoint point) => GeoPoint.Create(point.Latitude, point.Longitude);

    internal static T Lookup<T>(IReadOnlyDictionary<string, T> items, string name, string label) =>
        items.TryGetValue(name, out var item)
            ? item
            : throw new InvalidOperationException($"{label} \"{name}\" não existe na carga do Inventário.");
}
