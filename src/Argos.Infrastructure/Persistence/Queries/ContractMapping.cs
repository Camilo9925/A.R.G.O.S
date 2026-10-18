using Argos.Domain.Inventory.Geography;
using Argos.Domain.Inventory.Types;
using C = Argos.Contracts.Inventory;

namespace Argos.Infrastructure.Persistence.Queries;

/// <summary>
/// Mapeamento à mão dos dados lidos para os contratos (plan 2.3), feito depois da leitura: as consultas trazem os
/// enums do Domínio, convertidos pelo nome para os espelhos de Contracts (D-61).
/// </summary>
internal static class ContractMapping
{
    public static TTarget Convert<TTarget>(Enum value)
        where TTarget : struct, Enum =>
        Enum.Parse<TTarget>(value.ToString());

    public static TTarget? ConvertNullable<TTarget>(Enum? value)
        where TTarget : struct, Enum =>
        value is null ? null : Convert<TTarget>(value);

    public static C.GeoPointData Point(GeoPoint point) => new(point.Latitude, point.Longitude);

    public static IReadOnlyList<IReadOnlyList<C.GeoPointData>> Rings(GeoPolygon polygon) =>
        polygon.Rings.Select(ring => (IReadOnlyList<C.GeoPointData>)ring.Select(Point).ToArray()).ToArray();

    public static C.LocationData Location(Location location, Neighborhood neighborhood, Region region) => new()
    {
        ZipCode = location.ZipCode.Formatted,
        Street = location.Street,
        NeighborhoodId = neighborhood.Id,
        NeighborhoodName = neighborhood.Name,
        RegionId = region.Id,
        RegionName = region.Name,
        City = location.City,
        State = location.State,
        Reference = location.Reference,
        Latitude = location.Point.Latitude,
        Longitude = location.Point.Longitude,
    };

    public static C.EquipmentTypeData Type(EquipmentType type) => new()
    {
        Id = type.Id,
        Key = type.Key,
        Name = type.Name,
        Category = Convert<C.EquipmentCategory>(type.Category),
        Icon = type.Icon,
        IsBuiltIn = type.IsBuiltIn,
        CrewAssignable = type.CrewAssignable,
        IsActive = type.IsActive,
        Attributes = type.Attributes
            .OrderBy(attribute => attribute.Order)
            .Select(attribute => new C.EquipmentTypeAttributeData
            {
                Key = attribute.Key,
                Label = attribute.Label,
                DataType = Convert<C.AttributeDataType>(attribute.DataType),
                IsRequired = attribute.IsRequired,
                RequiredWhen = attribute.RequiredWhen is { } condition
                    ? new C.AttributeConditionData(condition.AttributeKey, condition.OptionKey)
                    : null,
                Unit = attribute.Unit,
                Min = attribute.Min,
                Max = attribute.Max,
                MaxLength = attribute.MaxLength,
                Options = attribute.Options.Select(option => new C.AttributeOptionData(option.Key, option.Label)).ToArray(),
                Order = attribute.Order,
            })
            .ToArray(),
    };
}
