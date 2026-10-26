using System.ComponentModel.DataAnnotations;

namespace Argos.Contracts.Operations;

/// <summary>
/// Alerta aberto da Central (US-15, RF-035 a RF-037; RN-18 a RN-21; plan 16.10). A posição é a do recurso como ele
/// aparece no mapa, para centralizar (RF-037). <see cref="AllowedActions"/> vem pronta (RN-20, D-59): vazia para quem
/// não trata alertas.
/// </summary>
public sealed record AlertSummary
{
    public Guid Id { get; init; }

    public AlertCause Cause { get; init; }

    public AlertSeverity Severity { get; init; }

    public AlertState State { get; init; }

    /// <summary>"Equipamento furtado" ou "Equipamento em mau funcionamento".</summary>
    public string Title { get; init; } = string.Empty;

    public ResourceKind ResourceKind { get; init; }

    public Guid ResourceId { get; init; }

    /// <summary>"{tipo} · {número de série}".</summary>
    public string ResourceLabel { get; init; } = string.Empty;

    /// <summary>Falso para alerta de cadastro inativo, que continua listado até ser resolvido (D-77).</summary>
    public bool ResourceIsActive { get; init; }

    public string NeighborhoodName { get; init; } = string.Empty;

    public string RegionName { get; init; } = string.Empty;

    public double Latitude { get; init; }

    public double Longitude { get; init; }

    public DateTimeOffset OpenedAt { get; init; }

    public DateTimeOffset LastOccurredAt { get; init; }

    /// <summary>Ocorrências com o alerta aberto, contando a abertura (RN-19).</summary>
    public int OccurrenceCount { get; init; }

    public DateTimeOffset? AcknowledgedAt { get; init; }

    public string? AcknowledgedBy { get; init; }

    /// <summary>Crítico listado fora do filtro (RN-21).</summary>
    public bool OutsideFilter { get; init; }

    public IReadOnlyList<AlertAction> AllowedActions { get; init; } = [];
}

/// <summary>Resolução de um alerta (RN-20): a observação é obrigatória, até 500 caracteres.</summary>
public sealed record ResolveAlertRequest
{
    [Required(ErrorMessage = "Informe a observação da resolução.")]
    [StringLength(500, ErrorMessage = "A observação deve ter no máximo 500 caracteres.")]
    public string? Notes { get; set; }
}
