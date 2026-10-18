using System.Globalization;
using Argos.Application.Common;
using Argos.Contracts.Inventory;
using Argos.Domain.Inventory.Equipments;
using Argos.Domain.Inventory.Types;
using ContractMaintenanceNeed = Argos.Contracts.Inventory.MaintenanceNeed;
using DomainMaintenanceKind = Argos.Domain.Inventory.Equipments.MaintenanceKind;

namespace Argos.Application.Inventory.Equipments;

/// <summary>Mapeamento à mão entre o agregado do equipamento e os contratos (plan 2.3: sem bibliotecas de mapeamento).</summary>
internal static class EquipmentMapping
{
    public static MaintenanceData ToData(Maintenance maintenance, DateOnly today, int upcomingWindowDays) => new()
    {
        Id = maintenance.Id,
        Kind = EnumNames.Convert<Contracts.Inventory.MaintenanceKind>(maintenance.Kind),
        State = EnumNames.Convert<Contracts.Inventory.MaintenanceState>(maintenance.State),
        Description = maintenance.Description,
        DueDate = maintenance.DueDate,
        PeriodicityDays = maintenance.PeriodicityDays,
        Origin = EnumNames.Convert<Contracts.Inventory.MaintenanceOrigin>(maintenance.Origin),
        PreviousMaintenanceId = maintenance.PreviousMaintenanceId,
        Need = maintenance.IsOpen
            ? NeedOf(maintenance.Kind, maintenance.DueDate, today, upcomingWindowDays)
            : null,
        OpenedAt = maintenance.OpenedAt,
        OpenedBy = maintenance.OpenedBy,
        CompletedAt = maintenance.CompletedAt,
        CompletedBy = maintenance.CompletedBy,
        Result = EnumNames.ConvertNullable<Contracts.Inventory.MaintenanceResult>(maintenance.Result),
        CompletionNotes = maintenance.CompletionNotes,
        CancelledAt = maintenance.CancelledAt,
        CancelledBy = maintenance.CancelledBy,
        CancellationReason = maintenance.CancellationReason,
    };

    /// <summary>Completa a manutenção lida pela consulta com a necessidade que ela representa, se aberta (RN-10).</summary>
    public static MaintenanceData WithNeed(MaintenanceData maintenance, DateOnly today, int upcomingWindowDays) =>
        maintenance.State == Contracts.Inventory.MaintenanceState.Open
            ? maintenance with
            {
                Need = NeedOf(
                    EnumNames.Convert<DomainMaintenanceKind>(maintenance.Kind), maintenance.DueDate, today, upcomingWindowDays),
            }
            : maintenance with { Need = null };

    public static StatusChangeOptionData ToData(StatusChangeOption option) =>
        new(EnumNames.Convert<Contracts.Inventory.EquipmentStatus>(option.Target), option.RequiresJustification);

    /// <summary>
    /// Características na ordem das definições do tipo: o valor no texto invariante do contrato (plan 15.14) e o texto
    /// de exibição do Domínio.
    /// </summary>
    public static IReadOnlyList<AttributeValueData> Describe(EquipmentType type, AttributeValues values) =>
        type.Attributes
            .Where(attribute => values.ContainsKey(attribute.Key))
            .Select(attribute => new AttributeValueData(
                attribute.Key,
                attribute.Label,
                InvariantText(values[attribute.Key]),
                type.Display(attribute.Key, values[attribute.Key])))
            .ToArray();

    /// <summary>
    /// Valor tipado no texto invariante do contrato, o mesmo que o cadastro aceita: inteiro e decimal com ponto,
    /// <c>"true"</c>/<c>"false"</c>, a chave da opção e as chaves da múltipla escolha separadas por vírgula.
    /// </summary>
    public static string InvariantText(object value) => value switch
    {
        string text => text,
        bool flag => flag ? "true" : "false",
        long number => number.ToString(CultureInfo.InvariantCulture),
        decimal number => number.ToString("0.##", CultureInfo.InvariantCulture),
        IEnumerable<string> keys => string.Join(",", keys),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    /// <summary>Contrato → Domínio, para as características: o Domínio converte e valida o texto (plan 15.5).</summary>
    public static IReadOnlyDictionary<string, object?> AttributeInput(Dictionary<string, string?>? attributes) =>
        attributes?.ToDictionary(entry => entry.Key, entry => (object?)entry.Value, StringComparer.Ordinal)
        ?? new Dictionary<string, object?>();

    private static ContractMaintenanceNeed NeedOf(
        DomainMaintenanceKind kind, DateOnly? dueDate, DateOnly today, int upcomingWindowDays) =>
        EnumNames.Convert<ContractMaintenanceNeed>(MaintenanceNeedCalculator.Classify(kind, dueDate, today, upcomingWindowDays));
}
