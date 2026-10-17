namespace Argos.Application.Common.Abstractions;

/// <summary>
/// Porta de hash de senha (plan 9.6, 14.4). O Domínio guarda só o hash opaco; quem calcula e verifica é a
/// Infraestrutura (Art. III.2).
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);
}
