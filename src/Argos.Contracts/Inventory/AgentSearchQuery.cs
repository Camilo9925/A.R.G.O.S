using System.ComponentModel.DataAnnotations;
using Argos.Contracts.Common;

namespace Argos.Contracts.Inventory;

/// <summary>
/// Pesquisa de agentes (US-03, US-06, RF-017; D-53; plan 15.8). O termo procura, sem acento e sem diferença de
/// maiúsculas, em nome, nome de exibição e matrícula. Cada filtro aceita vários valores, que combinam por "ou";
/// filtros diferentes combinam por "e". A região é a do bairro da base (D-65). <see cref="Activity"/> separa ativos e
/// inativos (padrão: ativos; US-09).
/// </summary>
public sealed record AgentSearchQuery : PageQuery
{
    [StringLength(100, ErrorMessage = "A pesquisa deve ter no máximo {1} caracteres.")]
    public string? Search { get; set; }

    public List<AgentRole> Role { get; set; } = [];

    public List<Guid> OrganizationId { get; set; } = [];

    public List<Guid> BaseId { get; set; } = [];

    public List<Guid> RegionId { get; set; } = [];

    public List<AgentStatus> Status { get; set; } = [];

    /// <summary>Ativos (padrão), inativos ou todos (RF-013, US-09).</summary>
    public ActivityFilter Activity { get; set; } = ActivityFilter.Active;
}

/// <summary>Valores aceitos em <see cref="PageQuery.Sort"/> na pesquisa de agentes (plan 15.8).</summary>
public static class AgentSortFields
{
    public const string DisplayName = "displayName";
    public const string RegistrationNumber = "registrationNumber";
    public const string Status = "status";

    /// <summary>Ordenação usada quando a pesquisa não informa uma.</summary>
    public const string Default = DisplayName;

    public static IReadOnlyList<string> All { get; } = [DisplayName, RegistrationNumber, Status];
}
