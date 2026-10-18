using Argos.Application.Common.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Argos.IntegrationTests.Infrastructure;

/// <summary>
/// Sonda do autor (D-43, D-47): devolve o <see cref="ICurrentActor"/> da requisição, para verificar por HTTP o nome
/// e o perfil lidos do token. Existe só neste projeto, como as demais sondas, e exige só um usuário autenticado ou a
/// chave de sistema (política padrão, D-33).
/// </summary>
[ApiController]
[Route(Path)]
public sealed class ActorProbeController(ICurrentActor currentActor) : ControllerBase
{
    public const string Path = "api/test/actor-probe";

    [HttpGet]
    public ActorProbeResult Get() => new(currentActor.Name, currentActor.Role?.ToString());
}

public sealed record ActorProbeResult(string Name, string? Role);
