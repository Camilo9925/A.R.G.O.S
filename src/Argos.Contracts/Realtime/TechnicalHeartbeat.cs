using System.ComponentModel.DataAnnotations;

namespace Argos.Contracts.Realtime;

/// <summary>
/// Sinal de vida técnico enviado pelo Simulador à API (temporário, Fase 0).
/// </summary>
public sealed record TechnicalHeartbeat
{
    [Required(ErrorMessage = "A origem é obrigatória.")]
    [StringLength(100, ErrorMessage = "A origem deve ter no máximo {1} caracteres.")]
    public string Source { get; init; } = string.Empty;

    [Range(1, long.MaxValue, ErrorMessage = "A sequência deve ser maior que zero.")]
    public long Sequence { get; init; }

    [Required(ErrorMessage = "O instante de envio é obrigatório.")]
    public DateTimeOffset? SentAt { get; init; }
}
