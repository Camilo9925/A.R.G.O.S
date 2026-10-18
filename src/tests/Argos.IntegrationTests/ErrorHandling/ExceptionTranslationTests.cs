using System.Net;
using System.Text.Json;
using Argos.IntegrationTests.Infrastructure;

namespace Argos.IntegrationTests.ErrorHandling;

/// <summary>
/// Tradução das exceções em <c>ProblemDetails</c> (Art. XII.4; D-05, D-46, D-47; plan 6.1, 15.7), pela sonda de erros:
/// exceção de domínio com campo → 400 com o campo; sem campo → 409; operação negada pelo perfil → 403 com o motivo;
/// conflito com extensões → 409 com as extensões.
/// </summary>
public sealed class ExceptionTranslationTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    private const string ValidationTitle = "Um ou mais campos são inválidos.";
    private const string ValidationDetail = "Corrija os campos indicados e tente novamente.";

    [Fact]
    public async Task DomainValidation_ReturnsBadRequestWithField_D46()
    {
        var problem = await GetProblemAsync(
            "domain-validation", HttpStatusCode.BadRequest, ValidationTitle, ValidationDetail);

        var errors = Errors(problem);
        Assert.Equal(["serialNumber"], errors.Keys);
        Assert.Equal(["Informe o número de série."], errors["serialNumber"]);
    }

    [Fact]
    public async Task DomainValidation_SeveralFields_ReturnsEveryFieldAtOnce_D46()
    {
        var problem = await GetProblemAsync(
            "domain-validation-several", HttpStatusCode.BadRequest, ValidationTitle, ValidationDetail);

        Assert.Equal(["attributes.lanes", "attributes.maxSpeedKmh"], Errors(problem).Keys.Order());
    }

    [Fact]
    public async Task DomainValidation_NestedObject_ReturnsPrefixedField_D46()
    {
        var problem = await GetProblemAsync(
            "domain-validation-nested", HttpStatusCode.BadRequest, ValidationTitle, ValidationDetail);

        Assert.Equal(["location.zipCode"], Errors(problem).Keys);
    }

    [Fact]
    public async Task DomainRuleWithoutField_StillReturnsConflict_D05()
    {
        var problem = await GetProblemAsync(
            "domain-rule",
            HttpStatusCode.Conflict,
            "Operação não permitida.",
            "Não é permitido passar de Furtado para Operacional.");

        Assert.False(problem.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task ForbiddenOperation_ReturnsForbiddenWithReason_D47()
    {
        await GetProblemAsync(
            "forbidden-operation",
            HttpStatusCode.Forbidden,
            ProblemAssert.ForbiddenTitle,
            "Somente o Administrador pode reativar um equipamento desativado.");
    }

    [Fact]
    public async Task ConflictWithExtensions_ReturnsConflictWithExtensions_RN01()
    {
        var problem = await GetProblemAsync(
            "conflict-with-extensions",
            HttpStatusCode.Conflict,
            "Conflito com o estado atual.",
            "Já existe um equipamento com essa marca e esse número de série.");

        Assert.Equal(ErrorProbeController.ExistingEquipmentId, problem.GetProperty("existingEquipmentId").GetGuid());
    }

    [Fact]
    public async Task ConflictWithoutExtensions_ReturnsConflict()
    {
        await GetProblemAsync(
            "conflict",
            HttpStatusCode.Conflict,
            "Conflito com o estado atual.",
            "O registro foi alterado por outra pessoa.");
    }

    private async Task<JsonElement> GetProblemAsync(string action, HttpStatusCode status, string title, string detail)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = Fixture.Factory.CreateClient();

        using var response = await client.GetAsync($"/{ErrorProbeController.RoutePrefix}/{action}", cancellationToken);

        return await ProblemAssert.HasProblemAsync(response, status, title, detail, cancellationToken);
    }

    private static Dictionary<string, string[]> Errors(JsonElement problem) =>
        problem.GetProperty("errors")
            .EnumerateObject()
            .ToDictionary(
                field => field.Name,
                field => field.Value.EnumerateArray().Select(message => message.GetString()!).ToArray());
}
