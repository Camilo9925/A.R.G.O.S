using Microsoft.AspNetCore.WebUtilities;

namespace Argos.Api.ErrorHandling;

/// <summary>
/// Títulos de <c>ProblemDetails</c> em pt-BR (Art. XIII.2).
/// </summary>
internal static class ProblemTitles
{
    public const string Validation = "Um ou mais campos são inválidos.";
    public const string NotFound = "Recurso não encontrado.";
    public const string Conflict = "Conflito com o estado atual.";
    public const string BusinessRule = "Operação não permitida.";
    public const string Unauthenticated = "Não autenticado.";
    public const string Forbidden = "Acesso negado.";
    public const string InternalError = "Erro interno.";

    private const string DefaultInternalErrorTitle = "An error occurred while processing your request.";

    private static readonly Dictionary<int, string> ByStatus = new()
    {
        [StatusCodes.Status400BadRequest] = "Requisição inválida.",
        [StatusCodes.Status401Unauthorized] = Unauthenticated,
        [StatusCodes.Status403Forbidden] = Forbidden,
        [StatusCodes.Status404NotFound] = NotFound,
        [StatusCodes.Status405MethodNotAllowed] = "Método não permitido.",
        [StatusCodes.Status406NotAcceptable] = "Formato de resposta não suportado.",
        [StatusCodes.Status409Conflict] = Conflict,
        [StatusCodes.Status415UnsupportedMediaType] = "Formato de conteúdo não suportado.",
        [StatusCodes.Status500InternalServerError] = InternalError,
    };

    /// <summary>
    /// Troca o título padrão em inglês do ASP.NET Core pelo equivalente em pt-BR, quando houver.
    /// </summary>
    public static string? Translate(int? status, string? title)
    {
        if (status is not { } code || !ByStatus.TryGetValue(code, out var translated))
        {
            return title;
        }

        var isDefault = title is null
            || title == ReasonPhrases.GetReasonPhrase(code)
            || title == DefaultInternalErrorTitle;

        return isDefault ? translated : title;
    }
}
