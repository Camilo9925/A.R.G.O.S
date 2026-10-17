using Argos.Application.Access;
using Argos.Application.Diagnostics;
using Argos.Application.Seeding;
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
        services.AddScoped<LoginService>();
        services.AddScoped<DemoUserProvisioningService>();
        return services;
    }
}
