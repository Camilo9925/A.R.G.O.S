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

    /// <summary>Chave de sistema enviada em toda requisição à API, que identifica o PROMETHEUS (D-34).</summary>
    [Required(ErrorMessage = "Informe Simulator:SystemKey (ARGOS_SYSTEM_KEY no .env, ou user-secrets em desenvolvimento).")]
    [MinLength(32, ErrorMessage = "Simulator:SystemKey deve ter no mínimo {1} caracteres.")]
    public string SystemKey { get; set; } = string.Empty;
}

/// <summary>
/// Opções <c>Simulator:Telemetry</c> (plan 16.5; D-76): intervalos e velocidades da telemetria simulada, validados na
/// subida com valores maiores que zero. São configurações técnicas da simulação, não parâmetros de negócio (Art. III.9).
/// </summary>
internal sealed class TelemetryOptions
{
    public const string SectionName = "Simulator:Telemetry";

    /// <summary>Intervalo entre os lotes (padrão 30 s, D-25; 5 s no ambiente <c>Demo</c>, D-76).</summary>
    [Range(1, 3600, ErrorMessage = "Simulator:Telemetry:PositionIntervalSeconds deve estar entre {1} e {2}.")]
    public int PositionIntervalSeconds { get; set; } = 30;

    /// <summary>Intervalo dos sinais de vida dos fixos (padrão 300 s; 60 s no <c>Demo</c>, D-76).</summary>
    [Range(1, 86400, ErrorMessage = "Simulator:Telemetry:FixedHeartbeatIntervalSeconds deve estar entre {1} e {2}.")]
    public int FixedHeartbeatIntervalSeconds { get; set; } = 300;

    [Required(ErrorMessage = "Informe Simulator:Telemetry:SpeedsKmh.")]
    public SpeedOptions SpeedsKmh { get; set; } = new();

    /// <summary>Velocidades por meio de deslocamento, em km/h (plan 16.5).</summary>
    internal sealed class SpeedOptions
    {
        [Range(0.1, 200, ErrorMessage = "Simulator:Telemetry:SpeedsKmh:Vehicle deve estar entre {1} e {2}.")]
        public double Vehicle { get; set; } = 30;

        [Range(0.1, 200, ErrorMessage = "Simulator:Telemetry:SpeedsKmh:Motorcycle deve estar entre {1} e {2}.")]
        public double Motorcycle { get; set; } = 35;

        [Range(0.1, 200, ErrorMessage = "Simulator:Telemetry:SpeedsKmh:Scooter deve estar entre {1} e {2}.")]
        public double Scooter { get; set; } = 12;

        [Range(0.1, 200, ErrorMessage = "Simulator:Telemetry:SpeedsKmh:OnFoot deve estar entre {1} e {2}.")]
        public double OnFoot { get; set; } = 4;
    }
}
