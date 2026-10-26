using System.ComponentModel.DataAnnotations;
using Argos.Contracts.Inventory;
using Argos.Contracts.Operations;

namespace Argos.Contracts.Telemetry;

/// <summary>Mensagens da validação do lote (plan 16.4), as mesmas na API e na Aplicação.</summary>
public static class TelemetryBatchMessages
{
    public const int MaxItemsPerList = 1000;

    public const string LatitudeRange = "A latitude deve estar entre -90 e 90.";

    public const string LongitudeRange = "A longitude deve estar entre -180 e 180.";

    public const string RecordedAtRequired = "Informe o instante da leitura.";

    public const string TooManyItems = "O lote pode ter no máximo 1.000 itens em cada lista.";

    public const string ReportedAtRequired = "Informe o instante da situação.";

    public const string ReportedAtInFuture = "O instante informado não pode estar no futuro.";

    public const string SentAtRequired = "Informe o instante de envio.";

    public const string IsSimulatedRequired = "Informe se o lote é simulado.";

    public const string AgentRequired = "Informe o agente.";

    public const string StatusRequired = "Escolha a situação do agente.";

    public const string ResourceKindRequired = "Escolha o tipo do recurso.";

    public const string ResourceRequired = "Informe o recurso.";

    public const string EquipmentRequired = "Informe o equipamento.";
}

/// <summary>
/// Lote de telemetria do PROMETHEUS (RF-023, RF-024; D-71; plan 16.4): <c>POST /api/telemetry/batches</c>. Aplicado na
/// ordem situações → posições → sinais de vida, num só <c>SaveChanges</c>. Os erros de formato chegam por campo, como
/// <c>positions[3].latitude</c>; recurso inexistente, inativo ou de categoria errada é ignorado e contado, não é erro.
/// </summary>
public sealed record TelemetryBatchRequest
{
    [Required(ErrorMessage = TelemetryBatchMessages.SentAtRequired)]
    public DateTimeOffset? SentAt { get; set; }

    [Required(ErrorMessage = TelemetryBatchMessages.IsSimulatedRequired)]
    public bool? IsSimulated { get; set; }

    [MaxLength(TelemetryBatchMessages.MaxItemsPerList, ErrorMessage = TelemetryBatchMessages.TooManyItems)]
    public List<AgentStatusReportItem> AgentStatusReports { get; set; } = [];

    [MaxLength(TelemetryBatchMessages.MaxItemsPerList, ErrorMessage = TelemetryBatchMessages.TooManyItems)]
    public List<TelemetryPositionItem> Positions { get; set; } = [];

    [MaxLength(TelemetryBatchMessages.MaxItemsPerList, ErrorMessage = TelemetryBatchMessages.TooManyItems)]
    public List<TelemetryHeartbeatItem> Heartbeats { get; set; } = [];
}

/// <summary>Situação reportada de um agente (RF-023, RN-12). O instante vira o da mudança (plan 16.4).</summary>
public sealed record AgentStatusReportItem
{
    [Required(ErrorMessage = TelemetryBatchMessages.AgentRequired)]
    public Guid? AgentId { get; set; }

    [Required(ErrorMessage = TelemetryBatchMessages.StatusRequired)]
    public AgentStatus? Status { get; set; }

    [Required(ErrorMessage = TelemetryBatchMessages.ReportedAtRequired)]
    public DateTimeOffset? ReportedAt { get; set; }
}

/// <summary>Posição de um equipamento móvel ou de um agente em atividade (RF-007, RF-015).</summary>
public sealed record TelemetryPositionItem
{
    [Required(ErrorMessage = TelemetryBatchMessages.ResourceKindRequired)]
    public ResourceKind? ResourceKind { get; set; }

    [Required(ErrorMessage = TelemetryBatchMessages.ResourceRequired)]
    public Guid? ResourceId { get; set; }

    /// <summary>Obrigatória; ausente, recebe a mesma mensagem da faixa.</summary>
    [Required(ErrorMessage = TelemetryBatchMessages.LatitudeRange)]
    [Range(-90d, 90d, ErrorMessage = TelemetryBatchMessages.LatitudeRange)]
    public double? Latitude { get; set; }

    [Required(ErrorMessage = TelemetryBatchMessages.LongitudeRange)]
    [Range(-180d, 180d, ErrorMessage = TelemetryBatchMessages.LongitudeRange)]
    public double? Longitude { get; set; }

    [Required(ErrorMessage = TelemetryBatchMessages.RecordedAtRequired)]
    public DateTimeOffset? RecordedAt { get; set; }
}

/// <summary>Sinal de vida de um equipamento fixo (RF-024).</summary>
public sealed record TelemetryHeartbeatItem
{
    [Required(ErrorMessage = TelemetryBatchMessages.EquipmentRequired)]
    public Guid? EquipmentId { get; set; }

    [Required(ErrorMessage = TelemetryBatchMessages.RecordedAtRequired)]
    public DateTimeOffset? RecordedAt { get; set; }
}

/// <summary>Resultado do lote aceito (<c>202</c>; plan 16.4).</summary>
public sealed record TelemetryBatchResult
{
    public DateTimeOffset ReceivedAt { get; init; }

    public int StatusReportsApplied { get; init; }

    public int PositionsAccepted { get; init; }

    public int HeartbeatsAccepted { get; init; }

    /// <summary>Itens ignorados sem erro: recurso inexistente ou inativo, categoria errada, agente fora de atividade, situação igual.</summary>
    public int Ignored { get; init; }
}
