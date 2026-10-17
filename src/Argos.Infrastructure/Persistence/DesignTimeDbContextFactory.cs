using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Argos.Infrastructure.Persistence;

/// <summary>
/// Permite ao <c>dotnet-ef</c> gerar migrations sem subir a API (plan 5.3).
/// Gerar migrations não abre conexão; para comandos que acessam o banco, informe a connection string
/// na variável de ambiente <c>ConnectionStrings__Argos</c>.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ArgosDbContext>
{
    private const string DesignTimeConnectionString = "Host=localhost;Database=argos";

    public ArgosDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Argos")
            ?? DesignTimeConnectionString;

        var builder = new DbContextOptionsBuilder<ArgosDbContext>();
        builder.UseArgosNpgsql(connectionString);

        return new ArgosDbContext(builder.Options);
    }
}
