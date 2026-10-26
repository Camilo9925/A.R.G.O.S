namespace Argos.Domain.Operations.Alerts;

/// <summary>Como um alerta aberto entra na lista da Central (RN-21).</summary>
public enum AlertListing
{
    /// <summary>Atende ao filtro: listado normalmente.</summary>
    Listed,

    /// <summary>Fora do filtro, mas crítico: listado com "Fora do filtro" (<c>outsideFilter</c>).</summary>
    ListedOutsideFilter,

    /// <summary>Fora do filtro e não crítico: não listado.</summary>
    Hidden,
}

/// <summary>
/// Lista de alertas da Central (RF-035, RN-21; plan 16.3, 16.6): quem entra e em que ordem. "Atende ao filtro" usa só
/// os filtros do usuário, sem o de ativos (plan 16.7), e é calculado pela consulta.
/// </summary>
public static class AlertListPolicy
{
    /// <summary>RN-21: o alerta crítico aparece sempre, mesmo fora do filtro; o não crítico, só dentro dele.</summary>
    public static AlertListing Include(bool matchesFilter, AlertSeverity severity)
    {
        if (matchesFilter)
        {
            return AlertListing.Listed;
        }

        return severity == AlertSeverity.Critical ? AlertListing.ListedOutsideFilter : AlertListing.Hidden;
    }

    /// <summary>
    /// Ordem da lista (RF-035): severidade, da Crítica à Baixa; depois a última ocorrência, da mais recente à mais
    /// antiga; depois o id, para a ordem ser estável.
    /// </summary>
    public static IEnumerable<T> Order<T>(
        IEnumerable<T> alerts,
        Func<T, AlertSeverity> severity,
        Func<T, DateTimeOffset> lastOccurredAt,
        Func<T, Guid> id) =>
        alerts.OrderBy(severity).ThenByDescending(lastOccurredAt).ThenBy(id);
}
