using Argos.Domain.Common.Parameters;

namespace Argos.Application.Common.Abstractions;

/// <summary>
/// Porta dos parâmetros de negócio guardados no banco (RN-34; Art. III.9; D-55; plan 15.10). A leitura tem cache em
/// memória na Infraestrutura; a edição pelo Administrador (RF-021) e a invalidação do cache entram na rodada da Central.
/// </summary>
public interface IBusinessParameters
{
    /// <summary>Valor gravado do parâmetro, ou o padrão do catálogo se ainda não houver (<see cref="BusinessParameterCatalog"/>).</summary>
    Task<int> GetAsync(BusinessParameter parameter, CancellationToken cancellationToken = default);

    /// <summary>Parâmetros que já têm valor gravado.</summary>
    Task<IReadOnlyCollection<BusinessParameter>> ListStoredAsync(CancellationToken cancellationToken = default);

    /// <summary>Acrescenta o valor de um parâmetro ainda não gravado; usado só pela carga (plan 15.10).</summary>
    void Add(BusinessParameter parameter, int value);
}
