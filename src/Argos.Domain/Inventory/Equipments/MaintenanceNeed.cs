namespace Argos.Domain.Inventory.Equipments;

/// <summary>
/// Necessidade de manutenção de um equipamento (RF-002, RF-010, RN-10; D-54), da menos para a mais grave.
/// Derivada das manutenções abertas, nunca guardada à parte.
/// </summary>
public enum MaintenanceNeed
{
    /// <summary>Nenhuma manutenção aberta.</summary>
    None,

    /// <summary>Alguma aberta com data além da janela.</summary>
    Scheduled,

    /// <summary>Alguma com data entre hoje e hoje + janela (RN-10).</summary>
    Upcoming,

    /// <summary>Alguma agendada ou periódica com data já passada (RN-10).</summary>
    Overdue,

    /// <summary>Uma manutenção imediata aberta.</summary>
    Immediate,
}

/// <summary>
/// Cálculo único da necessidade de manutenção (D-54; plan 15.4): prevalece a mais grave entre as manutenções abertas.
/// A consulta da Infraestrutura traduz a mesma regra para o banco, e um teste de integração confere a equivalência
/// (Art. V.3).
/// </summary>
public static class MaintenanceNeedCalculator
{
    /// <param name="maintenances">Manutenções do equipamento; só as abertas contam.</param>
    /// <param name="today">Data de hoje no fuso do município.</param>
    /// <param name="upcomingWindowDays">Parâmetro <c>maintenance.upcoming-window-days</c> (RN-10, RN-34).</param>
    public static MaintenanceNeed Calculate(IEnumerable<Maintenance> maintenances, DateOnly today, int upcomingWindowDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(upcomingWindowDays);

        var need = MaintenanceNeed.None;

        foreach (var maintenance in maintenances.Where(maintenance => maintenance.IsOpen))
        {
            var current = Classify(maintenance.Kind, maintenance.DueDate, today, upcomingWindowDays);
            if (current > need)
            {
                need = current;
            }
        }

        return need;
    }

    /// <summary>
    /// Necessidade que uma manutenção aberta representa, para o selo de vencida ou próxima de cada uma (RN-10). A
    /// Aplicação a usa sobre os dados lidos pela consulta, sem carregar a entidade.
    /// </summary>
    public static MaintenanceNeed Classify(MaintenanceKind kind, DateOnly? dueDate, DateOnly today, int upcomingWindowDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(upcomingWindowDays);

        if (kind == MaintenanceKind.Immediate || dueDate is not { } due)
        {
            return MaintenanceNeed.Immediate;
        }

        if (due < today)
        {
            return MaintenanceNeed.Overdue;
        }

        return due <= today.AddDays(upcomingWindowDays) ? MaintenanceNeed.Upcoming : MaintenanceNeed.Scheduled;
    }
}
