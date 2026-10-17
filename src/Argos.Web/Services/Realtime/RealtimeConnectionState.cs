namespace Argos.Web.Services.Realtime;

/// <summary>
/// Estado da conexão de tempo real exibido na Central (Art. V.4; base do RF-042).
/// </summary>
public enum RealtimeConnectionState
{
    Connecting,
    Connected,
    Reconnecting,
    Interrupted,
}
