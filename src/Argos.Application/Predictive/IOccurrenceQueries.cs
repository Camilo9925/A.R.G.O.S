using Argos.Domain.Operations.Occurrences;

namespace Argos.Application.Predictive;

/// <summary>Uma ocorrência projetada para o cálculo: a região atual do bairro (D-50), o tipo e o instante.</summary>
public sealed record OccurrenceSampleRow(Guid RegionId, OccurrenceKind Kind, DateTimeOffset OccurredAt);

/// <summary>Uma região ativa com o resumo do histórico dela: a primeira ocorrência e o total (plan 17.9).</summary>
public sealed record RegionHistoryRow(Guid RegionId, string RegionName, DateTimeOffset? FirstOccurredAt, int TotalOccurrences);

/// <summary>
/// Consultas da Cidade Preditiva (plan 17.9, 17.10): projeção direta das linhas, sem regra de negócio. A região é a do
/// mapeamento atual do bairro (D-50). O cálculo inteiro é em memória, a cada consulta (D-89).
/// </summary>
public interface IOccurrenceQueries
{
    /// <summary>Instante da ocorrência mais recente, ou nulo sem ocorrências.</summary>
    Task<DateTimeOffset?> GetLastOccurredAtAsync(CancellationToken cancellationToken = default);

    /// <summary>As regiões ativas, em ordem de nome, com a primeira ocorrência e o total de cada uma.</summary>
    Task<IReadOnlyList<RegionHistoryRow>> ListRegionHistoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>As ocorrências a partir do instante informado (a janela de treino).</summary>
    Task<IReadOnlyList<OccurrenceSampleRow>> ListSamplesSinceAsync(
        DateTimeOffset since, CancellationToken cancellationToken = default);
}

/// <summary>Porta de inclusão das ocorrências (só a carga). Sem remoção (Art. V.1; RN-23).</summary>
public interface IOccurrenceRepository
{
    void AddRange(IEnumerable<Occurrence> occurrences);
}
