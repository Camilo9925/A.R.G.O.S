using Argos.Application.Common.Abstractions;
using Argos.Domain.Access;

namespace Argos.Migrator.Configuration;

/// <summary>
/// A carga inicial é sempre atribuída ao sistema, que não tem perfil de usuário.
/// </summary>
internal sealed class SystemCurrentActor : ICurrentActor
{
    public string Name => "sistema";

    public UserRole? Role => null;
}
