using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Argos.IntegrationTests.Infrastructure;

/// <summary>
/// Verifica respostas de erro em <c>ProblemDetails</c> (Art. VIII.6, XII.4; plan 6.1, 14.7).
/// </summary>
internal static class ProblemAssert
{
    public const string UnauthenticatedTitle = "Não autenticado.";
    public const string UnauthenticatedDetail = "Sessão ausente ou expirada. Faça login para continuar.";
    public const string ForbiddenTitle = "Acesso negado.";
    public const string ForbiddenDetail = "Seu perfil não tem permissão para acessar este recurso.";
    public const string ValidationTitle = "Um ou mais campos são inválidos.";
    public const string ValidationDetail = "Corrija os campos indicados e tente novamente.";
    public const string NotFoundTitle = "Recurso não encontrado.";
    public const string ConflictTitle = "Conflito com o estado atual.";
    public const string BusinessRuleTitle = "Operação não permitida.";

    /// <summary>
    /// Confere o código, o tipo de conteúdo, o título e o detalhe em pt-BR e a presença do <c>traceId</c>.
    /// Devolve o corpo para verificações adicionais.
    /// </summary>
    public static async Task<JsonElement> HasProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string title,
        string detail,
        CancellationToken cancellationToken)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.Equal(title, problem.GetProperty("title").GetString());
        Assert.Equal(detail, problem.GetProperty("detail").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        return problem;
    }

    public static Task<JsonElement> IsUnauthenticatedAsync(HttpResponseMessage response, CancellationToken cancellationToken) =>
        HasProblemAsync(response, HttpStatusCode.Unauthorized, UnauthenticatedTitle, UnauthenticatedDetail, cancellationToken);

    public static Task<JsonElement> IsForbiddenAsync(HttpResponseMessage response, CancellationToken cancellationToken) =>
        HasProblemAsync(response, HttpStatusCode.Forbidden, ForbiddenTitle, ForbiddenDetail, cancellationToken);

    /// <summary>Forbidden com o motivo da recusa no <c>detail</c> (D-47).</summary>
    public static Task<JsonElement> IsForbiddenAsync(
        HttpResponseMessage response, string detail, CancellationToken cancellationToken) =>
        HasProblemAsync(response, HttpStatusCode.Forbidden, ForbiddenTitle, detail, cancellationToken);

    /// <summary>400 com <c>ValidationProblemDetails</c>; devolve os erros por campo para conferir o campo exato.</summary>
    public static async Task<Dictionary<string, string[]>> IsValidationAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var problem = await HasProblemAsync(
            response, HttpStatusCode.BadRequest, ValidationTitle, ValidationDetail, cancellationToken);

        return problem.GetProperty("errors")
            .EnumerateObject()
            .ToDictionary(
                field => field.Name,
                field => field.Value.EnumerateArray().Select(message => message.GetString()!).ToArray());
    }

    public static Task<JsonElement> IsNotFoundAsync(
        HttpResponseMessage response, string detail, CancellationToken cancellationToken) =>
        HasProblemAsync(response, HttpStatusCode.NotFound, NotFoundTitle, detail, cancellationToken);

    /// <summary>409 de conflito com o estado atual: duplicidade ou concorrência (D-60).</summary>
    public static Task<JsonElement> IsConflictAsync(
        HttpResponseMessage response, string detail, CancellationToken cancellationToken) =>
        HasProblemAsync(response, HttpStatusCode.Conflict, ConflictTitle, detail, cancellationToken);

    /// <summary>409 de regra de negócio sem campo, com o motivo no <c>detail</c> (Art. XII.4).</summary>
    public static Task<JsonElement> IsBusinessRuleAsync(
        HttpResponseMessage response, string detail, CancellationToken cancellationToken) =>
        HasProblemAsync(response, HttpStatusCode.Conflict, BusinessRuleTitle, detail, cancellationToken);
}
