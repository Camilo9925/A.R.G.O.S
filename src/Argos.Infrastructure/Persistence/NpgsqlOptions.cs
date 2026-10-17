using Microsoft.EntityFrameworkCore;

namespace Argos.Infrastructure.Persistence;

/// <summary>
/// Configuração do provedor comum à aplicação e ao <c>dotnet-ef</c>, para que as migrations usem as mesmas convenções.
/// </summary>
internal static class NpgsqlOptions
{
    public static DbContextOptionsBuilder UseArgosNpgsql(this DbContextOptionsBuilder builder, string connectionString) =>
        builder
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();
}
