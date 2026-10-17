using Argos.Api.Configuration;
using Argos.Application.Common.Abstractions;
using Argos.Contracts.Access;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Argos.Api.Authentication;

internal static class AuthenticationExtensions
{
    // Só o hub recebe o token na URL, mecanismo padrão do SignalR para WebSockets (D-39).
    private const string HubPathPrefix = "/hubs";
    private const string HubAccessTokenParameter = "access_token";

    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Esquemas de autenticação (seletor, JWT e chave de sistema), emissor do token e autor das operações
    /// (D-34, D-35, D-43; plan 14.6).
    /// </summary>
    public static IServiceCollection AddArgosAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<SystemKeyOptions>()
            .Bind(configuration.GetSection(SystemKeyOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddHttpContextAccessor();
        services.AddSingleton<ICurrentActor, HttpContextCurrentActor>();

        services.AddAuthentication(ArgosAuthenticationSchemes.Default)
            .AddPolicyScheme(ArgosAuthenticationSchemes.Default, ArgosAuthenticationSchemes.Default, options =>
                options.ForwardDefaultSelector = context =>
                    context.Request.Headers.ContainsKey(SystemKeyContract.HeaderName)
                        ? ArgosAuthenticationSchemes.SystemKey
                        : ArgosAuthenticationSchemes.Bearer)
            .AddJwtBearer(ArgosAuthenticationSchemes.Bearer, options =>
            {
                options.MapInboundClaims = false;
                options.Events = new JwtBearerEvents { OnMessageReceived = ReadHubAccessToken };
            })
            .AddScheme<AuthenticationSchemeOptions, SystemKeyAuthenticationHandler>(ArgosAuthenticationSchemes.SystemKey, null);

        services.AddOptions<JwtBearerOptions>(ArgosAuthenticationSchemes.Bearer)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = jwt.CreateSigningKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = ClockSkew,
                    NameClaimType = AccessClaimTypes.Login,
                    RoleClaimType = AccessClaimTypes.Role,
                };
            });

        return services;
    }

    private static Task ReadHubAccessToken(MessageReceivedContext context)
    {
        var accessToken = context.Request.Query[HubAccessTokenParameter].ToString();
        if (accessToken.Length > 0 && context.Request.Path.StartsWithSegments(HubPathPrefix))
        {
            context.Token = accessToken;
        }

        return Task.CompletedTask;
    }
}
