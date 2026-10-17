namespace Argos.Contracts.Access;

/// <summary>
/// Chave de sistema com que o Simulador se autentica na API como PROMETHEUS (D-34; plan 14.6, 14.9).
/// </summary>
public static class SystemKeyContract
{
    public const string HeaderName = "X-Argos-System-Key";
}
