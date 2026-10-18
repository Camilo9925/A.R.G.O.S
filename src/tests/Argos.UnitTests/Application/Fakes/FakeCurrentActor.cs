using Argos.Application.Common.Abstractions;
using Argos.Domain.Access;

namespace Argos.UnitTests.Application.Fakes;

/// <summary>
/// Autor da operação escolhido pelo teste: um usuário com perfil ou, com <see cref="Role"/> nulo, o sistema.
/// </summary>
internal sealed class FakeCurrentActor(string name = "admin", UserRole? role = UserRole.Administrator) : ICurrentActor
{
    public string Name { get; set; } = name;

    public UserRole? Role { get; set; } = role;
}
