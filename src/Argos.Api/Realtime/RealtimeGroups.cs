namespace Argos.Api.Realtime;

/// <summary>
/// Grupos do hub, um por perfil de usuário (D-10, D-38; plan 14.8).
/// </summary>
internal static class RealtimeGroups
{
    public static string ForRole(string role) => $"profile:{role}";
}
