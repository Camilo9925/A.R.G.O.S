using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Argos.Api.HealthChecks;

/// <summary>
/// Health checks da API (plan 7.3). A resposta traz o estado em texto: <c>Healthy</c> ou <c>Unhealthy</c> (503).
/// </summary>
internal static class HealthCheckEndpoints
{
    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";

    public static IEndpointRouteBuilder MapArgosHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        // Vivo: o processo responde, sem consultar dependências.
        endpoints.MapHealthChecks(LivePath, new HealthCheckOptions { Predicate = _ => false });

        // Pronto: todos os checks registrados, incluindo o do banco (AddInfrastructure).
        endpoints.MapHealthChecks(ReadyPath);

        return endpoints;
    }
}
