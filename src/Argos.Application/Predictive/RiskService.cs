using Argos.Application.Common.Abstractions;
using Argos.Application.Common.Exceptions;
using Argos.Application.Inventory;
using Argos.Contracts.Predictive;
using Argos.Domain.Common.Parameters;
using Argos.Domain.Predictive;

namespace Argos.Application.Predictive;

/// <summary>
/// Índice de risco por região, dia da semana e faixa horária, e o mapa das regiões por horizonte (US-20, US-25; RF-043,
/// RF-049; RN-28 a RN-30; D-89 a D-91, D-96; plan 17.5, 17.9). Tudo é recalculado a cada consulta, em memória, pelo
/// <see cref="RiskModel"/> sobre as mesmas ocorrências (Art. V.3): sem cache e sem tabela de previsões. O dia e a faixa
/// locais saem do fuso do município (<see cref="MunicipalCalendar"/>); os parâmetros, de <see cref="IBusinessParameters"/>.
/// </summary>
public sealed class RiskService(
    IOccurrenceQueries queries,
    IBusinessParameters parameters,
    MunicipalCalendar calendar,
    TimeProvider timeProvider)
{
    public const string RegionNotFoundMessage = "Região não encontrada.";

    public const string InvalidHorizonMessage = "Horizonte inválido. Use Next24Hours ou Next7Days.";

    /// <summary>Grade 7 × 6 de uma região, ou nenhuma célula com a mensagem da RN-29 (US-20).</summary>
    /// <exception cref="NotFoundException">Região inexistente ou inativa.</exception>
    public async Task<RiskGrid> GetGridAsync(Guid regionId, CancellationToken cancellationToken = default)
    {
        var (result, levels) = await CalculateAsync(cancellationToken);
        var region = result.Regions.FirstOrDefault(item => item.RegionId == regionId)
            ?? throw new NotFoundException(RegionNotFoundMessage);

        return new RiskGrid
        {
            RegionId = region.RegionId,
            RegionName = region.RegionName,
            GeneratedAt = timeProvider.GetUtcNow(),
            Levels = Ranges(levels),
            Bands = TimeBands.All
                .Select(band => new TimeBandData { Band = Map<Contracts.Predictive.TimeBand>(band), Label = TimeBands.Label(band) })
                .ToArray(),
            InsufficientData = region.InsufficientData,
            Explanation = PredictionExplanationBuilder.ForRegionPattern(region, result),
            Cells = region.Cells
                .Select(cell => new RiskGridCell
                {
                    DayOfWeek = cell.Day,
                    Band = Map<Contracts.Predictive.TimeBand>(cell.Band),
                    Index = cell.Index,
                    Level = Map<Contracts.Predictive.RiskLevel>(cell.Level),
                    Confidence = Map<Contracts.Predictive.ConfidenceLevel>(cell.Confidence),
                    Explanation = PredictionExplanationBuilder.ForCell(region, cell, result),
                })
                .ToArray(),
        };
    }

    /// <summary>
    /// Cada região colorida pelo maior índice entre as células cujas faixas cruzam o horizonte (D-91): as próximas 24 horas
    /// ou os próximos 7 dias a partir do instante atual, no fuso do município. O número é sempre o de uma célula da grade
    /// (Art. V.3); a Zona Oeste, com poucas semanas, fica sem índice (RN-29).
    /// </summary>
    /// <exception cref="ValidationFailedException">Horizonte desconhecido.</exception>
    public async Task<RiskForecast> GetForecastAsync(ForecastHorizon horizon, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(horizon))
        {
            throw new ValidationFailedException("horizon", InvalidHorizonMessage);
        }

        var (result, levels) = await CalculateAsync(cancellationToken);
        var municipality = await calendar.GetMunicipalityAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var end = now + (horizon == ForecastHorizon.Next24Hours ? TimeSpan.FromHours(24) : TimeSpan.FromDays(7));
        var localStart = TimeZoneInfo.ConvertTime(now, municipality.TimeZone).DateTime;
        var localEnd = TimeZoneInfo.ConvertTime(end, municipality.TimeZone).DateTime;
        var instances = TimeBands.CellsBetween(localStart, localEnd);
        var horizonLabel = PredictionExplanationBuilder.HorizonLabel(horizon);

        var regions = result.Regions.Select(region =>
        {
            if (region.InsufficientData)
            {
                return new RiskForecastRegion
                {
                    RegionId = region.RegionId,
                    RegionName = region.RegionName,
                    InsufficientData = true,
                    Explanation = PredictionExplanationBuilder.Insufficient(
                        region, horizonLabel, now, end, result, horizonLabel.ToLowerInvariant()),
                };
            }

            // Em empate, o pico é o que começa primeiro (as instâncias vêm em ordem de início).
            var peakInstance = instances.MaxBy(instance => region.Cell(instance.Day, instance.Band).Index)!;
            var peak = region.Cell(peakInstance.Day, peakInstance.Band);

            return new RiskForecastRegion
            {
                RegionId = region.RegionId,
                RegionName = region.RegionName,
                Index = peak.Index,
                Level = Map<Contracts.Predictive.RiskLevel>(peak.Level),
                PeakDayOfWeek = peak.Day,
                PeakBand = Map<Contracts.Predictive.TimeBand>(peak.Band),
                PeakStart = new DateTimeOffset(peakInstance.StartLocal, municipality.TimeZone.GetUtcOffset(peakInstance.StartLocal)),
                Explanation = PredictionExplanationBuilder.ForForecastRegion(region, peak, horizon, now, end, result),
            };
        }).ToArray();

        return new RiskForecast
        {
            Horizon = horizon,
            Start = now,
            End = end,
            GeneratedAt = now,
            Levels = Ranges(levels),
            Regions = regions,
        };
    }

    /// <summary>
    /// Lê as ocorrências da janela de treino e roda o modelo (plan 17.5). O fim dos dados é a meia-noite local seguinte à
    /// ocorrência mais recente (plan 17.4); sem ocorrências, é o fim do dia de hoje e toda região tem dados insuficientes.
    /// </summary>
    private async Task<(RiskResult Result, RiskLevels Levels)> CalculateAsync(CancellationToken cancellationToken)
    {
        var municipality = await calendar.GetMunicipalityAsync(cancellationToken);
        var timeZone = municipality.TimeZone;

        var last = await queries.GetLastOccurredAtAsync(cancellationToken);
        var dataEnd = last is { } lastAt
            ? municipality.DateOf(lastAt).AddDays(1)
            : municipality.DateOf(timeProvider.GetUtcNow());

        var windowStartLocal = dataEnd.AddDays(-7 * PredictiveModelSettings.TrainingWeeks).ToDateTime(TimeOnly.MinValue);
        var rows = await queries.ListSamplesSinceAsync(
            new DateTimeOffset(windowStartLocal, timeZone.GetUtcOffset(windowStartLocal)), cancellationToken);
        var histories = await queries.ListRegionHistoriesAsync(cancellationToken);

        var samples = rows.Select(row =>
        {
            var local = TimeZoneInfo.ConvertTime(row.OccurredAt, timeZone).DateTime;
            var (day, band) = TimeBands.CellOf(local);
            return new RiskSample(row.RegionId, row.Kind, DateOnly.FromDateTime(local), day, band);
        }).ToArray();

        var regions = histories
            .Select(history => new RiskRegionInput(
                history.RegionId,
                history.RegionName,
                history.FirstOccurredAt is { } first ? municipality.DateOf(first) : null,
                history.TotalOccurrences))
            .ToArray();

        var minimumWeeks = await parameters.GetAsync(BusinessParameter.PredictiveMinimumHistoryWeeks, cancellationToken);
        var levels = new RiskLevels(
            await parameters.GetAsync(BusinessParameter.RiskModerateFrom, cancellationToken),
            await parameters.GetAsync(BusinessParameter.RiskHighFrom, cancellationToken),
            await parameters.GetAsync(BusinessParameter.RiskVeryHighFrom, cancellationToken));

        return (RiskModel.Calculate(samples, dataEnd, regions, levels, minimumWeeks), levels);
    }

    private static Contracts.Predictive.RiskLevelRange[] Ranges(RiskLevels levels) =>
        levels.Ranges()
            .Select(range => new Contracts.Predictive.RiskLevelRange
            {
                Level = Map<Contracts.Predictive.RiskLevel>(range.Level),
                From = range.From,
                To = range.To,
            })
            .ToArray();

    private static TContract Map<TContract>(Enum value)
        where TContract : struct, Enum =>
        Enum.Parse<TContract>(value.ToString());
}
