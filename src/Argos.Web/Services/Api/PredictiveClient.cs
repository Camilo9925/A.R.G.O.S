using Argos.Contracts.Predictive;

namespace Argos.Web.Services.Api;

/// <summary>
/// Cidade Preditiva (plan 17.11): grade de risco de uma região e mapa das regiões por horizonte. A política
/// <c>predictive.view</c> é conferida pela API (D-58); índice, nível, faixas da legenda, confiança, fatores e textos
/// vêm prontos (D-59).
/// </summary>
public sealed class PredictiveClient(HttpClient http) : ApiClient(http)
{
    private const string Base = "api/predictive/risk";

    public Task<RiskGrid> GetGridAsync(Guid regionId, CancellationToken cancellationToken = default) =>
        GetAsync<RiskGrid>($"{Base}/regions/{regionId}/grid", cancellationToken);

    public Task<RiskForecast> GetForecastAsync(ForecastHorizon horizon, CancellationToken cancellationToken = default) =>
        GetAsync<RiskForecast>($"{Base}/forecast?horizon={horizon}", cancellationToken);
}
