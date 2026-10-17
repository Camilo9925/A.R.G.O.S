using Argos.Application.Common.Abstractions;

namespace Argos.Migrator.Configuration;

/// <summary>
/// A carga inicial é sempre atribuída ao sistema.
/// </summary>
internal sealed class SystemCurrentActor : ICurrentActor
{
    public string Name => "sistema";
}
