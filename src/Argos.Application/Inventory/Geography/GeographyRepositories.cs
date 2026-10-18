using Argos.Domain.Inventory.Geography;

namespace Argos.Application.Inventory.Geography;

// Portas de persistência da geografia (plan 15.12). Sem remoção: os cadastros só são inativados (plan 5.4, RN-23).
// As buscas por chave normalizada (D-53) enxergam também os inativos, porque os nomes são únicos entre todos.

/// <summary>O município da demonstração, registro único carregado pela carga inicial (D-50).</summary>
public interface IMunicipalityRepository
{
    /// <summary>O município, ou nulo se a carga ainda não o criou.</summary>
    Task<Municipality?> GetAsync(CancellationToken cancellationToken = default);

    void Add(Municipality municipality);
}

public interface IRegionRepository
{
    Task<Region?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Region?> FindByNameKeyAsync(string nameKey, CancellationToken cancellationToken = default);

    void Add(Region region);
}

public interface INeighborhoodRepository
{
    Task<Neighborhood?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Neighborhood?> FindByNameKeyAsync(string nameKey, CancellationToken cancellationToken = default);

    /// <summary>Se a região tem algum bairro ativo (impede a inativação da região, plan 15.2.1).</summary>
    Task<bool> AnyActiveInRegionAsync(Guid regionId, CancellationToken cancellationToken = default);

    void Add(Neighborhood neighborhood);
}

/// <summary>Tabela local de CEPs, somente leitura depois da carga (D-52).</summary>
public interface IZipCodeRepository
{
    Task<ZipCodeEntry?> FindAsync(ZipCode zipCode, CancellationToken cancellationToken = default);

    void Add(ZipCodeEntry entry);
}
