using Argos.Application.Common.Abstractions;
using Argos.Application.Common.Exceptions;
using Argos.Domain.Common;
using Argos.Domain.Common.Parameters;

namespace Argos.Application.Seeding;

/// <summary>
/// Cria os parâmetros de negócio que ainda não estão no banco (RN-34; Art. III.9; D-55; plan 15.10), com o valor da
/// configuração do Migrator (seção <c>BusinessParameters</c>, pela chave do catálogo) ou o padrão da spec. Nunca
/// sobrescreve um valor gravado: o Administrador é quem o altera. Roda a cada execução, fora do histórico do seed.
/// </summary>
public sealed class BusinessParameterProvisioningService(IBusinessParameters parameters, IUnitOfWork unitOfWork)
{
    public const string SectionName = "BusinessParameters";

    /// <param name="configured">Valores iniciais por chave (<c>maintenance.upcoming-window-days</c>), ou nulo.</param>
    /// <returns>As chaves criadas nesta execução.</returns>
    /// <exception cref="ValidationFailedException">Chave desconhecida ou valor fora da faixa, no campo da chave.</exception>
    public async Task<IReadOnlyList<string>> ProvisionAsync(
        IReadOnlyDictionary<string, int>? configured, CancellationToken cancellationToken = default)
    {
        var initialValues = Validate(configured ?? new Dictionary<string, int>());
        var stored = await parameters.ListStoredAsync(cancellationToken);
        var created = new List<string>();

        foreach (var definition in BusinessParameterCatalog.All.Where(definition => !stored.Contains(definition.Parameter)))
        {
            parameters.Add(
                definition.Parameter,
                initialValues.TryGetValue(definition.Parameter, out var value) ? value : definition.Default);
            created.Add(definition.Key);
        }

        if (created.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return created;
    }

    /// <summary>Confere toda a configuração, mesmo dos parâmetros já gravados, para que um erro nela não passe despercebido.</summary>
    private static Dictionary<BusinessParameter, int> Validate(IReadOnlyDictionary<string, int> configured)
    {
        var errors = new Dictionary<string, string[]>();
        var values = new Dictionary<BusinessParameter, int>();

        foreach (var (key, value) in configured)
        {
            var field = $"{SectionName}:{key}";

            if (BusinessParameterCatalog.FindByKey(key) is not { } definition)
            {
                errors[field] = ["Parâmetro de negócio desconhecido."];
                continue;
            }

            try
            {
                values[definition.Parameter] = BusinessParameterCatalog.Validate(definition.Parameter, value);
            }
            catch (DomainValidationException exception)
            {
                errors[field] = exception.Errors.SelectMany(error => error.Value).ToArray();
            }
        }

        return errors.Count == 0 ? values : throw new ValidationFailedException(errors);
    }
}
