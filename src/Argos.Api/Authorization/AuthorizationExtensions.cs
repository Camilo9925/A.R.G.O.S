using Argos.Api.Authentication;
using Argos.Application.Access;
using Argos.Contracts.Access;
using Microsoft.AspNetCore.Authorization;

namespace Argos.Api.Authorization;

internal static class AuthorizationExtensions
{
    /// <summary>
    /// Uma política por permissão do catálogo (RN-26, RN-27; D-32), a política do ator de sistema (D-34) e a
    /// proteção por padrão: todo endpoint sem política explícita exige usuário autenticado (D-33).
    /// </summary>
    public static IServiceCollection AddArgosAuthorization(this IServiceCollection services)
    {
        var builder = services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(SystemPolicies.TelemetryIngest, policy => policy.RequireRole(SystemActor.Role));

        foreach (var accessPolicy in AccessPolicyCatalog.Policies)
        {
            builder.AddPolicy(accessPolicy.Name, policy => policy.RequireRole(accessPolicy.Roles));
        }

        return services;
    }
}
