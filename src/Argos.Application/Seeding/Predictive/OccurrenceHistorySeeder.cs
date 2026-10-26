using Argos.Application.Common.Abstractions;
using Argos.Application.Inventory.Geography;
using Argos.Application.Predictive;
using Argos.Application.Seeding.Inventory;
using Argos.Domain.Common;
using Argos.Domain.Operations.Occurrences;

namespace Argos.Application.Seeding.Predictive;

/// <summary>Versões da carga da Cidade Preditiva (plan 17.3, 17.10; D-86).</summary>
public static class PredictiveSeed
{
    /// <summary>Fase 5: o histórico de 12 semanas de ocorrências simuladas.</summary>
    public const string OccurrencesVersion = "predictive-occurrences-v1";

    /// <summary>Ocorrências por inclusão em lote (plan 17.3).</summary>
    public const int BatchSize = 1000;
}

/// <summary>
/// Versão <c>predictive-occurrences-v1</c>: as ocorrências simuladas, pelo nome do bairro, geradas pelo Migrator com o
/// <c>OccurrenceHistoryGenerator</c> da Simulação, que a Aplicação não referencia.
/// </summary>
public sealed record OccurrenceHistorySeed(IReadOnlyList<OccurrenceSeed> Occurrences);

public sealed record OccurrenceSeed(
    OccurrenceKind Kind, DateTimeOffset OccurredAt, string NeighborhoodName, string Street, double Latitude, double Longitude);

/// <summary>
/// Carga <c>predictive-occurrences-v1</c> (Fase 5; RF-025, parcial; Art. IX.2, IX.3; D-56, D-86; plan 17.3, 17.10):
/// grava as ocorrências simuladas pelas operações do Domínio (ponto dentro do município, RN-02), com autor
/// <c>simulação</c>, <c>AddRange</c> em lotes de 1.000 e, com o registro da versão, num só <c>SaveChanges</c>. Exige
/// <c>inventory-reference-v1</c> (bairros e município); rodar de novo com a mesma semente não grava nada.
/// </summary>
public sealed class OccurrenceHistorySeeder(
    IMunicipalityRepository municipalities,
    INeighborhoodRepository neighborhoods,
    IOccurrenceRepository occurrences,
    ISeedLedger ledger,
    IUnitOfWork unitOfWork)
{
    /// <param name="seed">Semente da simulação, registrada com a versão (plan 7.4).</param>
    /// <exception cref="InvalidOperationException">Referência não carregada ou versão aplicada com outra semente.</exception>
    public async Task<SeedResult> SeedAsync(
        OccurrenceHistorySeed history, int seed, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(history);

        if (!await InventorySeed.ShouldApplyAsync(ledger, PredictiveSeed.OccurrencesVersion, seed, cancellationToken))
        {
            return SeedResult.Skipped(PredictiveSeed.OccurrencesVersion);
        }

        if (await ledger.FindAppliedSeedAsync(InventorySeed.ReferenceVersion, cancellationToken) is null)
        {
            throw new InvalidOperationException(
                $"Aplique a carga {InventorySeed.ReferenceVersion} antes de {PredictiveSeed.OccurrencesVersion}.");
        }

        var municipality = await municipalities.GetAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                $"O município ainda não foi carregado: aplique a carga {InventorySeed.ReferenceVersion}.");
        var neighborhoodIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var batch = new List<Occurrence>(PredictiveSeed.BatchSize);
        var total = 0;

        foreach (var item in history.Occurrences)
        {
            if (!neighborhoodIds.TryGetValue(item.NeighborhoodName, out var neighborhoodId))
            {
                var neighborhood = await neighborhoods.FindByNameKeyAsync(
                        TextNormalization.Normalize(item.NeighborhoodName), cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Bairro \"{item.NeighborhoodName}\" não existe; aplique antes a carga {InventorySeed.ReferenceVersion}.");
                neighborhoodId = neighborhood.Id;
                neighborhoodIds[item.NeighborhoodName] = neighborhoodId;
            }

            batch.Add(Occurrence.Record(
                item.Kind,
                item.OccurredAt,
                neighborhoodId,
                item.Latitude,
                item.Longitude,
                item.Street,
                $"{OccurrenceKindCatalog.Label(item.Kind)} em {item.Street}, {item.NeighborhoodName} (ocorrência simulada).",
                isSimulated: true,
                InventorySeed.Author,
                municipality));
            total++;

            if (batch.Count == PredictiveSeed.BatchSize)
            {
                occurrences.AddRange(batch);
                batch = new List<Occurrence>(PredictiveSeed.BatchSize);
            }
        }

        occurrences.AddRange(batch);
        ledger.Record(PredictiveSeed.OccurrencesVersion, seed);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SeedResult(
            PredictiveSeed.OccurrencesVersion, false, new Dictionary<string, int> { ["occurrences"] = total });
    }
}
