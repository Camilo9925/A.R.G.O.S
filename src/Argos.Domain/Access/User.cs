using System.Text.RegularExpressions;
using Argos.Domain.Common;

namespace Argos.Domain.Access;

/// <summary>
/// Pessoa que usa o ARGOS com um perfil (RF-022; spec 10; plan 14.2). Guarda a senha apenas como hash opaco:
/// o Domínio não sabe calcular nem verificar o hash, papel de uma porta da Aplicação (Art. III.2).
/// Nunca é excluído, só inativado (Art. V.2).
/// </summary>
public sealed partial class User : IAuditable, IDeactivatable
{
    public const int NameMinLength = 3;
    public const int NameMaxLength = 100;
    public const int LoginMinLength = 3;
    public const int LoginMaxLength = 50;

    // Usado pelo EF Core ao materializar.
    private User()
    {
    }

    private User(Guid id, string name, string login, string passwordHash, UserRole role)
    {
        Id = id;
        Name = name;
        Login = login;
        PasswordHash = passwordHash;
        Role = role;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Nome de usuário, sempre em minúsculas; único entre todos os usuários, inclusive os inativos.</summary>
    public string Login { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset? DeactivatedAt { get; private set; }

    public string? DeactivatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; private set; }

    public string? UpdatedBy { get; private set; }

    public static User Create(string name, string login, string passwordHash, UserRole role) =>
        new(Guid.NewGuid(), ValidName(name), ValidLogin(login), ValidPasswordHash(passwordHash), ValidRole(role));

    /// <summary>
    /// Forma canônica do login: sem espaços nas pontas e em minúsculas. Também usada para buscar no login,
    /// que não diferencia maiúsculas (D-36).
    /// </summary>
    public static string NormalizeLogin(string login) => (login ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>Atualiza nome e perfil. Retorna se algo mudou.</summary>
    public bool UpdateProfile(string name, UserRole role)
    {
        var validName = ValidName(name);
        var validRole = ValidRole(role);

        if (validName == Name && validRole == Role)
        {
            return false;
        }

        Name = validName;
        Role = validRole;
        return true;
    }

    /// <summary>
    /// Substitui o hash da senha. Só a reconciliação dos usuários de demonstração usa: não há troca de senha
    /// pelo usuário (D-09, D-40).
    /// </summary>
    public void ReplacePasswordHash(string passwordHash) => PasswordHash = ValidPasswordHash(passwordHash);

    /// <summary>Inativa o usuário, que deixa de poder entrar no sistema.</summary>
    public void Deactivate(DateTimeOffset at, string by)
    {
        if (!IsActive)
        {
            throw new InvalidUserException("O usuário já está inativo.");
        }

        if (string.IsNullOrWhiteSpace(by))
        {
            throw new InvalidUserException("Informe quem inativou o usuário.");
        }

        IsActive = false;
        DeactivatedAt = at.ToUniversalTime();
        DeactivatedBy = by.Trim();
    }

    private static string ValidName(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length is < NameMinLength or > NameMaxLength)
        {
            throw new InvalidUserException(
                $"O nome do usuário deve ter entre {NameMinLength} e {NameMaxLength} caracteres.");
        }

        return trimmed;
    }

    private static string ValidLogin(string login)
    {
        var normalized = NormalizeLogin(login);
        if (normalized.Length is < LoginMinLength or > LoginMaxLength || !LoginPattern().IsMatch(normalized))
        {
            throw new InvalidUserException(
                $"O login deve ter entre {LoginMinLength} e {LoginMaxLength} caracteres, "
                + "usando apenas letras minúsculas sem acento, números, ponto, hífen ou sublinhado.");
        }

        return normalized;
    }

    private static string ValidPasswordHash(string passwordHash) =>
        string.IsNullOrWhiteSpace(passwordHash)
            ? throw new InvalidUserException("O hash da senha é obrigatório.")
            : passwordHash;

    private static UserRole ValidRole(UserRole role) =>
        Enum.IsDefined(role) ? role : throw new InvalidUserException("Perfil de usuário desconhecido.");

    [GeneratedRegex("^[a-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex LoginPattern();
}
