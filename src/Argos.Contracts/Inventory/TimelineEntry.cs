namespace Argos.Contracts.Inventory;

/// <summary>
/// Uma entrada da linha do tempo de um equipamento ou agente (US-08, RF-011, RF-018; Art. V.1; D-49; plan 15.6), do
/// mais recente para o mais antigo. <see cref="Title"/>, <see cref="Description"/> e <see cref="Details"/> chegam
/// prontos em pt-BR, montados pela Aplicação: a Web só exibe, com o ícone de <see cref="Kind"/>.
/// </summary>
public sealed record TimelineEntry
{
    /// <summary>Instante de negócio, em UTC.</summary>
    public DateTimeOffset OccurredAt { get; init; }

    public TimelineEntryKind Kind { get; init; }

    /// <summary>Texto principal, como "Operacional → Mau funcionamento" ou "Manutenção periódica concluída com sucesso".</summary>
    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    /// <summary>Login do usuário, <c>PROMETHEUS</c>, <c>sistema</c> ou <c>simulação</c> (D-43, D-56).</summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>Pares rótulo–valor: justificativa, data prevista, resultado, observações, campos alterados.</summary>
    public IReadOnlyList<TimelineDetailData> Details { get; init; } = [];
}

/// <summary>Um detalhe de uma entrada da linha do tempo, já em texto de exibição.</summary>
public sealed record TimelineDetailData(string Label, string Value);
