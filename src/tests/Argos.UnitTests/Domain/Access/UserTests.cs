using Argos.Domain.Access;

namespace Argos.UnitTests.Domain.Access;

/// <summary>
/// Regras da entidade Usuário (RF-022; spec 10, v1.1.3; plan 14.2).
/// </summary>
public sealed class UserTests
{
    private const string Hash = "hash-opaco";

    [Fact]
    public void Create_ValidData_CreatesActiveUser()
    {
        var user = User.Create("Operador de Demonstração", "operador", Hash, UserRole.Operator);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("Operador de Demonstração", user.Name);
        Assert.Equal("operador", user.Login);
        Assert.Equal(Hash, user.PasswordHash);
        Assert.Equal(UserRole.Operator, user.Role);
        Assert.True(user.IsActive);
        Assert.Null(user.DeactivatedAt);
        Assert.Null(user.DeactivatedBy);
    }

    [Fact]
    public void Create_TrimsNameAndNormalizesLogin()
    {
        var user = User.Create("  Gestor de Demonstração  ", "  Gestor.Demo  ", Hash, UserRole.Manager);

        Assert.Equal("Gestor de Demonstração", user.Name);
        Assert.Equal("gestor.demo", user.Login);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    public void Create_NameTooShort_Throws(string name)
    {
        Assert.Throws<InvalidUserException>(() => User.Create(name, "admin", Hash, UserRole.Administrator));
    }

    [Fact]
    public void Create_NameTooLong_Throws()
    {
        var name = new string('a', User.NameMaxLength + 1);

        Assert.Throws<InvalidUserException>(() => User.Create(name, "admin", Hash, UserRole.Administrator));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("com espaço")]
    [InlineData("joão")]
    [InlineData("admin@argos")]
    public void Create_InvalidLogin_Throws(string login)
    {
        Assert.Throws<InvalidUserException>(() => User.Create("Administrador", login, Hash, UserRole.Administrator));
    }

    [Fact]
    public void Create_LoginTooLong_Throws()
    {
        var login = new string('a', User.LoginMaxLength + 1);

        Assert.Throws<InvalidUserException>(() => User.Create("Administrador", login, Hash, UserRole.Administrator));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingPasswordHash_Throws(string passwordHash)
    {
        Assert.Throws<InvalidUserException>(() => User.Create("Administrador", "admin", passwordHash, UserRole.Administrator));
    }

    [Fact]
    public void Create_UnknownRole_Throws()
    {
        Assert.Throws<InvalidUserException>(() => User.Create("Administrador", "admin", Hash, (UserRole)99));
    }

    [Fact]
    public void UpdateProfile_ChangedValues_UpdatesAndReportsChange()
    {
        var user = User.Create("Operador", "operador", Hash, UserRole.Operator);

        var changed = user.UpdateProfile("Operadora", UserRole.Manager);

        Assert.True(changed);
        Assert.Equal("Operadora", user.Name);
        Assert.Equal(UserRole.Manager, user.Role);
    }

    [Fact]
    public void UpdateProfile_SameValues_ReportsNoChange()
    {
        var user = User.Create("Operador", "operador", Hash, UserRole.Operator);

        Assert.False(user.UpdateProfile(" Operador ", UserRole.Operator));
    }

    [Fact]
    public void ReplacePasswordHash_ReplacesHash()
    {
        var user = User.Create("Operador", "operador", Hash, UserRole.Operator);

        user.ReplacePasswordHash("outro-hash");

        Assert.Equal("outro-hash", user.PasswordHash);
    }

    [Fact]
    public void ReplacePasswordHash_Empty_Throws()
    {
        var user = User.Create("Operador", "operador", Hash, UserRole.Operator);

        Assert.Throws<InvalidUserException>(() => user.ReplacePasswordHash(" "));
    }

    [Fact]
    public void Deactivate_ActiveUser_RecordsWhenAndWho()
    {
        var user = User.Create("Operador", "operador", Hash, UserRole.Operator);
        var at = new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.FromHours(-3));

        user.Deactivate(at, "admin");

        Assert.False(user.IsActive);
        Assert.Equal(at, user.DeactivatedAt);
        Assert.Equal(TimeSpan.Zero, user.DeactivatedAt!.Value.Offset);
        Assert.Equal("admin", user.DeactivatedBy);
    }

    [Fact]
    public void Deactivate_AlreadyInactive_Throws()
    {
        var user = User.Create("Operador", "operador", Hash, UserRole.Operator);
        user.Deactivate(DateTimeOffset.UtcNow, "admin");

        Assert.Throws<InvalidUserException>(() => user.Deactivate(DateTimeOffset.UtcNow, "admin"));
    }

    [Fact]
    public void Deactivate_WithoutAuthor_Throws()
    {
        var user = User.Create("Operador", "operador", Hash, UserRole.Operator);

        Assert.Throws<InvalidUserException>(() => user.Deactivate(DateTimeOffset.UtcNow, " "));
    }
}
