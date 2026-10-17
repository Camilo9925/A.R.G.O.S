namespace Argos.Web.Services.Api;

/// <summary>
/// Campos do <c>ProblemDetails</c> da API que a interface exibe (plan 6.1; Art. X.6).
/// </summary>
public sealed record ProblemResponse(string? Title, string? Detail);
