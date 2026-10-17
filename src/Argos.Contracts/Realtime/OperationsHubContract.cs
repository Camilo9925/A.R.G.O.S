namespace Argos.Contracts.Realtime;

/// <summary>
/// Endereço do hub de tempo real e nomes das mensagens que ele envia aos clientes (plan 4.2).
/// </summary>
public static class OperationsHubContract
{
    public const string Path = "/hubs/operations";

    public static class Messages
    {
        public const string TechnicalHeartbeatReceived = "TechnicalHeartbeatReceived";
    }
}
