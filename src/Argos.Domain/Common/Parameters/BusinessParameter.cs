namespace Argos.Domain.Common.Parameters;

/// <summary>
/// Parâmetros de negócio ajustáveis pelo Administrador (RN-34; D-55; plan 15.10). Esta rodada só usa o prazo de
/// manutenção próxima; as rodadas seguintes acrescentam os demais.
/// </summary>
public enum BusinessParameter
{
    /// <summary>Prazo, em dias, para uma manutenção ser considerada próxima (RN-10).</summary>
    MaintenanceUpcomingWindowDays,

    /// <summary>Semanas de histórico mínimas para a previsão de uma região (RN-29).</summary>
    PredictiveMinimumHistoryWeeks,

    /// <summary>Índice a partir do qual o risco é Moderado (RN-30).</summary>
    RiskModerateFrom,

    /// <summary>Índice a partir do qual o risco é Alto (RN-30).</summary>
    RiskHighFrom,

    /// <summary>Índice a partir do qual o risco é Muito alto (RN-30).</summary>
    RiskVeryHighFrom,
}

/// <summary>Definição de um parâmetro: chave estável, unidade, faixa válida e valor padrão da spec.</summary>
public sealed record BusinessParameterDefinition(
    BusinessParameter Parameter,
    string Key,
    string Unit,
    int Min,
    int Max,
    int Default);

/// <summary>
/// Catálogo dos parâmetros de negócio (plan 15.10): a única fonte da chave, da faixa e do padrão. A tabela
/// <c>business_parameters</c> guarda o valor atual; o Migrator cria só as chaves que faltam (Art. III.9, XII.5).
/// </summary>
public static class BusinessParameterCatalog
{
    private static readonly IReadOnlyDictionary<BusinessParameter, BusinessParameterDefinition> Definitions =
        new Dictionary<BusinessParameter, BusinessParameterDefinition>
        {
            [BusinessParameter.MaintenanceUpcomingWindowDays] = new(
                BusinessParameter.MaintenanceUpcomingWindowDays, "maintenance.upcoming-window-days", "dias", 1, 60, 7),
            [BusinessParameter.PredictiveMinimumHistoryWeeks] = new(
                BusinessParameter.PredictiveMinimumHistoryWeeks, "predictive.minimum-history-weeks", "semanas", 1, 52, 4),
            [BusinessParameter.RiskModerateFrom] = new(
                BusinessParameter.RiskModerateFrom, "predictive.risk-moderate-from", "pontos", 1, 98, 25),
            [BusinessParameter.RiskHighFrom] = new(
                BusinessParameter.RiskHighFrom, "predictive.risk-high-from", "pontos", 2, 99, 50),
            [BusinessParameter.RiskVeryHighFrom] = new(
                BusinessParameter.RiskVeryHighFrom, "predictive.risk-very-high-from", "pontos", 3, 100, 75),
        };

    public static IReadOnlyCollection<BusinessParameterDefinition> All => Definitions.Values.ToArray();

    public static BusinessParameterDefinition Get(BusinessParameter parameter) =>
        Definitions.TryGetValue(parameter, out var definition)
            ? definition
            : throw new ArgumentOutOfRangeException(nameof(parameter), parameter, "Parâmetro desconhecido.");

    public static BusinessParameterDefinition? FindByKey(string key) =>
        Definitions.Values.FirstOrDefault(definition => definition.Key == key);

    /// <summary>Confere a faixa válida (RN-34). Erro no campo <c>value</c>.</summary>
    public static int Validate(BusinessParameter parameter, int value)
    {
        var definition = Get(parameter);

        return value >= definition.Min && value <= definition.Max
            ? value
            : throw new DomainValidationException(
                "value", $"Informe um valor entre {definition.Min} e {definition.Max} {definition.Unit}.");
    }
}
