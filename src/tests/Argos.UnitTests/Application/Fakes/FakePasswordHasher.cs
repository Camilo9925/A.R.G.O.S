using Argos.Application.Common.Abstractions;

namespace Argos.UnitTests.Application.Fakes;

/// <summary>
/// Hash reversível e previsível, só para testes. Conta as verificações feitas.
/// </summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public int VerifyCalls { get; private set; }

    public string Hash(string password) => $"hash:{password}";

    public bool Verify(string passwordHash, string password)
    {
        VerifyCalls++;
        return passwordHash == Hash(password);
    }
}
