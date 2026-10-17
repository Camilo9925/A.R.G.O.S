namespace Argos.Web.Auth;

/// <summary>
/// Usuários de demonstração exibidos na tela de login com preenchimento rápido (RF-053; D-41; plan 14.10).
/// O bloco <c>DemoLogin</c> do <c>appsettings.json</c> versionado vem desligado e vazio; só o nginx do ambiente Demo
/// o preenche na subida do contêiner.
/// </summary>
public sealed class DemoLoginOptions
{
    public const string SectionName = "DemoLogin";

    public bool Enabled { get; init; }

    public IReadOnlyList<DemoLoginUser> Users { get; init; } = [];

    /// <summary>Exibe o painel só se estiver habilitado e houver usuários.</summary>
    public bool IsAvailable => Enabled && Users.Count > 0;

    /// <summary>Lê a seção sem binder por reflexão, para não depender do que o trimming da publicação preserva.</summary>
    public static DemoLoginOptions From(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);

        return new DemoLoginOptions
        {
            Enabled = bool.TryParse(section["Enabled"], out var enabled) && enabled,
            Users = section.GetSection("Users").GetChildren()
                .Select(user => new DemoLoginUser(user["Role"] ?? string.Empty, user["Login"] ?? string.Empty, user["Password"] ?? string.Empty))
                .Where(user => user.Login.Length > 0 && user.Password.Length > 0)
                .ToArray(),
        };
    }
}

/// <summary>Um usuário de demonstração; <see cref="Role"/> é um dos valores de <c>UserRoles</c>.</summary>
public sealed record DemoLoginUser(string Role, string Login, string Password);
