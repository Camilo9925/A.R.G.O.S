using Argos.Domain.Common;
using Argos.Domain.Telemetry;

namespace Argos.Domain.Operations.Alerts;

/// <summary>
/// Alerta da Central (US-15, RF-035, RF-036, RN-18 a RN-20, RN-23; D-77; plan 16.2, 16.3, 16.6). Aponta o recurso por
/// tipo + id, como a telemetria. Aberto na mesma unidade de trabalho da mudança de situação que o causou; enquanto
/// aberto, uma nova ocorrência da mesma causa no mesmo recurso o atualiza (RN-19) em vez de abrir outro.
/// <para>
/// <b>RN-20:</b> Aberto → Reconhecido → Resolvido; o não crítico pode ser resolvido sem reconhecimento. Reconhecimento
/// e resolução são gravados uma única vez, com instante e autor. Não existe exclusão (RN-23), e voltar a Operacional
/// não resolve o alerta: a resolução é humana (plan 16.6).
/// </para>
/// </summary>
public sealed class Alert : IAuditable
{
    public const int ResolutionNotesMaxLength = 500;

    public const string AlreadyAcknowledgedMessage = "O alerta já foi reconhecido.";

    public const string AlreadyResolvedMessage = "O alerta já foi resolvido.";

    public const string CriticalNeedsAcknowledgementMessage =
        "Alertas críticos precisam ser reconhecidos antes de serem resolvidos.";

    public const string NotesRequiredMessage = "Informe a observação da resolução.";

    public const string NotesTooLongMessage = "A observação deve ter no máximo 500 caracteres.";

    // Usado pelo EF Core ao materializar.
    private Alert()
    {
    }

    private Alert(AlertDefinition definition, ResourceKind resourceKind, Guid resourceId, DateTimeOffset at, string author)
    {
        Id = Guid.NewGuid();
        Cause = definition.Cause;
        Severity = definition.Severity;
        ResourceKind = resourceKind;
        ResourceId = resourceId;
        State = AlertState.Open;
        OpenedAt = at.ToUniversalTime();
        OpenedBy = author;
        LastOccurredAt = OpenedAt;
        OccurrenceCount = 1;
    }

    public Guid Id { get; private set; }

    public AlertCause Cause { get; private set; }

    public AlertSeverity Severity { get; private set; }

    public ResourceKind ResourceKind { get; private set; }

    public Guid ResourceId { get; private set; }

    public AlertState State { get; private set; }

    public DateTimeOffset OpenedAt { get; private set; }

    /// <summary>Autor da mudança que causou o alerta: login, <c>PROMETHEUS</c>, <c>sistema</c> ou <c>simulação</c>.</summary>
    public string OpenedBy { get; private set; } = string.Empty;

    /// <summary>Instante da ocorrência mais recente (RN-19).</summary>
    public DateTimeOffset LastOccurredAt { get; private set; }

    /// <summary>Quantas vezes a causa ocorreu com o alerta aberto, contando a abertura (RN-19).</summary>
    public int OccurrenceCount { get; private set; }

    public DateTimeOffset? AcknowledgedAt { get; private set; }

    public string? AcknowledgedBy { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public string? ResolvedBy { get; private set; }

    public string? ResolutionNotes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; private set; }

    public string? UpdatedBy { get; private set; }

    /// <summary>Aberto ou reconhecido, ainda não resolvido (plan 16.1).</summary>
    public bool IsOpen => State != AlertState.Resolved;

    /// <summary>
    /// Ações aceitas agora (RN-20; D-59): Aberto → Reconhecer (e Resolver, se não for crítico); Reconhecido → Resolver;
    /// Resolvido → nenhuma. A Aplicação esvazia a lista para quem não tem <c>alerts.handle</c>.
    /// </summary>
    public IReadOnlyList<AlertAction> AllowedActions => ActionsFor(State, Severity);

