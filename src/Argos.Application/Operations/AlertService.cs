using Argos.Application.Common.Abstractions;
using Argos.Application.Common.Exceptions;
using Argos.Contracts.Operations;
using Argos.Domain.Operations.Alerts;

namespace Argos.Application.Operations;

/// <summary>
/// Reconhecer e resolver alertas (US-15, RF-036, RN-20; plan 16.6). As transições, as recusas e a observação ficam no
/// agregado <see cref="Alert"/>; o autor vem do <see cref="ICurrentActor"/>, e a concorrência pela versão do alerta
/// (reconhecimento simultâneo → 409, pela unidade de trabalho). Depois da gravação, avisa a Central (D-80).
/// </summary>
public sealed class AlertService(
    IAlertRepository alerts,
    IOperationsQueries queries,
    IUnitOfWork unitOfWork,
    IRealtimeNotifier notifier,
    ICurrentActor actor,
    TimeProvider timeProvider)
{
    public const string NotFoundMessage = "Alerta não encontrado.";

    public const string ForbiddenMessage = "Seu perfil não pode tratar alertas.";

    /// <exception cref="NotFoundException">Alerta inexistente.</exception>
    /// <exception cref="Domain.Operations.OperationsRuleException">Já reconhecido ou já resolvido (RN-20).</exception>
    public Task<AlertSummary> AcknowledgeAsync(Guid id, CancellationToken cancellationToken = default) =>
        HandleAsync(id, (alert, now) => alert.Acknowledge(now, actor.Name), cancellationToken);

    /// <exception cref="NotFoundException">Alerta inexistente.</exception>
    /// <exception cref="Domain.Common.DomainValidationException">Observação ausente ou longa demais, em <c>notes</c>.</exception>
    /// <exception cref="Domain.Operations.OperationsRuleException">Já resolvido ou crítico não reconhecido (RN-20).</exception>
    public Task<AlertSummary> ResolveAsync(Guid id, ResolveAlertRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return HandleAsync(id, (alert, now) => alert.Resolve(request.Notes, now, actor.Name), cancellationToken);
    }

    private async Task<AlertSummary> HandleAsync(Guid id, Action<Alert, DateTimeOffset> operation, CancellationToken cancellationToken)
    {
        if (!OperationsPermissions.CanHandleAlerts(actor))
        {
            throw new ForbiddenOperationException(ForbiddenMessage);
        }

        var alert = await alerts.FindByIdAsync(id, cancellationToken) ?? throw new NotFoundException(NotFoundMessage);
        var now = timeProvider.GetUtcNow();

        operation(alert, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.PublishOperationsChangedAsync(OperationsChangeKind.Alert, now, cancellationToken);

        var row = await queries.FindAlertAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Alerta {id} gravado e não encontrado pela consulta.");
        return OperationsMapping.ToSummary(
            row, outsideFilter: false, canHandleAlerts: true, OperationsPermissions.CanSeeAgentPositions(actor));
    }
}
