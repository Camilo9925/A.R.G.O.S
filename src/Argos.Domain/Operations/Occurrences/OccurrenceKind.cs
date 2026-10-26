namespace Argos.Domain.Operations.Occurrences;

/// <summary>Tipo de ocorrência (A-32; plan 17.2). A ordem é a do sorteio do gerador e a do contrato.</summary>
public enum OccurrenceKind
{
    TrafficAccident,
    TheftOrRobbery,
    Vandalism,
    Congestion,
    SevereTrafficViolation,
}

/// <summary>Rótulo e peso de gravidade de cada tipo de ocorrência (A-32; plan 17.2): a base da intensidade do risco.</summary>
public static class OccurrenceKindCatalog
{
    public static string Label(OccurrenceKind kind) => kind switch
    {
        OccurrenceKind.TrafficAccident => "Acidente de trânsito",
        OccurrenceKind.TheftOrRobbery => "Furto ou roubo",
        OccurrenceKind.Vandalism => "Vandalismo",
        OccurrenceKind.Congestion => "Congestionamento",
        OccurrenceKind.SevereTrafficViolation => "Infração de trânsito grave",
        _ => throw Unknown(kind),
    };

    /// <summary>Peso de gravidade: acidente e furto 3, vandalismo 2, congestionamento e infração grave 1.</summary>
    public static int Weight(OccurrenceKind kind) => kind switch
    {
        OccurrenceKind.TrafficAccident => 3,
        OccurrenceKind.TheftOrRobbery => 3,
        OccurrenceKind.Vandalism => 2,
        OccurrenceKind.Congestion => 1,
        OccurrenceKind.SevereTrafficViolation => 1,
        _ => throw Unknown(kind),
    };

    private static ArgumentOutOfRangeException Unknown(OccurrenceKind kind) =>
        new(nameof(kind), kind, "Tipo de ocorrência desconhecido.");
}
