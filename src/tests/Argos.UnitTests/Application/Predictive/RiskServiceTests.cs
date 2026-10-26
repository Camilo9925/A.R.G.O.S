using Argos.Application.Common.Abstractions;
using Argos.Application.Common.Exceptions;
using Argos.Application.Inventory;
using Argos.Application.Inventory.Geography;
using Argos.Application.Predictive;
using Argos.Contracts.Predictive;
using Argos.Domain.Common.Parameters;
using Argos.Domain.Inventory.Geography;
using Argos.UnitTests.Application.Fakes;
using Argos.UnitTests.Domain.Inventory;
using DomainOccurrenceKind = Argos.Domain.Operations.Occurrences.OccurrenceKind;

namespace Argos.UnitTests.Application.Predictive;

/// <summary>
/// Risco, explicação e horizontes (US-20, US-25; RF-043, RF-049; RN-28 a RN-30; D-91, D-96; plan 17.5, 17.6, 17.13),
/// com fakes das portas: todos os elementos da RN-28 em célula e região, a mensagem da RN-29 com a Zona Oeste em 3
/// semanas (primeira ocorrência no meio do primeiro dia) e o horizonte contado a partir do instante do
/// <see cref="TimeProvider"/>.
/// </summary>
public sealed class RiskServiceTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly Guid CentroId = Guid.NewGuid();

    private static readonly Guid EastId = Guid.NewGuid();

    private static readonly Guid WestId = Guid.NewGuid();

    // Terça-feira 27/10/2026, 12h no fuso do município (−03:00).
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 15, 0, 0, TimeSpan.Zero);

    // Fim dos dados: segunda-feira 26/10/2026, 00h local.
    private static readonly DateOnly DataEnd = new(2026, 10, 26);

    [Fact]
    public async Task Grid_OfARegionWithHistory_HasFortyTwoCellsEachWithEveryElementOfRN28_CS06()
    {
        var service = NewService();

        var grid = await service.GetGridAsync(EastId, Cancellation);

        Assert.False(grid.InsufficientData);
        Assert.Equal(42, grid.Cells.Count);
        Assert.Equal(42, grid.Cells.Select(cell => (cell.DayOfWeek, cell.Band)).Distinct().Count());
        Assert.Equal(["02h–06h", "06h–10h", "10h–14h", "14h–18h", "18h–22h", "22h–02h"], grid.Bands.Select(band => band.Label));
        Assert.Equal(
            [(RiskLevel.Low, 0, 24), (RiskLevel.Moderate, 25, 49), (RiskLevel.High, 50, 74), (RiskLevel.VeryHigh, 75, 100)],
            grid.Levels.Select(range => (range.Level, range.From, range.To)));
        Assert.All(grid.Cells, cell => AssertCompleteExplanation(cell.Explanation));
        AssertCompleteExplanation(grid.Explanation);
        Assert.Contains(grid.Cells, cell => cell.Index == 100);
    }

    [Fact]
    public async Task Grid_Explanation_UsesTheTextsOfThePlan_RN28()
    {
        var service = NewService();

        var grid = await service.GetGridAsync(EastId, Cancellation);
        var cell = grid.Cells.Single(item => item.DayOfWeek == DayOfWeek.Friday && item.Band == TimeBand.From18To22);

        Assert.Equal("Índice de risco de ocorrências na Zona Leste, sexta-feira, 18h–22h.", cell.Explanation.Subject);
        Assert.Equal("Padrão semanal", cell.Explanation.HorizonLabel);
        Assert.True(cell.Explanation.DataConsidered.IsSimulated);
        Assert.StartsWith("840 ocorrências simuladas da Zona Leste, de 03/08/2026 a 25/10/2026 (12 semanas; 8 na janela de treino)", cell.Explanation.DataConsidered.Description);
        Assert.Contains(cell.Explanation.Factors, factor => factor.Description.StartsWith("18h–22h: ", StringComparison.Ordinal));
        Assert.Contains(cell.Explanation.Factors, factor => factor.Description.StartsWith("Sexta-feira: ", StringComparison.Ordinal));
        Assert.Contains(cell.Explanation.Factors, factor => factor.Description.StartsWith("Zona Leste: ", StringComparison.Ordinal));
        Assert.Contains(cell.Explanation.Factors, factor => factor.Description.StartsWith("Últimas 2 semanas: ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Grid_OfTheWestZoneWithThreeWeeks_HasInsufficientDataAndTheMessageOfRN29()
    {
        var service = NewService();

        var grid = await service.GetGridAsync(WestId, Cancellation);

        Assert.True(grid.InsufficientData);
        Assert.Empty(grid.Cells);
        Assert.True(grid.Explanation.InsufficientData);
        Assert.Equal(
            "Dados insuficientes: a Zona Oeste tem 3 semanas de histórico, e a previsão exige pelo menos 4.",
            grid.Explanation.InsufficientDataMessage);
        Assert.Empty(grid.Explanation.Factors);
        Assert.Null(grid.Explanation.Confidence);
        Assert.NotEmpty(grid.Explanation.Subject);
        Assert.True(grid.Explanation.DataConsidered.IsSimulated);
    }

    [Fact]
    public void Message_OfARegionWithMasculineName_UsesTheMasculineArticleAndTheSingular_RN29()
    {
        Assert.Equal(
            "Dados insuficientes: o Centro tem 1 semana de histórico, e a previsão exige pelo menos 4.",
            PredictionExplanationBuilder.InsufficientMessage("Centro", 1, 4));
    }

    [Fact]
    public async Task Grid_OfUnknownRegion_IsNotFound()
    {
        var service = NewService();

        var error = await Assert.ThrowsAsync<NotFoundException>(() => service.GetGridAsync(Guid.NewGuid(), Cancellation));

        Assert.Equal("Região não encontrada.", error.Message);
    }

    [Theory]
    [InlineData(ForecastHorizon.Next24Hours)]
    [InlineData(ForecastHorizon.Next7Days)]
    public async Task Forecast_TheIndexOfEachRegionIsTheOneOfACellOfItsGrid_ArtV3(ForecastHorizon horizon)
    {
        var service = NewService();

        var forecast = await service.GetForecastAsync(horizon, Cancellation);

        Assert.Equal(horizon, forecast.Horizon);
        Assert.Equal(3, forecast.Regions.Count);
        foreach (var region in forecast.Regions.Where(region => !region.InsufficientData))
        {
            var grid = await service.GetGridAsync(region.RegionId, Cancellation);
            var cell = grid.Cells.Single(item => item.DayOfWeek == region.PeakDayOfWeek && item.Band == region.PeakBand);
            Assert.Equal(cell.Index, region.Index);
            Assert.Equal(cell.Level, region.Level);
            AssertCompleteExplanation(region.Explanation);
        }

        var west = forecast.Regions.Single(region => region.RegionId == WestId);
        Assert.True(west.InsufficientData);
        Assert.Null(west.Index);
        Assert.Null(west.Level);
        Assert.StartsWith("Dados insuficientes: a Zona Oeste tem 3 semanas", west.Explanation.InsufficientDataMessage);
    }

    [Fact]
    public async Task Forecast_Next24Hours_StartsAtTheInstantOfTheClockAndPicksOnlyTheCellsOfTheHorizon_D91()
    {
        var clock = new FakeTimeProvider(Now);
        var service = NewService(clock);

        var forecast = await service.GetForecastAsync(ForecastHorizon.Next24Hours, Cancellation);

        Assert.Equal(Now, forecast.Start);
        Assert.Equal(Now.AddHours(24), forecast.End);
        Assert.All(forecast.Regions.Where(region => !region.InsufficientData), region =>
        {
            Assert.InRange(region.PeakStart!.Value, Now.AddHours(-4), Now.AddHours(24));
            Assert.Equal(Now, region.Explanation.HorizonStart);
            Assert.Equal("Próximas 24 horas", region.Explanation.HorizonLabel);
        });

        clock.Now = Now.AddDays(2);
        var later = await service.GetForecastAsync(ForecastHorizon.Next24Hours, Cancellation);

        Assert.Equal(Now.AddDays(2), later.Start);
    }

    [Fact]
    public async Task Forecast_Next7Days_IsAtLeastTheNext24HoursAndHasTheMaximumOfTheGrid_D91()
    {
        var service = NewService();

        var day = await service.GetForecastAsync(ForecastHorizon.Next24Hours, Cancellation);
        var week = await service.GetForecastAsync(ForecastHorizon.Next7Days, Cancellation);

        foreach (var region in week.Regions.Where(region => !region.InsufficientData))
        {
            var grid = await service.GetGridAsync(region.RegionId, Cancellation);
            Assert.Equal(grid.Cells.Max(cell => cell.Index), region.Index);
            Assert.True(region.Index >= day.Regions.Single(item => item.RegionId == region.RegionId).Index);
            Assert.Equal("Próximos 7 dias", region.Explanation.HorizonLabel);
        }
    }

    [Fact]
    public async Task Forecast_UnknownHorizon_IsRefusedWithTheFieldHorizon()
    {
        var service = NewService();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(() => service.GetForecastAsync((ForecastHorizon)99, Cancellation));

        Assert.Equal("Horizonte inválido. Use Next24Hours ou Next7Days.", error.Errors["horizon"].Single());
    }

    [Fact]
    public async Task Grid_WithoutOccurrences_HasInsufficientDataForEveryRegion_RN29()
    {
        var service = NewService(occurrences: new FakeOccurrences([], [new RegionHistoryRow(CentroId, "Centro", null, 0)]));

        var grid = await service.GetGridAsync(CentroId, Cancellation);

        Assert.True(grid.InsufficientData);
        Assert.Equal(
            "Dados insuficientes: o Centro tem 0 semanas de histórico, e a previsão exige pelo menos 4.",
            grid.Explanation.InsufficientDataMessage);
    }

    [Fact]
    public async Task Grid_UsesTheMinimumFromTheParameter_RN29()
    {
        var parameters = new FixedParameters { [BusinessParameter.PredictiveMinimumHistoryWeeks] = 3 };
        var service = NewService(parameters: parameters);

        var west = await service.GetGridAsync(WestId, Cancellation);

        Assert.False(west.InsufficientData);
        Assert.Equal(42, west.Cells.Count);
    }

    private static void AssertCompleteExplanation(PredictionExplanation explanation)
    {
        Assert.False(string.IsNullOrWhiteSpace(explanation.Subject));
        Assert.False(string.IsNullOrWhiteSpace(explanation.HorizonLabel));
        Assert.False(string.IsNullOrWhiteSpace(explanation.DataConsidered.Description));
        Assert.True(explanation.DataConsidered.IsSimulated);
        Assert.False(string.IsNullOrWhiteSpace(explanation.Recommendation));

        if (explanation.InsufficientData)
        {
            Assert.False(string.IsNullOrWhiteSpace(explanation.InsufficientDataMessage));
            return;
        }

        Assert.NotEmpty(explanation.Factors);
        Assert.All(explanation.Factors, factor => Assert.False(string.IsNullOrWhiteSpace(factor.Description)));
        Assert.NotNull(explanation.Confidence);
        Assert.True(explanation.DataConsidered.OccurrenceCount > 0);
    }

    private static RiskService NewService(
        FakeTimeProvider? clock = null, FakeOccurrences? occurrences = null, FixedParameters? parameters = null)
    {
        clock ??= new FakeTimeProvider(Now);
        var calendar = new MunicipalCalendar(new FixedMunicipality(), clock);
        return new RiskService(occurrences ?? Scenario(), parameters ?? new FixedParameters(), calendar, clock);
    }

    /// <summary>
    /// Centro e Zona Leste com 12 semanas (a Leste com o dobro de ocorrências e mais intensa nas duas últimas semanas) e a
    /// Zona Oeste com 3 semanas, a primeira ocorrência ao meio-dia do primeiro dia (RN-29).
    /// </summary>
    private static FakeOccurrences Scenario()
    {
        var rows = new List<OccurrenceSampleRow>();
        Fill(rows, CentroId, 84, perDayFactor: 1);
        Fill(rows, EastId, 84, perDayFactor: 2);
        Fill(rows, WestId, 21, perDayFactor: 1, firstDayFromHour: 12);

        var histories = new[] { (CentroId, "Centro"), (EastId, "Zona Leste"), (WestId, "Zona Oeste") }
            .Select(region =>
            {
                var own = rows.Where(row => row.RegionId == region.Item1).ToArray();
                return new RegionHistoryRow(region.Item1, region.Item2, own.Min(row => row.OccurredAt), own.Length);
            })
            .ToArray();

        return new FakeOccurrences(rows, histories);
    }

    /// <summary>Quatro horários por dia (06h30, 12h30, 19h30 e 23h30 locais), com tipos e quantidades diferentes por dia.</summary>
    private static void Fill(List<OccurrenceSampleRow> rows, Guid region, int days, int perDayFactor, int firstDayFromHour = 0)
    {
        var slots = new[]
        {
            (Hour: 6, Kind: DomainOccurrenceKind.TrafficAccident),
            (Hour: 12, Kind: DomainOccurrenceKind.Congestion),
            (Hour: 19, Kind: DomainOccurrenceKind.TheftOrRobbery),
            (Hour: 23, Kind: DomainOccurrenceKind.Vandalism),
        };

        for (var offset = days; offset >= 1; offset--)
        {
            var day = DataEnd.AddDays(-offset);

            foreach (var slot in slots)
            {
                if (offset == days && slot.Hour < firstDayFromHour)
                {
                    continue;
                }

                // Sexta-feira à noite pesa mais, para a célula máxima existir; as duas últimas semanas, mais ainda.
                var count = perDayFactor * (day.DayOfWeek == DayOfWeek.Friday && slot.Hour == 19 ? 3 : 1)
                    * (region == EastId && offset <= 14 ? 2 : 1);

                for (var item = 0; item < count; item++)
                {
                    var local = day.ToDateTime(new TimeOnly(slot.Hour, 30));
                    rows.Add(new OccurrenceSampleRow(region, slot.Kind, new DateTimeOffset(local, TimeSpan.FromHours(-3))));
                }
            }
        }
    }

    private sealed class FakeOccurrences(
        IReadOnlyList<OccurrenceSampleRow> rows, IReadOnlyList<RegionHistoryRow> histories) : IOccurrenceQueries
    {
        public Task<DateTimeOffset?> GetLastOccurredAtAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(rows.Count == 0 ? (DateTimeOffset?)null : rows.Max(row => row.OccurredAt));

        public Task<IReadOnlyList<RegionHistoryRow>> ListRegionHistoriesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(histories);

        public Task<IReadOnlyList<OccurrenceSampleRow>> ListSamplesSinceAsync(
            DateTimeOffset since, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<OccurrenceSampleRow>>(rows.Where(row => row.OccurredAt >= since).ToArray());
    }

    private sealed class FixedMunicipality : IMunicipalityRepository
    {
        private readonly Municipality _municipality = InventoryTestData.NewMunicipality();

        public Task<Municipality?> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<Municipality?>(_municipality);

        public void Add(Municipality municipality) => throw new NotSupportedException();
    }

    private sealed class FixedParameters : IBusinessParameters
    {
        private readonly Dictionary<BusinessParameter, int> _values = [];

        public int this[BusinessParameter parameter]
        {
            set => _values[parameter] = value;
        }

        public Task<int> GetAsync(BusinessParameter parameter, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.TryGetValue(parameter, out var value) ? value : BusinessParameterCatalog.Get(parameter).Default);

        public Task<IReadOnlyCollection<BusinessParameter>> ListStoredAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<BusinessParameter>>([]);

        public void Add(BusinessParameter parameter, int value) => throw new NotSupportedException();
    }
}
