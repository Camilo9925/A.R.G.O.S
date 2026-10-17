using Argos.Application.Common.Abstractions;
using Argos.Contracts.Access;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Argos.IntegrationTests.Infrastructure;

/// <summary>
/// Emite tokens de teste pela própria porta <see cref="IAccessTokenIssuer"/> da API, sem usuário no banco
/// (D-44; plan 8.2, 14.12). Os tokens inválidos saem de cópias da API com uma única diferença de configuração:
/// o relógio no passado (expirado) ou outra chave de assinatura (assinatura inválida).
/// </summary>
public sealed class TestAccessTokens
{
    // Emitido um dia atrás: vencido há horas com qualquer validade permitida (até 8 h) e fora da tolerância de 1 min.
    private static readonly TimeSpan ExpiredIssueOffset = TimeSpan.FromDays(1);

    private const string ForeignSigningKey = "outra-chave-de-assinatura-que-a-api-nao-conhece";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly Lazy<WebApplicationFactory<Program>> _expiredFactory;
    private readonly Lazy<WebApplicationFactory<Program>> _foreignKeyFactory;

    public TestAccessTokens(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _expiredFactory = new(() => factory.WithWebHostBuilder(builder => builder
            .ConfigureTestServices(services => services.AddSingleton<TimeProvider>(
                new FixedTimeProvider(DateTimeOffset.UtcNow - ExpiredIssueOffset)))));
        _foreignKeyFactory = new(() => factory.WithWebHostBuilder(builder => builder
            .UseSetting("Jwt:SigningKey", ForeignSigningKey)));
    }

    /// <summary>Token válido de um perfil (<see cref="UserRoles"/>).</summary>
    public string For(string role) => Issue(_factory, role);

    /// <summary>Token do perfil que já venceu.</summary>
    public string ExpiredFor(string role) => Issue(_expiredFactory.Value, role);

    /// <summary>Token do perfil assinado com uma chave que a API não conhece.</summary>
    public string WithInvalidSignatureFor(string role) => Issue(_foreignKeyFactory.Value, role);

    /// <summary>Login fictício usado nos tokens de cada perfil.</summary>
    public static string LoginFor(string role) => role switch
    {
        UserRoles.Administrator => "admin",
        UserRoles.Operator => "operador",
        UserRoles.Manager => "gestor",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Perfil desconhecido."),
    };

    private static string Issue(WebApplicationFactory<Program> factory, string role) =>
        factory.Services.GetRequiredService<IAccessTokenIssuer>()
            .Issue(new AccessTokenSubject(Guid.NewGuid(), LoginFor(role), $"Usuário de Teste ({role})", role))
            .Value;

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
