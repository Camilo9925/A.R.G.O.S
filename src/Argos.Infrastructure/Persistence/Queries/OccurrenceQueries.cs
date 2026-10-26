using Argos.Application.Predictive;
using Argos.Domain.Operations.Occurrences;
using Microsoft.EntityFrameworkCore;

namespace Argos.Infrastructure.Persistence.Queries;

/// <summary>
/// Consultas da Cidade Preditiva (plan 17.9, 17.10), sem rastreamento e com projeção direta: só (região atual do bairro,
/// tipo, instante), com a região do mapeamento atual do bairro (D-50). Nenhuma regra de negócio: o cálculo é do Domínio.
/// </summary>
internal sealed class OccurrenceQueries(ArgosDbContext dbContext) : IOccurrenceQueries
{
    public Task<DateTimeOffset?> GetLastOccurredAtAsync(CancellationToken cancellationToken = default) =>
        dbContext.Occurrences
            .AsNoTracking()
            .MaxAsync(occurrence => (DateTimeOffset?)occurrence.OccurredAt, cancellationToken);

    public async Task<IReadOnlyList<RegionHistoryRow>> ListRegionHistoriesAsync(CancellationToken cancellationToken = default)
    {
        var regions = await dbContext.Regions
            .AsNoTracking()
            .Where(region => region.IsActive)
            .Select(region => new { region.Id, region.Name })
            .ToListAsync(cancellationToken);

        var summaries = await (
                from occurrence in dbContext.Occurrences.AsNoTracking()
                join neighborhood in dbContext.Neighborhoods.AsNoTracking() on occurrence.NeighborhoodId equals neighborhood.Id
                group occurrence by neighborhood.RegionId into grouped
                select new
                {
                    RegionId = grouped.Key,
                    First = grouped.Min(occurrence => occurrence.OccurredAt),
                    Total = grouped.Count(),
                })
            .ToDictionaryAsync(summary => summary.RegionId, cancellationToken);

        return regions
            .OrderBy(region => region.Name, StringComparer.Ordinal)
            .Select(region => summaries.TryGetValue(region.Id, out var summary)
                ? new RegionHistoryRow(region.Id, region.Name, summary.First, summary.Total)
                : new RegionHistoryRow(region.Id, region.Name, null, 0))
            .ToArray();
    }

    public Task<IReadOnlyList<OccurrenceSampleRow>> ListSamplesSinceAsync(
        DateTimeOffset since, CancellationToken cancellationToken = default) => ListSamplesAsync(since.ToUniversalTime(), cancellationToken);

    private async Task<IReadOnlyList<OccurrenceSampleRow>> ListSamplesAsync(DateTimeOffset since, CancellationToken cancellationToken) =>
        await (
                from occurrence in dbContext.Occurrences.AsNoTracking()
                join neighborhood in dbContext.Neighborhoods.AsNoTracking() on occurrence.NeighborhoodId equals neighborhood.Id
                where occurrence.OccurredAt >= since
                orderby occurrence.OccurredAt
                select new OccurrenceSampleRow(neighborhood.RegionId, occurrence.Kind, occurrence.OccurredAt))
            .ToListAsync(cancellationToken);
}

/// <summary>Inclusão das ocorrências (plan 17.10): só a carga; sem remoção (Art. V.1).</summary>
internal sealed class OccurrenceRepository(ArgosDbContext dbContext) : IOccurrenceRepository
{
    public void AddRange(IEnumerable<Occurrence> occurrences) => dbContext.Occurrences.AddRange(occurrences);
}
