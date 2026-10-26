namespace Argos.Contracts.Realtime;

/// <summary>
/// Endereço do hub de tempo real e nomes das mensagens que ele envia aos clientes (plan 4.2).
/// </summary>
public static class OperationsHubContract
{
    public const string Path = "/hubs/operations";

    public static class Messages
    {
        /// <summary><c>TelemetryBatchProcessed</c>, para todos (plan 16.11).</summary>
        public const string TelemetryBatchProcessed = "TelemetryBatchProcessed";

        /// <summary><c>AgentPositionsUpdated</c>, só para <c>agents.positions.view</c> (RN-27).</summary>
        public const string AgentPositionsUpdated = "AgentPositionsUpdated";

        /// <summary><c>OperationsChanged</c>, para todos (D-80).</summary>
        public const string OperationsChanged = "OperationsChanged";
    }
}
