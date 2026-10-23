namespace Argos.Contracts.Common;

/// <summary>
/// Filtro de ativos e inativos das pesquisas do Inventário (RF-010, RF-017, US-09; plan 15.8), como aparece na query
/// string (<c>active</c>, <c>inactive</c> ou <c>all</c>). O padrão é <see cref="Active"/>: o inativo sai da visão
/// ativa e continua consultável com o filtro "inativos" (RN-35). É só do contrato, sem espelho no Domínio, como
/// <see cref="SortDirection"/>.
/// </summary>
public enum ActivityFilter
{
    /// <summary>Só os cadastros ativos (padrão).</summary>
    Active,

    /// <summary>Só os cadastros inativos.</summary>
    Inactive,

    /// <summary>Ativos e inativos.</summary>
    All,
}
