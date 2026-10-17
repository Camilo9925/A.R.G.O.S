namespace Argos.Application.Common.Abstractions;

/// <summary>
/// Quem recebe uma mensagem de tempo real (D-38; plan 14.8): todos os usuários conectados ou só os perfis com uma
/// permissão. A Aplicação decide; a API só traduz para os grupos do hub. É assim que a posição de agentes ficará
/// restrita a Administrador e Operador (RN-27).
/// </summary>
public sealed class RealtimeAudience
{
    private RealtimeAudience(string? permission) => Permission = permission;

    /// <summary>Todos os usuários conectados.</summary>
    public static RealtimeAudience Everyone { get; } = new(null);

    /// <summary>Nome da permissão (<c>Permissions</c>) exigida, ou <c>null</c> para todos.</summary>
    public string? Permission { get; }

    /// <summary>Só os perfis que têm a permissão de nome informado (<c>Permissions</c>).</summary>
    public static RealtimeAudience WithPermission(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        return new RealtimeAudience(permission);
    }
}
