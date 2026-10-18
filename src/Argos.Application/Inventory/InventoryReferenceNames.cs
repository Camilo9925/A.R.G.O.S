using Argos.Application.Inventory.Geography;
using Argos.Application.Inventory.Support;
using Argos.Domain.Inventory;

namespace Argos.Application.Inventory;

/// <summary>
/// Troca, nas alterações devolvidas pelo Domínio, os ids dos campos de referência a outro cadastro pelos nomes, antes
/// de gravar o registro cadastral (D-49; plan 15.2.11): o Domínio só conhece o cadastro novo, e o histórico precisa
/// ser legível sem consultar o cadastro atual. Vale também para campos aninhados, como <c>location.neighborhoodId</c>.
/// </summary>
public sealed class InventoryReferenceNames(
    IOrganizationRepository organizations,
    IRegionRepository regions,
    INeighborhoodRepository neighborhoods,
    IOperationalBaseRepository bases)
{
    public async Task<IReadOnlyList<FieldChange>> ResolveAsync(
        IReadOnlyList<FieldChange> changes, CancellationToken cancellationToken = default)
    {
        var resolved = new List<FieldChange>(changes.Count);

        foreach (var change in changes)
        {
            var lookup = LookupFor(change.Field);
            if (lookup is null)
            {
                resolved.Add(change);
                continue;
            }

            resolved.Add(change with
            {
                Before = await NameOfAsync(change.Before, lookup, cancellationToken),
                After = await NameOfAsync(change.After, lookup, cancellationToken),
            });
        }

        return resolved;
    }

    private Func<Guid, CancellationToken, Task<string?>>? LookupFor(string field) =>
        field[(field.LastIndexOf('.') + 1)..] switch
        {
            "organizationId" => async (id, token) => (await organizations.FindByIdAsync(id, token))?.Name,
            "regionId" => async (id, token) => (await regions.FindByIdAsync(id, token))?.Name,
            "neighborhoodId" => async (id, token) => (await neighborhoods.FindByIdAsync(id, token))?.Name,
            "baseId" => async (id, token) => (await bases.FindByIdAsync(id, token))?.Name,
            _ => null,
        };

    /// <summary>Nome do cadastro, ou o próprio texto quando não é um id conhecido (não deveria acontecer).</summary>
    private static async Task<string?> NameOfAsync(
        string? value, Func<Guid, CancellationToken, Task<string?>> lookup, CancellationToken cancellationToken) =>
        Guid.TryParse(value, out var id) ? await lookup(id, cancellationToken) ?? value : value;
}
