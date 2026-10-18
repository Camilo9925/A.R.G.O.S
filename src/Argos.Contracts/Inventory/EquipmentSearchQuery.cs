using System.ComponentModel.DataAnnotations;
using Argos.Contracts.Common;

namespace Argos.Contracts.Inventory;

/// <summary>
/// Pesquisa de equipamentos (US-03, RF-010; D-53; plan 15.8). O termo procura, sem acento e sem diferença de
/// maiúsculas, em número de série, marca, modelo, rua, referência e bairro. Cada filtro aceita vários valores, que
/// combinam por "ou"; filtros diferentes combinam por "e". Na query string, um filtro com vários valores se repete
/// (<c>?status=Malfunction&amp;status=Stolen</c>), por isso os nomes ficam no singular. O filtro de ativos e inativos
/// entra na Fase 3 (T082); até lá a pesquisa devolve os ativos.
/// </summary>
public sealed record EquipmentSearchQuery : PageQuery
{
    [StringLength(100, ErrorMessage = "A pesquisa deve ter no máximo {1} caracteres.")]
    public string? Search { get; set; }

    public List<Guid> TypeId { get; set; } = [];

    public List<EquipmentStatus> Status { get; set; } = [];

    public List<Guid> OrganizationId { get; set; } = [];

    public List<Guid> RegionId { get; set; } = [];

    public List<Guid> NeighborhoodId { get; set; } = [];

    public List<MaintenanceNeed> MaintenanceNeed { get; set; } = [];

    public List<EquipmentCategory> Category { get; set; } = [];
}

/// <summary>Valores aceitos em <see cref="PageQuery.Sort"/> na pesquisa de equipamentos (plan 15.8).</summary>
public static class EquipmentSortFields
{
    public const string SerialNumber = "serialNumber";
    public const string Type = "type";
    public const string Status = "status";
    public const string Neighborhood = "neighborhood";
    public const string UpdatedAt = "updatedAt";

    /// <summary>Ordenação usada quando a pesquisa não informa uma.</summary>
    public const string Default = SerialNumber;

    public static IReadOnlyList<string> All { get; } = [SerialNumber, Type, Status, Neighborhood, UpdatedAt];
}