    /// <summary>A mesma regra de <see cref="AllowedActions"/>, para as linhas lidas pela consulta, sem o agregado.</summary>
    public static IReadOnlyList<AlertAction> ActionsFor(AlertState state, AlertSeverity severity) => state switch
    {
        AlertState.Open when severity == AlertSeverity.Critical => [AlertAction.Acknowledge],
        AlertState.Open => [AlertAction.Acknowledge, AlertAction.Resolve],
        AlertState.Acknowledged => [AlertAction.Resolve],
        _ => [],
    };

    /// <summary>Abre o alerta da causa no recurso (RN-18), com uma ocorrência.</summary>
    public static Alert Open(AlertDefinition definition, ResourceKind resourceKind, Guid resourceId, DateTimeOffset at, string by)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!Enum.IsDefined(resourceKind))
        {
            throw new ArgumentOutOfRangeException(nameof(resourceKind), resourceKind, "Tipo de recurso desconhecido.");
        }

        return new Alert(definition, resourceKind, resourceId, at, ValidAuthor(by));
    }

    /// <summary>
    /// RN-19: nova ocorrência da mesma causa com o alerta aberto. Soma uma ocorrência e avança o instante da última; o
    /// estado não muda (um alerta reconhecido continua reconhecido).
    /// </summary>
    /// <exception cref="OperationsRuleException">Alerta resolvido: a nova ocorrência abre outro alerta.</exception>
    public void Recur(DateTimeOffset at)
    {
        EnsureNotResolved();
        OccurrenceCount++;

        var occurredAt = at.ToUniversalTime();
        if (occurredAt > LastOccurredAt)
        {
            LastOccurredAt = occurredAt;
        }
    }

    /// <summary>RN-20: reconhece o alerta aberto, uma única vez, com instante e autor.</summary>
    /// <exception cref="OperationsRuleException">Alerta já reconhecido ou já resolvido.</exception>
    public void Acknowledge(DateTimeOffset at, string by)
    {
        var author = ValidAuthor(by);
        EnsureNotResolved();

        if (State == AlertState.Acknowledged)
        {
            throw new OperationsRuleException(AlreadyAcknowledgedMessage);
        }

        State = AlertState.Acknowledged;
        AcknowledgedAt = at.ToUniversalTime();
        AcknowledgedBy = author;
    }

    /// <summary>
    /// RN-20: resolve o alerta com uma observação obrigatória, até <see cref="ResolutionNotesMaxLength"/> caracteres. O
    /// erro do campo vem antes das recusas sem campo, como nas demais operações (D-46). Crítico só depois de reconhecido.
    /// </summary>
    /// <exception cref="DomainValidationException">Observação ausente ou longa demais, no campo <c>notes</c>.</exception>
    /// <exception cref="OperationsRuleException">Alerta já resolvido ou crítico não reconhecido.</exception>
    public void Resolve(string? notes, DateTimeOffset at, string by)
    {
        var author = ValidAuthor(by);
        var trimmed = notes?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainValidationException("notes", NotesRequiredMessage);
        }

        if (trimmed.Length > ResolutionNotesMaxLength)
        {
            throw new DomainValidationException("notes", NotesTooLongMessage);
        }

        EnsureNotResolved();

        if (State == AlertState.Open && Severity == AlertSeverity.Critical)
        {
            throw new OperationsRuleException(CriticalNeedsAcknowledgementMessage);
        }

        State = AlertState.Resolved;
        ResolvedAt = at.ToUniversalTime();
        ResolvedBy = author;
        ResolutionNotes = trimmed;
    }

    private void EnsureNotResolved()
    {
        if (State == AlertState.Resolved)
        {
            throw new OperationsRuleException(AlreadyResolvedMessage);
        }
    }

    private static string ValidAuthor(string by) =>
        string.IsNullOrWhiteSpace(by)
            ? throw new ArgumentException("Informe o autor da operação.", nameof(by))
            : by.Trim();
}
