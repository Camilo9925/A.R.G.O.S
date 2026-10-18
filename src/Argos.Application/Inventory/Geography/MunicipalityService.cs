using Argos.Application.Common.Exceptions;
using Argos.Contracts.Inventory;
using Argos.Domain.Inventory.Geography;

namespace Argos.Application.Inventory.Geography;

/// <summary>Dados do município e sugestão de endereço por CEP (RF-004, RN-02; D-50, D-52; plan 15.9).</summary>
public sealed class MunicipalityService(IInventoryQueries queries)
{
    public async Task<MunicipalityData> GetAsync(CancellationToken cancellationToken = default) =>
        await queries.GetMunicipalityAsync(cancellationToken)
            ?? throw new NotFoundException("O município ainda não foi carregado.");

    /// <summary>
    /// Sugere rua, bairro, região, cidade e UF do CEP (RF-004). Formato inválido vira erro no campo <c>zipCode</c>
    /// (RN-02); um CEP fora da tabela não impede o cadastro, que só exige o formato (D-52).
    /// </summary>
    public async Task<ZipCodeSuggestion> SuggestAsync(string? zipCode, CancellationToken cancellationToken = default)
    {
        var parsed = ZipCode.Parse(zipCode);

        return await queries.FindZipCodeAsync(parsed.Value, cancellationToken)
            ?? throw new NotFoundException("CEP não encontrado em Prometeia. Preencha o endereço manualmente.");
    }
}
