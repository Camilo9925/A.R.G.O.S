using Argos.Application.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Argos.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registra os serviços de aplicação. As portas são implementadas e registradas pela API e pela Infraestrutura.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<TechnicalHeartbeatService>();
        return services;
    }
}
