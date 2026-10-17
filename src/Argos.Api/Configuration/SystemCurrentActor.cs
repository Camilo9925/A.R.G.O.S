using Argos.Application.Common.Abstractions;

namespace Argos.Api.Configuration;

/// <summary>
/// Autor fixo da Fase 0, antes da autenticação: toda operação é atribuída ao sistema.
/// </summary>
internal sealed class SystemCurrentActor : ICurrentActor
{
    public string Name => "sistema";
}
