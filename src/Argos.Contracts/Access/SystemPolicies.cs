namespace Argos.Contracts.Access;

/// <summary>
/// Políticas do ator de sistema PROMETHEUS, autenticado pela chave de sistema (D-34). Ficam fora de
/// <see cref="Permissions"/> porque nenhum perfil de usuário as recebe.
/// </summary>
public static class SystemPolicies
{
    /// <summary>Envio de telemetria à API (hoje, o sinal de vida técnico).</summary>
    public const string TelemetryIngest = "telemetry.ingest";
}
