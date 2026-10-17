using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Argos.Api.ErrorHandling;

/// <summary>
/// Monta o <see cref="ValidationProblemDetails"/> padronizado: título em pt-BR e campos em camelCase (plan 6.1).
/// </summary>
internal static class ValidationProblems
{
    private const string InvalidFormatMessage = "Valor em formato inválido.";

    public static ValidationProblemDetails Create(IReadOnlyDictionary<string, string[]> errors) =>
        new(errors.ToDictionary(error => NormalizeField(error.Key), error => error.Value))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = ProblemTitles.Validation,
            Detail = "Corrija os campos indicados e tente novamente.",
        };

    /// <summary>
    /// Substitui a resposta automática do <c>[ApiController]</c> para formato inválido nos DTOs.
    /// </summary>
    public static IActionResult FromModelState(ActionContext context)
    {
        var errors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .GroupBy(entry => NormalizeField(entry.Key))
            .ToDictionary(
                group => group.Key,
                group => group
                    .SelectMany(entry => entry.Value!.Errors)
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? InvalidFormatMessage : error.ErrorMessage)
                    .Distinct()
                    .ToArray());

        var problem = Create(errors);
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

        return new BadRequestObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" },
        };
    }

    /// <summary>
    /// "$.sentAt" e "SentAt" viram "sentAt"; caminhos aninhados mantêm os pontos ("location.zipCode").
    /// </summary>
    private static string NormalizeField(string key)
    {
        var path = key.TrimStart('$').TrimStart('.');

        return string.Join('.', path
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => char.ToLowerInvariant(segment[0]) + segment[1..]));
    }
}
