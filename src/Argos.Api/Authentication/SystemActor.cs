namespace Argos.Api.Authentication;

/// <summary>
/// Ator de sistema autenticado pela chave de sistema (D-34). O papel técnico <see cref="Role"/> não é um perfil de
/// usuário e só satisfaz a política <c>telemetry.ingest</c>.
/// </summary>
internal static class SystemActor
{
    public const string Name = "PROMETHEUS";
    public const string Role = "System";
}
