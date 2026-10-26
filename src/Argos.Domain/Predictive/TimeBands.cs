namespace Argos.Domain.Predictive;

/// <summary>Faixa horária de 4 h no fuso do município (D-88; plan 17.1), alinhada aos turnos da escala.</summary>
public enum TimeBand
{
    From02To06,
    From06To10,
    From10To14,
    From14To18,
    From18To22,

    /// <summary>22h às 02h do dia seguinte; pertence ao dia em que começa.</summary>
    From22To02,
}

/// <summary>Uma ocorrência futura de uma célula: o dia da semana e a faixa, com o início no horário local.</summary>
public sealed record CellInstance(DayOfWeek Day, TimeBand Band, DateTime StartLocal);

/// <summary>
/// As seis faixas horárias e a conversão do horário local para (dia da semana, faixa) (D-88; plan 17.1, 17.4). A faixa
/// 22h–02h pertence ao dia em que começa: 00h30 de terça é a faixa 22h–02h de segunda.
/// </summary>
public static class TimeBands
{
    public const int LengthHours = 4;

    public static readonly IReadOnlyList<TimeBand> All = Enum.GetValues<TimeBand>();

    /// <summary>Segunda a domingo, a ordem das linhas da grade.</summary>
    public static readonly IReadOnlyList<DayOfWeek> WeekDays =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday,
    ];

    public static int StartHour(TimeBand band) => band switch
    {
        TimeBand.From02To06 => 2,
        TimeBand.From06To10 => 6,
        TimeBand.From10To14 => 10,
        TimeBand.From14To18 => 14,
        TimeBand.From18To22 => 18,
        TimeBand.From22To02 => 22,
        _ => throw new ArgumentOutOfRangeException(nameof(band), band, "Faixa horária desconhecida."),
    };

    /// <summary>Rótulo como "18h–22h".</summary>
    public static string Label(TimeBand band) =>
        $"{StartHour(band):00}h–{(StartHour(band) + LengthHours) % 24:00}h";

    /// <summary>Nome do dia da semana, como "Sexta-feira".</summary>
    public static string DayName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Segunda-feira",
        DayOfWeek.Tuesday => "Terça-feira",
        DayOfWeek.Wednesday => "Quarta-feira",
        DayOfWeek.Thursday => "Quinta-feira",
        DayOfWeek.Friday => "Sexta-feira",
        DayOfWeek.Saturday => "Sábado",
        DayOfWeek.Sunday => "Domingo",
        _ => throw new ArgumentOutOfRangeException(nameof(day), day, "Dia da semana desconhecido."),
    };

    /// <summary>A célula (dia da semana e faixa) a que pertence o horário local.</summary>
    public static (DayOfWeek Day, TimeBand Band) CellOf(DateTime local)
    {
        if (local.Hour < 2)
        {
            return (local.AddDays(-1).DayOfWeek, TimeBand.From22To02);
        }

        var band = local.Hour switch
        {
            < 6 => TimeBand.From02To06,
            < 10 => TimeBand.From06To10,
            < 14 => TimeBand.From10To14,
            < 18 => TimeBand.From14To18,
            < 22 => TimeBand.From18To22,
            _ => TimeBand.From22To02,
        };

        return (local.DayOfWeek, band);
    }

    /// <summary>Início local da faixa no dia informado (a 22h–02h começa às 22h desse dia).</summary>
    public static DateTime StartOf(DateOnly day, TimeBand band) => day.ToDateTime(new TimeOnly(StartHour(band), 0));

    /// <summary>
    /// As ocorrências de faixa que cruzam <c>[localStart, localEnd)</c>, em ordem de início (D-91): a faixa em andamento
    /// conta. Atravessa a meia-noite sem problema, porque a 22h–02h começa no dia anterior.
    /// </summary>
    public static IReadOnlyList<CellInstance> CellsBetween(DateTime localStart, DateTime localEnd)
    {
        var instances = new List<CellInstance>();
        var first = DateOnly.FromDateTime(localStart).AddDays(-1);
        var last = DateOnly.FromDateTime(localEnd);

        for (var day = first; day <= last; day = day.AddDays(1))
        {
            foreach (var band in All)
            {
                var start = StartOf(day, band);

                if (start < localEnd && start.AddHours(LengthHours) > localStart)
                {
                    instances.Add(new CellInstance(day.DayOfWeek, band, start));
                }
            }
        }

        return instances.OrderBy(instance => instance.StartLocal).ToArray();
    }
}
