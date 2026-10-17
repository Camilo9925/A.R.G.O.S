namespace Argos.Contracts.Realtime;

/// <summary>
/// Mensagem do hub que repassa o sinal de vida técnico, com o instante UTC em que a API o recebeu (temporário, Fase 0).
/// </summary>
public sealed record TechnicalHeartbeatNotification(
    string Source,
    long Sequence,
    DateTimeOffset SentAt,
    DateTimeOffset ReceivedAt);
