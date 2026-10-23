using System.ComponentModel.DataAnnotations;

namespace Argos.Contracts.Inventory;

/// <summary>
/// Inativação de equipamento ou de agente (US-09, RN-35; plan 15.14). O motivo é opcional e fica no registro cadastral
/// (D-49). Só é aceita nas situações da RN-35; fora delas, o <c>409</c> explica em quais é possível. Não existe
/// exclusão (RN-23).
/// </summary>
public sealed record InactivationRequest
{
    [StringLength(500, ErrorMessage = "O motivo deve ter no máximo {1} caracteres.")]
    public string? Reason { get; set; }
}
