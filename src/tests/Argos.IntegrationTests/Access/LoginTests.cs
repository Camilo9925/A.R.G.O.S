using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Argos.Application.Seeding;
using Argos.Contracts.Access;
using Argos.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Argos.IntegrationTests.Access;

/// <summary>
/// Login (RF-022; plan 14.7, 14.12): caminho feliz com usuários criados pelo provisionamento de demonstração
/// (Art. XII.3), a mesma recusa para senha errada e login inexistente (D-36) e validação em pt-BR (Art. XII.4).
/// </summary>
public sealed class LoginTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    private const string LoginPath = "/api/access/login";
    private const string RefusedDetail = "Usuário ou senha inválidos.";

    private static readonly DemoUsersOptions DemoUsers = new()
    {
        Administrator = new() { Login = "admin", Name = "Administrador de Teste", Password = "senha-admin-teste" },
        Operator = new() { Login = "operador", Name = "Operador de Teste", Password = "senha-operador-teste" },
        Manager = new() { Login = "gestor", Name = "Gestor de Teste", Password = "senha-gestor-teste" },
    };

    public static TheoryData<string> Roles => [.. UserRoles.All];

    [Theory]
    [MemberData(nameof(Roles))]
    public async Task Login_ValidCredentials_ReturnsTokenUserAndRolePermissions(string role)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await ProvisionDemoUsersAsync(cancellationToken);
        var demoUser = DemoUserFor(role);
        var before = DateTimeOffset.UtcNow;

        using var client = Fixture.Factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            LoginPath, new LoginRequest { Login = demoUser.Login, Password = demoUser.Password }, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.Equal("Bearer", login.TokenType);
        Assert.Equal(demoUser.Login, login.User.Login);
        Assert.Equal(demoUser.Name, login.User.Name);
        Assert.Equal(role, login.User.Role);

        // RN-26: a Web recebe exatamente as permissões do perfil.
        Assert.Equal(Rn26Matrix.PermissionsFor(role).Order(), login.Permissions.Order());

        // D-47: só o Administrador recebe a reativação, e a Web só oferece a opção a quem a tem.
        Assert.Equal(role == UserRoles.Administrator, login.Permissions.Contains(Permissions.ResourceReactivate));

        // D-09: 8 h de validade, em UTC.
        Assert.Equal(TimeSpan.Zero, login.ExpiresAt.Offset);
        Assert.InRange(login.ExpiresAt, before.AddHours(8).AddSeconds(-2), DateTimeOffset.UtcNow.AddHours(8));

        // O token emitido é aceito por um endpoint protegido.
        using var userClient = Fixture.CreateUserClient(login.AccessToken);
        using var protectedResponse = await userClient.GetAsync(
            PolicyProbeController.PathFor(Permissions.OperationsView), cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, protectedResponse.StatusCode);
    }

    [Fact]
    public async Task Login_LoginInDifferentCase_IsAccepted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await ProvisionDemoUsersAsync(cancellationToken);

        using var client = Fixture.Factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            LoginPath, new LoginRequest { Login = " GESTOR ", Password = DemoUsers.Manager!.Password }, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
        Assert.Equal("gestor", login?.User.Login);
    }

    [Fact]
    public async Task Login_WrongPasswordAndUnknownLogin_ReturnTheSameUnauthorizedProblem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await ProvisionDemoUsersAsync(cancellationToken);
        using var client = Fixture.Factory.CreateClient();

        using var wrongPassword = await client.PostAsJsonAsync(
            LoginPath, new LoginRequest { Login = "operador", Password = "senha-errada" }, cancellationToken);
        using var unknownLogin = await client.PostAsJsonAsync(
            LoginPath, new LoginRequest { Login = "ninguem", Password = "senha-errada" }, cancellationToken);

        var wrongPasswordProblem = await ProblemAssert.HasProblemAsync(
            wrongPassword, HttpStatusCode.Unauthorized, ProblemAssert.UnauthenticatedTitle, RefusedDetail, cancellationToken);
        var unknownLoginProblem = await ProblemAssert.HasProblemAsync(
            unknownLogin, HttpStatusCode.Unauthorized, ProblemAssert.UnauthenticatedTitle, RefusedDetail, cancellationToken);

        // D-36: fora o traceId, as duas respostas são idênticas e não revelam qual login existe.
        Assert.Equal(WithoutTraceId(wrongPasswordProblem), WithoutTraceId(unknownLoginProblem));
    }

    [Fact]
    public async Task Login_EmptyObject_ReturnsValidationProblemInPortuguese()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = Fixture.Factory.CreateClient();

        using var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(LoginPath, content, cancellationToken);

        var problem = await ProblemAssert.HasProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            "Um ou mais campos são inválidos.",
            "Corrija os campos indicados e tente novamente.",
            cancellationToken);
        var errors = problem.GetProperty("errors");
        Assert.Equal(["Informe o usuário."], ErrorsOf(errors, "login"));
        Assert.Equal(["Informe a senha."], ErrorsOf(errors, "password"));
    }

    [Fact]
    public async Task Login_WithoutBody_ReturnsValidationProblem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = Fixture.Factory.CreateClient();

        using var content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(LoginPath, content, cancellationToken);

        var problem = await ProblemAssert.HasProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            "Um ou mais campos são inválidos.",
            "Corrija os campos indicados e tente novamente.",
            cancellationToken);
        var messages = problem.GetProperty("errors").EnumerateObject()
            .SelectMany(field => field.Value.EnumerateArray().Select(error => error.GetString()));
        Assert.Contains("O corpo da requisição é obrigatório.", messages);
    }

    private async Task ProvisionDemoUsersAsync(CancellationToken cancellationToken)
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DemoUserProvisioningService>().ProvisionAsync(DemoUsers, cancellationToken);
    }

    private static DemoUserOptions DemoUserFor(string role) => role switch
    {
        UserRoles.Administrator => DemoUsers.Administrator!,
        UserRoles.Operator => DemoUsers.Operator!,
        UserRoles.Manager => DemoUsers.Manager!,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Perfil desconhecido."),
    };

    private static string[] ErrorsOf(JsonElement errors, string field) =>
        errors.GetProperty(field).EnumerateArray().Select(error => error.GetString()!).ToArray();

    private static string WithoutTraceId(JsonElement problem) =>
        JsonSerializer.Serialize(problem.EnumerateObject()
            .Where(property => property.Name != "traceId")
            .ToDictionary(property => property.Name, property => property.Value));
}
