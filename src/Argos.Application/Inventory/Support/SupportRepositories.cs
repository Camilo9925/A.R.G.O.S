using Argos.Domain.Inventory.Support;

namespace Argos.Application.Inventory.Support;

// Portas de persistência dos órgãos e das bases (plan 15.12). Sem remoção (plan 5.4, RN-23). As buscas por chave
// normalizada (D-53) enxergam também os inativos.

public interface IOrganizationRepository
{
    Task<Organization?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Organization?> FindByNameKeyAsync(string nameKey, CancellationToken cancellationToken = default);

    Task<Organization?> FindByAcronymKeyAsync(string acronymKey, CancellationToken cancellationToken = default);

    void Add(Organization organization);
}

public interface IOperationalBaseRepository
{
    Task<OperationalBase?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Base do órgão com o nome normalizado informado; o nome é único por órgão (plan 15.2.3).</summary>
    Task<OperationalBase?> FindByNameKeyAsync(Guid organizationId, string nameKey, CancellationToken cancellationToken = default);

    /// <summary>Se há base ativa localizada no bairro (impede a inativação do bairro, plan 15.2.1).</summary>
    Task<bool> AnyActiveInNeighborhoodAsync(Guid neighborhoodId, CancellationToken cancellationToken = default);

    void Add(OperationalBase operationalBase);
}
