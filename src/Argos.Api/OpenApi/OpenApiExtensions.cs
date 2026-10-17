using Argos.Api.Configuration;
using Argos.Contracts.Access;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Argos.Api.OpenApi;

/// <summary>
/// Documento OpenAPI e interface Scalar, ligados apenas no ambiente Demo (D-12). O documento declara os dois esquemas
/// de autenticação, para que o Scalar consiga autenticar ao testar os endpoints (plan 14.6). Ambos ficam anônimos.
/// </summary>
internal static class OpenApiExtensions
{
    private const string Title = "ARGOS API";
    private const string BearerScheme = "Bearer";
    private const string SystemKeyScheme = "SystemKey";

    public static IServiceCollection AddArgosOpenApi(this IServiceCollection services, IHostEnvironment environment)
    {
        if (environment.IsEnvironment(ArgosEnvironments.Demo))
        {
            services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = Title;
                AddSecuritySchemes(document);
                return Task.CompletedTask;
            }));
        }

        return services;
    }

    public static WebApplication MapArgosOpenApi(this WebApplication app)
    {
        if (app.Environment.IsEnvironment(ArgosEnvironments.Demo))
        {
            app.MapOpenApi().AllowAnonymous();
            app.MapScalarApiReference(options => options.Title = Title).AllowAnonymous();
        }

        return app;
    }

    private static void AddSecuritySchemes(OpenApiDocument document)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Token de acesso devolvido por POST /api/access/login (usuários da Web).",
        };

        document.Components.SecuritySchemes[SystemKeyScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = SystemKeyContract.HeaderName,
            Description = "Chave de sistema do PROMETHEUS (Simulador).",
        };

        // Dois requisitos alternativos: um endpoint aceita o token ou a chave, conforme a sua política.
        document.Security =
        [
            new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(BearerScheme, document)] = [] },
            new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(SystemKeyScheme, document)] = [] },
        ];
    }
}
