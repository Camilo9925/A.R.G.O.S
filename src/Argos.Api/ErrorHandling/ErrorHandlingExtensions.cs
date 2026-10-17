using Microsoft.AspNetCore.Mvc;

namespace Argos.Api.ErrorHandling;

internal static class ErrorHandlingExtensions
{
    /// <summary>
    /// <c>ProblemDetails</c> para todas as respostas de erro, com títulos em pt-BR e <c>traceId</c> (plan 6.1).
    /// Os <c>401</c> e <c>403</c> da autenticação e da autorização ganham também o <c>detail</c> de plan 14.7.
    /// </summary>
    public static IServiceCollection AddArgosErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Title = ProblemTitles.Translate(context.ProblemDetails.Status, context.ProblemDetails.Title);
            context.ProblemDetails.Detail ??= ProblemDetailMessages.DefaultFor(context.ProblemDetails.Status);
        });

        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.Configure<ApiBehaviorOptions>(options =>
            options.InvalidModelStateResponseFactory = ValidationProblems.FromModelState);

        return services;
    }
}
