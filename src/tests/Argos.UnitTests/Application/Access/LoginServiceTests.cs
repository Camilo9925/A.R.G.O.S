using Argos.Application.Access;
using Argos.Application.Common.Exceptions;
using Argos.Contracts.Access;
using Argos.Domain.Access;
using Argos.UnitTests.Application.Fakes;

namespace Argos.UnitTests.Application.Access;

/// <summary>
/// Login de usuário (RF-022; D-36; plan 14.4).
/// </summary>
public sealed class LoginServiceTests
{
    private const string Password = "senha-de-teste";

    private readonly FakeUserRepository _users = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeAccessTokenIssuer _tokenIssuer = new();

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokenUserAndPermissions_RN26()
    {
        var user = AddUser("Operador de Demonstração", "operador", UserRole.Operator);

        var response = await CreateService().LoginAsync(Request("operador", Password), TestContext.Current.CancellationToken);

        Assert.Equal("token:operador", response.AccessToken);
        Assert.Equal("Bearer", response.TokenType);
        Assert.Equal(FakeAccessTokenIssuer.ExpiresAt, response.ExpiresAt);
        Assert.Equal("operador", response.User.Login);
        Assert.Equal("Operador de Demonstração", response.User.Name);
        Assert.Equal(UserRoles.Operator, response.User.Role);
        Assert.Equal(AccessPolicyCatalog.PermissionsFor(UserRole.Operator), response.Permissions);
        Assert.Equal(user.Id, _tokenIssuer.LastSubject!.UserId);
        Assert.Equal(UserRoles.Operator, _tokenIssuer.LastSubject.Role);
    }

    [Fact]
    public async Task Login_IgnoresCaseAndSurroundingSpacesInLogin_D36()
    {
        AddUser("Gestor de Demonstração", "gestor", UserRole.Manager);

        var response = await CreateService().LoginAsync(Request("  GESTOR ", Password), TestContext.Current.CancellationToken);

        Assert.Equal("gestor", response.User.Login);
    }

    [Fact]
    public async Task Login_WrongPassword_Fails_D36()
    {
        AddUser("Operador", "operador", UserRole.Operator);

        await AssertFailsAsync(Request("operador", "senha-errada"));
    }

    [Fact]
    public async Task Login_UnknownLogin_FailsAfterVerifyingADummyHash_D36()
    {
        await AssertFailsAsync(Request("ninguem", Password));

        Assert.Equal(1, _hasher.VerifyCalls);
    }

    [Fact]
    public async Task Login_EmptyLogin_FailsAfterVerifyingADummyHash_D36()
    {
        await AssertFailsAsync(Request("   ", Password));

        Assert.Equal(1, _hasher.VerifyCalls);
    }

    [Fact]
    public async Task Login_InactiveUser_FailsEvenWithRightPassword_D36()
    {
        var user = AddUser("Operador", "operador", UserRole.Operator);
        user.Deactivate(DateTimeOffset.UtcNow, "admin");

        await AssertFailsAsync(Request("operador", Password));
    }

    [Fact]
    public async Task Login_AllFailures_ShareTheSameMessage_D36()
    {
        var inactive = AddUser("Inativo", "inativo", UserRole.Operator);
        inactive.Deactivate(DateTimeOffset.UtcNow, "admin");
        AddUser("Operador", "operador", UserRole.Operator);

        var wrongPassword = await AssertFailsAsync(Request("operador", "senha-errada"));
        var unknown = await AssertFailsAsync(Request("ninguem", Password));
        var deactivated = await AssertFailsAsync(Request("inativo", Password));

        Assert.Equal("Usuário ou senha inválidos.", wrongPassword.Message);
        Assert.Equal(wrongPassword.Message, unknown.Message);
        Assert.Equal(wrongPassword.Message, deactivated.Message);
        Assert.Null(_tokenIssuer.LastSubject);
    }

    private LoginService CreateService() => new(_users, _hasher, _tokenIssuer);

    private User AddUser(string name, string login, UserRole role)
    {
        var user = User.Create(name, login, _hasher.Hash(Password), role);
        _users.Add(user);
        return user;
    }

    private static LoginRequest Request(string login, string password) => new() { Login = login, Password = password };

    private Task<AuthenticationFailedException> AssertFailsAsync(LoginRequest request) =>
        Assert.ThrowsAsync<AuthenticationFailedException>(
            () => CreateService().LoginAsync(request, TestContext.Current.CancellationToken));
}
