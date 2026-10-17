using Microsoft.AspNetCore.Mvc;

namespace Argos.Api.ErrorHandling;

internal static class ErrorHandlingExtensions
{
    /// <summary>
    /// <c>ProblemDetails</c> para todas as respostas de erro, com títulos em pt-BR e <c>traceId</c> (plan 6.1).
    /// </summary>
    public static IServiceCollection AddArgosErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Title = ProblemTitles.Translate(context.ProblemDetails.Status, context.ProblemDetails.Title));

        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.Configure<ApiBehaviorOptions>(options =>
            options.InvalidModelStateResponseFactory = ValidationProblems.FromModelState);

        return services;
    }
}
