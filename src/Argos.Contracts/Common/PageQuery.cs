using System.ComponentModel.DataAnnotations;

namespace Argos.Contracts.Common;

/// <summary>
/// Página pedida numa pesquisa (D-53; plan 15.8). Os valores de <see cref="Sort"/> aceitos são uma lista fechada de
/// cada pesquisa, conferida por ela; a ordenação é sempre estável (desempate por id). As propriedades têm <c>set</c>
/// porque vêm da query string e dos filtros da Web.
/// </summary>
public record PageQuery
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    [Range(1, int.MaxValue, ErrorMessage = "A página deve ser 1 ou maior.")]
    public int Page { get; set; } = 1;

    [Range(1, MaxPageSize, ErrorMessage = "O tamanho da página deve estar entre {1} e {2}.")]
    public int PageSize { get; set; } = DefaultPageSize;

    [StringLength(40, ErrorMessage = "A ordenação deve ter no máximo {1} caracteres.")]
    public string? Sort { get; set; }

    public SortDirection Direction { get; set; } = SortDirection.Asc;
}

/// <summary>Sentido da ordenação, como aparece na query string (<c>asc</c> ou <c>desc</c>).</summary>
public enum SortDirection
{
    Asc,
    Desc,
}
