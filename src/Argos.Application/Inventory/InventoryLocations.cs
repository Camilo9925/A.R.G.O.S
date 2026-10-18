using Argos.Application.Common.Exceptions;
using Argos.Application.Inventory.Geography;
using Argos.Contracts.Inventory;
using Argos.Domain.Common;
using Argos.Domain.Inventory.Geography;

namespace Argos.Application.Inventory;

/// <summary>
/// Monta a <see cref="Location"/> do Domínio a partir da entrada do contrato (RF-003, RN-02, RN-03; plan 15.2.4),
/// com o município e o bairro carregados, e põe os erros sob o nome do campo no contrato, como
/// <c>location.zipCode</c> (D-46).
/// </summary>
public sealed class InventoryLocations(MunicipalCalendar calendar, INeighborhoodRepository neighborhoods)
{
    public const string Field = "location";

    /// <exception cref="NotFoundException">O bairro informado não existe.</exception>
    /// <exception cref="DomainValidationException">Campos inválidos, com o prefixo <c>location.</c>.</exception>
    public async Task<Location> BuildAsync(LocationInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.NeighborhoodId is not { } neighborhoodId)
        {
            throw new DomainValidationException($"{Field}.neighborhoodId", "Escolha o bairro.");
        }

        var municipality = await calendar.GetMunicipalityAsync(cancellationToken);
        var neighborhood = await neighborhoods.FindByIdAsync(neighborhoodId, cancellationToken)
            ?? throw new NotFoundException("Bairro não encontrado.");

        return DomainFields.Under(Field, () => Location.Create(
            municipality,
            neighborhood,
            input.ZipCode,
            input.Street,
            input.Reference,
            input.Latitude ?? double.NaN,
            input.Longitude ?? double.NaN));
    }
}
