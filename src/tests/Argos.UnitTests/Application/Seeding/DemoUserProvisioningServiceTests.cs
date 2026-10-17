using Argos.Application.Common.Exceptions;
using Argos.Application.Seeding;
using Argos.Domain.Access;
using Argos.UnitTests.Application.Fakes;

namespace Argos.UnitTests.Application.Seeding;

/// <summary>
/// Reconciliação dos usuários de demonstração, um por perfil (RF-053; D-40; plan 14.4, 14.10).
/// </summary>
public sealed class DemoUserProvisioningServiceTests
{
    private readonly FakeUserRepository _users = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    [Fact]
    public async Task Provision_EmptyDatabase_CreatesOneUserPerProfile_RF053()
    {
        var result = await Provision(Options());

        Assert.Equal(["admin", "operador", "gestor"], result.Created);
        Assert.Empty(result.Updated);
        Assert.Equal(1, _unitOfWork.SaveCalls);

        Assert.Collection(
            _users.Users,
            user => AssertUser(user, "admin", "Administrador de Demonstração", UserRole.Administrator, "senha-admin"),
            user => AssertUser(user, "operador", "Operador de Demonstração", UserRole.Operator, "senha-operador"),
            user => AssertUser(user, "gestor", "Gestor de Demonstração", UserRole.Manager, "senha-gestor"));
    }

    [Fact]
    public async Task Provision_SameConfigurationTwice_DoesNotSaveAgain_D40()
    {
        await Provision(Options());

        var second = await Provision(Options());

        Assert.Empty(second.Created);
        Assert.Empty(second.Updated);
        Assert.Equal(1, _unitOfWork.SaveCalls);
        Assert.Equal(3, _users.Users.Count);
    }

    [Fact]
    public async Task Provision_ChangedPassword_ReplacesOnlyThatHash_D40()
    {
        await Provision(Options());
        var options = Options();
        options.Operator!.Password = "nova-senha-operador";

        var result = await Provision(options);

        Assert.Equal(["operador"], result.Updated);
        Assert.Equal(_hasher.Hash("nova-senha-operador"), _users.Users.Single(user => user.Login == "operador").PasswordHash);
        Assert.Equal(_hasher.Hash("senha-admin"), _users.Users.Single(user => user.Login == "admin").PasswordHash);
        Assert.Equal(2, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Provision_ChangedNameOrProfile_UpdatesUser_D40()
    {
        await Provision(Options());
        var options = Options();
        options.Manager!.Name = "Gestora de Demonstração";

        var result = await Provision(options);

        Assert.Equal(["gestor"], result.Updated);
        Assert.Equal("Gestora de Demonstração", _users.Users.Single(user => user.Login == "gestor").Name);
    }

    [Fact]
    public async Task Provision_LoginOfAnotherProfile_MovesUserToConfiguredProfile_D40()
    {
        _users.Add(User.Create("Antigo", "gestor", _hasher.Hash("senha-gestor"), UserRole.Operator));

        var result = await Provision(Options());

        Assert.Equal(["gestor"], result.Updated);
        Assert.Equal(UserRole.Manager, _users.Users.Single(user => user.Login == "gestor").Role);
    }

    [Fact]
    public async Task Provision_InactiveUser_IsNotReactivated_D40()
    {
        var inactive = User.Create("Operador de Demonstração", "operador", _hasher.Hash("senha-operador"), UserRole.Operator);
        inactive.Deactivate(DateTimeOffset.UtcNow, "admin");
        _users.Add(inactive);

        await Provision(Options());

        Assert.False(inactive.IsActive);
    }

    [Fact]
    public async Task Provision_MissingProfile_FailsWithoutSaving_D40()
    {
        var options = Options();
        options.Manager = null;

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => Provision(options));

        Assert.Contains("manager", exception.Errors.Keys);
        Assert.Empty(_users.Users);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Theory]
    [InlineData("Admin", "senha-admin", "administrator.login")]
    [InlineData("ad", "senha-admin", "administrator.login")]
    [InlineData("admin", "curta", "administrator.password")]
    [InlineData("admin", "senha com espaço", "administrator.password")]
    [InlineData("admin", "senha\"aspas", "administrator.password")]
    public async Task Provision_InvalidFormat_FailsNamingTheField_D40(string login, string password, string field)
    {
        var options = Options();
        options.Administrator!.Login = login;
        options.Administrator.Password = password;

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => Provision(options));

        Assert.Contains(field, exception.Errors.Keys);
        Assert.Empty(_users.Users);
    }

    [Fact]
    public async Task Provision_SameLoginForTwoProfiles_Fails_D40()
    {
        var options = Options();
        options.Manager!.Login = "operador";

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => Provision(options));

        Assert.Contains("login", exception.Errors.Keys);
        Assert.Empty(_users.Users);
    }

    private Task<DemoUserProvisioningResult> Provision(DemoUsersOptions options) =>
        new DemoUserProvisioningService(_users, _hasher, _unitOfWork)
            .ProvisionAsync(options, TestContext.Current.CancellationToken);

    private void AssertUser(User user, string login, string name, UserRole role, string password)
    {
        Assert.Equal(login, user.Login);
        Assert.Equal(name, user.Name);
        Assert.Equal(role, user.Role);
        Assert.True(user.IsActive);
        Assert.True(_hasher.Verify(user.PasswordHash, password));
    }

    private static DemoUsersOptions Options() => new()
    {
        Administrator = new DemoUserOptions { Login = "admin", Name = "Administrador de Demonstração", Password = "senha-admin" },
        Operator = new DemoUserOptions { Login = "operador", Name = "Operador de Demonstração", Password = "senha-operador" },
        Manager = new DemoUserOptions { Login = "gestor", Name = "Gestor de Demonstração", Password = "senha-gestor" },
    };
}
