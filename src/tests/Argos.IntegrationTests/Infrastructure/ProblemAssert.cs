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
}
