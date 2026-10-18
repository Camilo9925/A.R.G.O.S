using Argos.Application.Common.Exceptions;
using Argos.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Argos.IntegrationTests.Infrastructure;

/// <summary>
/// Sonda de erros (plan 6.1, 15.7): cada ação lança uma exceção, para verificar por HTTP a tradução do
/// <c>GlobalExceptionHandler</c> antes de existirem os endpoints do Inventário. Como a sonda de políticas
/// (<see cref="PolicyProbeController"/>), existe só neste projeto e entra na API em teste como <c>ApplicationPart</c>.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route(RoutePrefix)]
public sealed class ErrorProbeController : ControllerBase
{
    public const string RoutePrefix = "api/test/error-probe";

    public static readonly Guid ExistingEquipmentId = Guid.Parse("6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7");

    [HttpGet("domain-validation")]
    public IActionResult DomainValidation() =>
        throw new DomainValidationException("serialNumber", "Informe o número de série.");

    [HttpGet("domain-validation-several")]
    public IActionResult DomainValidationSeveral() =>
        throw new DomainValidationException(new Dictionary<string, string[]>
        {
            ["attributes.maxSpeedKmh"] = ["Informe a velocidade máxima."],
            ["attributes.lanes"] = ["Informe um valor entre 1 e 8."],
        });

    [HttpGet("domain-validation-nested")]
    public IActionResult DomainValidationNested() =>
        DomainFields.Under<IActionResult>("location", () => throw new DomainValidationException("zipCode", "CEP inválido."));

    [HttpGet("domain-rule")]
    public IActionResult DomainRule() => throw new ProbeDomainException("Não é permitido passar de Furtado para Operacional.");

    [HttpGet("forbidden-operation")]
    public IActionResult ForbiddenOperation() =>
        throw new ForbiddenOperationException("Somente o Administrador pode reativar um equipamento desativado.");

    [HttpGet("conflict-with-extensions")]
    public IActionResult ConflictWithExtensions() =>
        throw new ConflictException(
            "Já existe um equipamento com essa marca e esse número de série.",
            new Dictionary<string, object?> { ["existingEquipmentId"] = ExistingEquipmentId });

    [HttpGet("conflict")]
    public IActionResult PlainConflict() => throw new ConflictException("O registro foi alterado por outra pessoa.");

    /// <summary>Exceção de domínio sem campo, como a de um módulo.</summary>
    private sealed class ProbeDomainException(string message) : DomainException(message);
}
