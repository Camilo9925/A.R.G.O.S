using Argos.Domain.Operations.Alerts;
using Argos.Domain.Telemetry;

namespace Argos.Application.Operations;

/// <summary>
/// Porta de persistência dos alertas (plan 16.8). Sem remoção (RN-23): o alerta é resolvido, nunca apagado. O índice
/// único parcial do banco garante a RN-19 mesmo com duas mudanças simultâneas (plan 16.9).
/// </summary>
public interface IAlertRepository
{
    Task<Alert?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>O alerta aberto (Aberto ou Reconhecido) da causa no recurso, se houver; há no máximo um (RN-19).</summary>
    Task<Alert?> FindOpenAsync(
        ResourceKind resourceKind, Guid resourceId, AlertCause cause, CancellationToken cancellationToken = default);

    void Add(Alert alert);
}
