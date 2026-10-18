namespace Argos.Contracts.Common;

/// <summary>
/// Uma página de resultados (D-53; plan 15.8). Sem resultado, <see cref="Items"/> vem vazio e a Web mostra a
/// mensagem com "Limpar filtros" (US-03).
/// </summary>
public sealed record PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    public int Page { get; init; }

    public int PageSize { get; init; }

    /// <summary>Total de registros que atendem aos filtros, em todas as páginas.</summary>
    public int TotalCount { get; init; }
}
