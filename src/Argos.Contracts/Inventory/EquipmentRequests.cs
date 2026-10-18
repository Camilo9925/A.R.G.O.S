using System.ComponentModel.DataAnnotations;

namespace Argos.Contracts.Inventory;

/// <summary>
/// Campos comuns ao cadastro e à edição de equipamento (RF-002, RF-003, RF-006, RF-007; plan 15.14). O fixo informa
/// <see cref="Location"/> e o móvel, <see cref="BaseId"/>; quem confere é o Domínio, conforme o tipo. As
/// características vão em <see cref="Attributes"/>, no texto invariante: inteiro e decimal com ponto (<c>"12.5"</c>),
/// <c>"true"</c>/<c>"false"</c>, a chave da opção e, na múltipla escolha, as chaves separadas por vírgula
/// (<c>"cash,pix"</c>). Os erros chegam por campo, como <c>attributes.maxSpeedKmh</c> e <c>location.latitude</c> (D-46).
/// </summary>
public abstract record EquipmentRegistrationRequest
{
    [Required(ErrorMessage = "Informe o número de série.")]
    [StringLength(50, ErrorMessage = "O número de série deve ter no máximo {1} caracteres.")]
    public string SerialNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a marca.")]
    [StringLength(60, ErrorMessage = "A marca deve ter no máximo {1} caracteres.")]
    public string Brand { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o modelo.")]
    [StringLength(60, ErrorMessage = "O modelo deve ter no máximo {1} caracteres.")]
    public string Model { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escolha o órgão responsável.")]
    public Guid? OrganizationId { get; set; }

    /// <summary>Só para equipamento fixo.</summary>
    public LocationInput? Location { get; set; }

    /// <summary>Só para equipamento móvel.</summary>
    public Guid? BaseId { get; set; }

    public Dictionary<string, string?> Attributes { get; set; } = [];
}

/// <summary>
/// Cadastro de equipamento (US-01, US-02). A situação inicial aceita é Ainda não instalado, Operacional, Mau
/// funcionamento ou Em manutenção (D-66). A manutenção inicial, opcional, é a "necessidade de manutenção" do RF-002;
/// os erros dela chegam em <c>initialMaintenance.*</c>.
/// </summary>
public sealed record CreateEquipmentRequest : EquipmentRegistrationRequest
{
    [Required(ErrorMessage = "Escolha o tipo do equipamento.")]
    public Guid? TypeId { get; set; }

    [Required(ErrorMessage = "Escolha a situação inicial.")]
    public EquipmentStatus? Status { get; set; }

    public RegisterMaintenanceRequest? InitialMaintenance { get; set; }
}

/// <summary>
/// Edição do cadastro (RF-001). Tipo e categoria não mudam (D-66); a situação muda por
/// <see cref="ChangeEquipmentStatusRequest"/>. <see cref="Version"/> é a dos detalhes lidos (D-60).
/// </summary>
public sealed record UpdateEquipmentRequest : EquipmentRegistrationRequest
{
    [Required(ErrorMessage = "Informe a versão do cadastro lida.")]
    public uint? Version { get; set; }
}

/// <summary>Mudança de situação (US-04, RF-008). A justificativa é exigida para Furtado, Desativado e a reativação (RN-06).</summary>
public sealed record ChangeEquipmentStatusRequest
{
    [Required(ErrorMessage = "Escolha a nova situação.")]
    public EquipmentStatus? Status { get; set; }

    [StringLength(500, ErrorMessage = "A justificativa deve ter no máximo {1} caracteres.")]
    public string? Justification { get; set; }
}

/// <summary>
/// Registro de manutenção (US-05, RF-009). A imediata não tem data prevista nem periodicidade; a agendada exige a data;
/// a periódica exige data e periodicidade (RN-08).
/// </summary>
public sealed record RegisterMaintenanceRequest
{
    [Required(ErrorMessage = "Escolha o tipo da manutenção.")]
    public MaintenanceKind? Kind { get; set; }

    /// <summary>Data prevista, no calendário do município (D-54).</summary>
    public DateOnly? DueDate { get; set; }

    [Range(1, 3650, ErrorMessage = "A periodicidade deve estar entre {1} e {2} dias.")]
    public int? PeriodicityDays { get; set; }

    [StringLength(500, ErrorMessage = "A descrição deve ter no máximo {1} caracteres.")]
    public string? Description { get; set; }
}

/// <summary>Conclusão de manutenção (US-05): resultado, instante e observações (RN-09, RN-11).</summary>
public sealed record CompleteMaintenanceRequest
{
    [Required(ErrorMessage = "Informe se a manutenção teve sucesso.")]
    public MaintenanceResult? Result { get; set; }

    [Required(ErrorMessage = "Informe quando a manutenção foi concluída.")]
    public DateTimeOffset? CompletedAt { get; set; }

    [StringLength(1000, ErrorMessage = "As observações devem ter no máximo {1} caracteres.")]
    public string? Notes { get; set; }
}
