using Argos.Application.Inventory.Geography;
using Argos.Domain.Inventory.Geography;

namespace Argos.Application.Inventory;

/// <summary>
/// O município da demonstração e o "hoje" no seu fuso (D-50, D-54; plan 15.12): a data que conta para as manutenções
/// (RN-08, RN-10). O município é lido uma vez por caso de uso.
/// </summary>
public sealed class MunicipalCalendar(IMunicipalityRepository municipalities, TimeProvider timeProvider)
{
    private Municipality? _municipality;

    public async Task<Municipality> GetMunicipalityAsync(CancellationToken cancellationToken = default) =>
        _municipality ??= await municipalities.GetAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "O município ainda não foi carregado: rode o Migrator com a carga inventory-reference-v1.");

    /// <summary>Data de hoje no fuso do município.</summary>
    public async Task<DateOnly> TodayAsync(CancellationToken cancellationToken = default) =>
        (await GetMunicipalityAsync(cancellationToken)).DateOf(timeProvider.GetUtcNow());
}
