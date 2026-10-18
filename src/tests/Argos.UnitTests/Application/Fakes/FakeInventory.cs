using Argos.Application.Common.Abstractions;
using Argos.Application.Inventory;
using Argos.Application.Inventory.Equipments;
using Argos.Application.Inventory.Geography;
using Argos.Application.Inventory.Support;
using Argos.Application.Inventory.Types;
using Argos.Domain.Common.Parameters;
using Argos.Domain.Inventory;
using Argos.Domain.Inventory.Equipments;
using Argos.Domain.Inventory.Geography;
using Argos.Domain.Inventory.Support;
using Argos.Domain.Inventory.Types;

namespace Argos.UnitTests.Application.Fakes;

/// <summary>
/// Inventário em memória: implementa as portas de escrita do Inventário, o registro cadastral, os parâmetros de
/// negócio e o histórico do seed sobre listas compartilhadas, para que as verificações entre cadastros (dependentes
/// ativos, nomes de referência) enxerguem os mesmos dados. Diferente do EF Core, o que é acrescentado fica visível na
/// hora; os testes conferem a gravação pelas chamadas de <see cref="UnitOfWork"/>.
/// </summary>
internal sealed class FakeInventory :
    IMunicipalityRepository,
    IRegionRepository,
    INeighborhoodRepository,
    IZipCodeRepository,
    IOrganizationRepository,
    IOperationalBaseRepository,
    IEquipmentTypeRepository,
    IEquipmentRepository,
    IInventoryChangeLog,
    IBusinessParameters,
    ISeedLedger
{
    public List<Municipality> Municipalities { get; } = [];

    public List<Region> Regions { get; } = [];

    public List<Neighborhood> Neighborhoods { get; } = [];

    public List<ZipCodeEntry> ZipCodes { get; } = [];

    public List<Organization> Organizations { get; } = [];

    public List<OperationalBase> Bases { get; } = [];

    public List<EquipmentType> Types { get; } = [];

    public List<Equipment> Equipment { get; } = [];

    public List<InventoryChange> Changes { get; } = [];

    /// <summary>Versão de cada equipamento (D-60); 1 se o teste não definir outra.</summary>
    public Dictionary<Guid, uint> EquipmentVersions { get; } = [];

    public Dictionary<BusinessParameter, int> Parameters { get; } = [];

    public Dictionary<string, int> Ledger { get; } = new(StringComparer.Ordinal);

    public FakeUnitOfWork UnitOfWork { get; } = new();

    // Município

    Task<Municipality?> IMunicipalityRepository.GetAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Municipalities.SingleOrDefault());

    void IMunicipalityRepository.Add(Municipality municipality) => Municipalities.Add(municipality);

    // Regiões

    Task<Region?> IRegionRepository.FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Regions.SingleOrDefault(region => region.Id == id));

    Task<Region?> IRegionRepository.FindByNameKeyAsync(string nameKey, CancellationToken cancellationToken) =>
        Task.FromResult(Regions.SingleOrDefault(region => region.NameKey == nameKey));

    void IRegionRepository.Add(Region region) => Regions.Add(region);

    // Bairros

    Task<Neighborhood?> INeighborhoodRepository.FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Neighborhoods.SingleOrDefault(neighborhood => neighborhood.Id == id));

    Task<Neighborhood?> INeighborhoodRepository.FindByNameKeyAsync(string nameKey, CancellationToken cancellationToken) =>
        Task.FromResult(Neighborhoods.SingleOrDefault(neighborhood => neighborhood.NameKey == nameKey));

    Task<bool> INeighborhoodRepository.AnyActiveInRegionAsync(Guid regionId, CancellationToken cancellationToken) =>
        Task.FromResult(Neighborhoods.Any(neighborhood => neighborhood.IsActive && neighborhood.RegionId == regionId));

    void INeighborhoodRepository.Add(Neighborhood neighborhood) => Neighborhoods.Add(neighborhood);

    // CEPs

    Task<ZipCodeEntry?> IZipCodeRepository.FindAsync(ZipCode zipCode, CancellationToken cancellationToken) =>
        Task.FromResult(ZipCodes.SingleOrDefault(entry => entry.ZipCode == zipCode));

    void IZipCodeRepository.Add(ZipCodeEntry entry) => ZipCodes.Add(entry);

    // Órgãos

    Task<Organization?> IOrganizationRepository.FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Organizations.SingleOrDefault(organization => organization.Id == id));

    Task<Organization?> IOrganizationRepository.FindByNameKeyAsync(string nameKey, CancellationToken cancellationToken) =>
        Task.FromResult(Organizations.SingleOrDefault(organization => organization.NameKey == nameKey));

    Task<Organization?> IOrganizationRepository.FindByAcronymKeyAsync(string acronymKey, CancellationToken cancellationToken) =>
        Task.FromResult(Organizations.SingleOrDefault(organization => organization.AcronymKey == acronymKey));

    void IOrganizationRepository.Add(Organization organization) => Organizations.Add(organization);

    // Bases

    Task<OperationalBase?> IOperationalBaseRepository.FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Bases.SingleOrDefault(operationalBase => operationalBase.Id == id));

    Task<OperationalBase?> IOperationalBaseRepository.FindByNameKeyAsync(
        Guid organizationId, string nameKey, CancellationToken cancellationToken) =>
        Task.FromResult(Bases.SingleOrDefault(
            operationalBase => operationalBase.OrganizationId == organizationId && operationalBase.NameKey == nameKey));

    Task<bool> IOperationalBaseRepository.AnyActiveInNeighborhoodAsync(Guid neighborhoodId, CancellationToken cancellationToken) =>
        Task.FromResult(Bases.Any(operationalBase =>
            operationalBase.IsActive && operationalBase.Location.NeighborhoodId == neighborhoodId));

    void IOperationalBaseRepository.Add(OperationalBase operationalBase) => Bases.Add(operationalBase);

    // Tipos

    Task<EquipmentType?> IEquipmentTypeRepository.FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Types.SingleOrDefault(type => type.Id == id));

    Task<EquipmentType?> IEquipmentTypeRepository.FindByKeyAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult(Types.SingleOrDefault(type => type.Key == key));

    void IEquipmentTypeRepository.Add(EquipmentType type) => Types.Add(type);

    // Equipamentos

    Task<Equipment?> IEquipmentRepository.FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Equipment.SingleOrDefault(equipment => equipment.Id == id));

    Task<Guid?> IEquipmentRepository.FindIdByBrandAndSerialAsync(
        string brandKey, string serialKey, CancellationToken cancellationToken) =>
        Task.FromResult(Equipment
            .Where(equipment => equipment.BrandKey == brandKey && equipment.SerialKey == serialKey)
            .Select(equipment => (Guid?)equipment.Id)
            .FirstOrDefault());

    Task<bool> IEquipmentRepository.MaintenanceExistsAsync(
        Guid equipmentId, Guid maintenanceId, CancellationToken cancellationToken) =>
        Task.FromResult(Equipment.Any(equipment =>
            equipment.Id == equipmentId && equipment.Maintenances.Any(maintenance => maintenance.Id == maintenanceId)));

    Task<bool> IEquipmentRepository.AnyActiveInNeighborhoodAsync(Guid neighborhoodId, CancellationToken cancellationToken) =>
        Task.FromResult(Equipment.Any(equipment => equipment.IsActive && equipment.Location?.NeighborhoodId == neighborhoodId));

    Task<bool> IEquipmentRepository.AnyActiveInBaseAsync(Guid baseId, CancellationToken cancellationToken) =>
        Task.FromResult(Equipment.Any(equipment => equipment.IsActive && equipment.BaseId == baseId));

    uint IEquipmentRepository.VersionOf(Equipment equipment) => VersionOf(equipment.Id);

    void IEquipmentRepository.Add(Equipment equipment) => Equipment.Add(equipment);

    public uint VersionOf(Guid equipmentId) => EquipmentVersions.GetValueOrDefault(equipmentId, 1u);

    // Registro cadastral

    void IInventoryChangeLog.Add(InventoryChange change) => Changes.Add(change);

    // Parâmetros de negócio

    Task<int> IBusinessParameters.GetAsync(BusinessParameter parameter, CancellationToken cancellationToken) =>
        Task.FromResult(Parameters.TryGetValue(parameter, out var value) ? value : BusinessParameterCatalog.Get(parameter).Default);

    Task<IReadOnlyCollection<BusinessParameter>> IBusinessParameters.ListStoredAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<BusinessParameter>>(Parameters.Keys.ToArray());

    void IBusinessParameters.Add(BusinessParameter parameter, int value) => Parameters.Add(parameter, value);

    // Histórico do seed

    Task<int?> ISeedLedger.FindAppliedSeedAsync(string version, CancellationToken cancellationToken) =>
        Task.FromResult(Ledger.TryGetValue(version, out var seed) ? seed : (int?)null);

    void ISeedLedger.Record(string version, int seed) => Ledger.Add(version, seed);
}
