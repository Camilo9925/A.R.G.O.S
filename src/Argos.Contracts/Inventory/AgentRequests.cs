using System.ComponentModel.DataAnnotations;

namespace Argos.Contracts.Inventory;

/// <summary>
/// Campos comuns ao cadastro e à edição de agente (RF-014, US-06; plan 15.2.9, 15.14). <b>RN-25 (LGPD):</b> só os dados
/// mínimos da operação; não há documento pessoal, endereço residencial, telefone nem saúde, e um teste unitário
/// compara as propriedades com uma lista fechada. Formato da matrícula, órgão e base ativos ficam com o Domínio, que
/// devolve os erros por campo (D-46).
/// </summary>
public abstract record AgentRegistrationRequest
{
    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "O nome deve ter entre {2} e {1} caracteres.")]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Nome de exibição ou de guerra, o que aparece no mapa e nos detalhes (US-14).</summary>
    [Required(ErrorMessage = "Informe o nome de exibição.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "O nome de exibição deve ter entre {2} e {1} caracteres.")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Matrícula, única por órgão (US-06).</summary>
    [Required(ErrorMessage = "Informe a matrícula.")]
    [StringLength(20, ErrorMessage = "A matrícula deve ter no máximo {1} caracteres.")]
    public string RegistrationNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escolha a função do agente.")]
    public AgentRole? Role { get; set; }

    [Required(ErrorMessage = "Escolha o órgão do agente.")]
    public Guid? OrganizationId { get; set; }

    /// <summary>Posto ou graduação, "quando aplicável" (RF-014).</summary>
    [StringLength(50, ErrorMessage = "O posto ou graduação deve ter no máximo {1} caracteres.")]
    public string? Rank { get; set; }

    /// <summary>Base de atuação (RF-014); a região do agente é a do bairro da base (D-65).</summary>
    [Required(ErrorMessage = "Escolha a base de atuação do agente.")]
    public Guid? BaseId { get; set; }
}

/// <summary>Cadastro de agente (US-06). Todo agente nasce Fora de serviço (RN-12), por isso não há situação aqui.</summary>
public sealed record CreateAgentRequest : AgentRegistrationRequest;

/// <summary>
/// Edição do cadastro do agente (RF-013). A situação muda por <see cref="ChangeAgentStatusRequest"/>.
/// <see cref="Version"/> é a dos detalhes lidos (D-60).
/// </summary>
public sealed record UpdateAgentRequest : AgentRegistrationRequest
{
    [Required(ErrorMessage = "Informe a versão do cadastro lida.")]
    public uint? Version { get; set; }
}

/// <summary>Mudança de situação do agente (RF-015, RN-12): qualquer situação diferente da atual, sem justificativa.</summary>
public sealed record ChangeAgentStatusRequest
{
    [Required(ErrorMessage = "Escolha a nova situação.")]
    public AgentStatus? Status { get; set; }
}

/// <summary>
/// Vínculo do agente com um equipamento móvel (US-07, RF-016; D-62). Um vínculo aberto com outro equipamento é
/// encerrado na mesma gravação.
/// </summary>
public sealed record AssignEquipmentRequest
{
    [Required(ErrorMessage = "Escolha o equipamento.")]
    public Guid? EquipmentId { get; set; }
}
