using System.ComponentModel.DataAnnotations;

namespace Argos.Simulator.Configuration;

/// <summary>
/// Configuração do Simulador, validada na inicialização (plan 6.4).
/// </summary>
internal sealed class SimulatorOptions
{
    public const string SectionName = "Simulator";

    [Required(ErrorMessage = "Informe Simulator:ApiBaseUrl com o endereço da API do ARGOS.")]
    [Url(ErrorMessage = "Simulator:ApiBaseUrl deve ser uma URL válida.")]
    public string ApiBaseUrl { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe Simulator:Source com o nome da origem dos sinais.")]
    [StringLength(100, ErrorMessage = "Simulator:Source deve ter no máximo {1} caracteres.")]
    public string Source { get; set; } = "argos-simulator";

    [Range(1, 3600, ErrorMessage = "Simulator:HeartbeatIntervalSeconds deve estar entre {1} e {2}.")]
    public int HeartbeatIntervalSeconds { get; set; } = 5;
}
