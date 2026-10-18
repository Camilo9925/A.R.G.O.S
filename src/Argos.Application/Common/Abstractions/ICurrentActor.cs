using Argos.Domain.Access;

namespace Argos.Application.Common.Abstractions;

/// <summary>
/// Autor da operação em curso, pessoa ou sistema, registrado na auditoria (Art. V.1, VIII.4).
/// </summary>
public interface ICurrentActor
{
    string Name { get; }

    /// <summary>
    /// Perfil do usuário autenticado, usado pelas regras que dependem dele, como a reativação (D-47). Nulo para o
    /// sistema: Migrator, serviços em segundo plano e o PROMETHEUS, que não tem perfil de usuário (D-34).
    /// </summary>
    UserRole? Role { get; }
}
