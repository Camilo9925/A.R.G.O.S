using Argos.Application.Common.Exceptions;
using Argos.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Argos.Api.ErrorHandling;

/// <summary>
/// Traduz exceções em <see cref="ProblemDetails"/> em pt-BR, sem detalhes internos (Art. VIII.6, XII.4; plan 6.1).
/// O detalhe técnico de erros inesperados vai só para o log.
/// </summary>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ValidationFailedException validation => ValidationProblems.Create(validation.Errors),
            AuthenticationFailedException =>
                Problem(StatusCodes.Status401Unauthorized, ProblemTitles.Unauthenticated, exception.Message),
            NotFoundException => Problem(StatusCodes.Status404NotFound, ProblemTitles.NotFound, exception.Message),
            ConflictException => Problem(StatusCodes.Status409Conflict, ProblemTitles.Conflict, exception.Message),
            BusinessRuleException or DomainException =>
                Problem(StatusCodes.Status409Conflict, ProblemTitles.BusinessRule, exception.Message),
            _ => null,
        };

        if (problem is null)
        {
            logger.LogError(exception, "Erro não tratado em {Method} {Path}.", httpContext.Request.Method, httpContext.Request.Path);
            problem = Problem(
                StatusCodes.Status500InternalServerError,
                ProblemTitles.InternalError,
                "Ocorreu um erro inesperado. Se o problema persistir, informe o traceId ao suporte.");
        }

        httpContext.Response.StatusCode = problem.Status!.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
        });
    }

    private static ProblemDetails Problem(int status, string title, string detail) =>
        new() { Status = status, Title = title, Detail = detail };
}
