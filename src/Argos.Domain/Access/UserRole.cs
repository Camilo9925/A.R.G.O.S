namespace Argos.Domain.Access;

/// <summary>
/// Perfil de um usuário (RF-022, RN-26). O ator de sistema PROMETHEUS não é um perfil de usuário (D-34).
/// </summary>
public enum UserRole
{
    Administrator,
    Operator,
    Manager,
}
