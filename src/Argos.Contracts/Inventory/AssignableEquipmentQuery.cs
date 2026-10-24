using System.ComponentModel.DataAnnotations;
using Argos.Contracts.Common;

namespace Argos.Contracts.Inventory;

/// <summary>
/// Pesquisa dos equipamentos que podem receber o vínculo de um agente, para o diálogo "Vincular a equipamento" (US-07,
/// RF-016; D-62, D-68; plan 15.2.10, 15.14). Vinculável é o equipamento ativo, móvel, de tipo que aceita tripulação e
/// fora das situações que recusam vínculo novo: a regra vem do Domínio, e a Web só exibe. O termo procura como na
/// pesquisa de equipamentos (número de série, marca, modelo e bairro, sem acento); a ordenação usa
/// <see cref="EquipmentSortFields"/>. Não há filtro de ativos, porque só ativos são vinculáveis.
/// </summary>
public sealed record AssignableEquipmentQuery : PageQuery
{
    [StringLength(100, ErrorMessage = "A pesquisa deve ter no máximo {1} caracteres.")]
    public string? Search { get; set; }
}
