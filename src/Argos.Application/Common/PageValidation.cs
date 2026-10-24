using Argos.Application.Common.Exceptions;
using Argos.Contracts.Common;

namespace Argos.Application.Common;

/// <summary>
/// Conferência dos parâmetros das listas paginadas do Inventário (D-53; plan 15.8): página, tamanho, ordenação de uma
/// lista fechada e filtro de ativos e inativos. Os erros saem todos de uma vez, cada um no seu parâmetro (400).
/// </summary>
internal static class PageValidation
{
    /// <exception cref="ValidationFailedException">Página ou tamanho fora da faixa.</exception>
    public static void Validate(int page, int pageSize) => ThrowIfAny(PageErrors(page, pageSize));

    /// <summary>Confere a pesquisa e devolve a ordenação a usar (a padrão, se não informada).</summary>
    /// <exception cref="ValidationFailedException">Página, tamanho, ordenação ou filtro de ativos inválidos.</exception>
    public static string ValidateSearch(
        PageQuery query, IReadOnlyList<string> sortFields, string defaultSort, ActivityFilter activity) =>
        Search(query, sortFields, defaultSort, activity);

    /// <summary>
    /// Confere uma pesquisa sem filtro de ativos (a dos equipamentos vinculáveis, D-68, em que só ativos entram) e
    /// devolve a ordenação a usar.
    /// </summary>
    /// <exception cref="ValidationFailedException">Página, tamanho ou ordenação inválidos.</exception>
    public static string ValidateSearch(PageQuery query, IReadOnlyList<string> sortFields, string defaultSort) =>
        Search(query, sortFields, defaultSort, activity: null);

    private static string Search(
        PageQuery query, IReadOnlyList<string> sortFields, string defaultSort, ActivityFilter? activity)
    {
        var errors = PageErrors(query.Page, query.PageSize);

        var sort = string.IsNullOrWhiteSpace(query.Sort) ? defaultSort : query.Sort.Trim();
        if (!sortFields.Contains(sort))
        {
            errors["sort"] = [$"Ordenação inválida. Use uma destas: {string.Join(", ", sortFields)}."];
        }

        // Na API, o binder do [ApiController] já recusa um número fora do enum com 400 antes de chegar aqui; esta
        // conferência protege as chamadas que não passam pela API (plan 15.12, Pesquisas): um valor fora da lista não
        // pode virar "todos".
        if (activity is { } filter && !Enum.IsDefined(filter))
        {
            errors["activity"] = ["Filtro de ativos inválido. Use active, inactive ou all."];
        }

        ThrowIfAny(errors);
        return sort;
    }

    private static Dictionary<string, string[]> PageErrors(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();

        if (page < 1)
        {
            errors["page"] = ["A página deve ser 1 ou maior."];
        }

        if (pageSize is < 1 or > PageQuery.MaxPageSize)
        {
            errors["pageSize"] = [$"O tamanho da página deve estar entre 1 e {PageQuery.MaxPageSize}."];
        }

        return errors;
    }

    private static void ThrowIfAny(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
        {
            throw new ValidationFailedException(errors);
        }
    }
}
