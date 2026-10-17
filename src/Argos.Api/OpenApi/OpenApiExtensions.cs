using Argos.Api.Configuration;
using Scalar.AspNetCore;

namespace Argos.Api.OpenApi;

/// <summary>
/// Documento OpenAPI e interface Scalar, ligados apenas no ambiente Demo (D-12).
/// </summary>
internal static class OpenApiExtensions
{
    private const string Title = "ARGOS API";

    public static IServiceCollection AddArgosOpenApi(this IServiceCollection services, IHostEnvironment environment)
    {
        if (environment.IsEnvironment(ArgosEnvironments.Demo))
        {
            services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = Title;
                return Task.CompletedTask;
            }));
        }

        return services;
    }

    public static WebApplication MapArgosOpenApi(this WebApplication app)
    {
        if (app.Environment.IsEnvironment(ArgosEnvironments.Demo))
        {
            app.MapOpenApi();
            app.MapScalarApiReference(options => options.Title = Title);
        }

        return app;
    }
}
