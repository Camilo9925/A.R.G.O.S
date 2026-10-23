using System.Globalization;
using Argos.Application.Common;
using Argos.Application.Common.Exceptions;
using Argos.Application.Inventory.Agents;
using Argos.Application.Inventory.Equipments;
using Argos.Contracts.Common;
using Argos.Contracts.Inventory;
using Argos.Domain.Inventory.Agents;
using Argos.Domain.Inventory.Equipments;
using DomainAssignmentEndReason = Argos.Domain.Inventory.Agents.AssignmentEndReason;
using DomainMaintenanceKind = Argos.Domain.Inventory.Equipments.MaintenanceKind;
using DomainMaintenanceOrigin = Argos.Domain.Inventory.Equipments.MaintenanceOrigin;
using DomainMaintenanceResult = Argos.Domain.Inventory.Equipments.MaintenanceResult;
using DomainStatusChangeOrigin = Argos.Domain.Inventory.Equipments.StatusChangeOrigin;

namespace Argos.Application.Inventory.Timeline;

/// <summary>
/// Linha do tempo de equipamento e de agente (US-08, RF-011, RF-018; Art. V.1; D-49; plan 15.6): os fatos vêm de
/// <see cref="ITimelineQueries"/>, do mais recente para o mais antigo, e aqui ganham título, descrição e detalhes em
/// pt-BR (Art. XIII.2), para a Web só exibir. Os vínculos são descritos pelo lado de quem consulta: na do equipamento,
/// o agente; na do agente, o equipamento.
/// </summary>
public sealed class TimelineService(ITimelineQueries timeline, IInventoryQueries inventoryQueries, IAgentQueries agentQueries)
{
    /// <summary>Valor de um campo vazio nas alterações de cadastro.</summary>
    private const string Empty = "(vazio)";

    /// <exception cref="NotFoundException">Equipamento inexistente.</exception>
    /// <exception cref="ValidationFailedException">Página ou tamanho fora da faixa.</exception>
    public async Task<PagedResult<TimelineEntry>> ForEquipmentAsync(
        Guid id, PageQuery page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        PageValidation.Validate(page.Page, page.PageSize);

        if (!await inventoryQueries.EquipmentExistsAsync(id, cancellationToken))
        {
            throw new NotFoundException(EquipmentQueryService.NotFoundMessage);
        }

        var facts = await timeline.ListEquipmentTimelineAsync(id, page.Page, page.PageSize, cancellationToken);
        return Describe(facts, Subject.Equipment);
    }

    /// <exception cref="NotFoundException">Agente inexistente.</exception>
    /// <exception cref="ValidationFailedException">Página ou tamanho fora da faixa.</exception>
    public async Task<PagedResult<TimelineEntry>> ForAgentAsync(
        Guid id, PageQuery page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        PageValidation.Validate(page.Page, page.PageSize);

        if (!await agentQueries.AgentExistsAsync(id, cancellationToken))
        {
            throw new NotFoundException(AgentQueryService.NotFoundMessage);
        }

        var facts = await timeline.ListAgentTimelineAsync(id, page.Page, page.PageSize, cancellationToken);
        return Describe(facts, Subject.Agent);
    }

    private static PagedResult<TimelineEntry> Describe(PagedResult<TimelineFact> facts, Subject subject) => new()
    {
        Items = facts.Items.Select(fact => Describe(fact, subject)).ToArray(),
        Page = facts.Page,
        PageSize = facts.PageSize,
        TotalCount = facts.TotalCount,
    };

    private static TimelineEntry Describe(TimelineFact fact, Subject subject)
    {
        var (title, description, details) = fact.Kind switch
        {
            TimelineEntryKind.Registered => Registered(fact, subject),
            TimelineEntryKind.StatusChanged => StatusChanged(fact, subject),
            TimelineEntryKind.MaintenanceOpened => MaintenanceOpened(Part(fact.Maintenance, fact)),
            TimelineEntryKind.MaintenanceCompleted => MaintenanceCompleted(Part(fact.Maintenance, fact)),
            TimelineEntryKind.MaintenanceCancelled => MaintenanceCancelled(Part(fact.Maintenance, fact)),
            TimelineEntryKind.RegistrationUpdated => RegistrationUpdated(Part(fact.Registration, fact)),
            TimelineEntryKind.Inactivated => Inactivated(Part(fact.Registration, fact)),
            TimelineEntryKind.CrewAssigned => CrewAssigned(Part(fact.Crew, fact), subject),
            TimelineEntryKind.CrewUnassigned => CrewUnassigned(Part(fact.Crew, fact), subject),
            _ => throw new ArgumentOutOfRangeException(nameof(fact), fact.Kind, "Tipo de entrada desconhecido."),
        };

        return new TimelineEntry
        {
            OccurredAt = fact.OccurredAt,
            Kind = fact.Kind,
            Title = title,
            Description = description,
            Author = fact.Author,
            Details = details,
        };
    }

    // Situação (US-01, US-04, US-06, RN-05, RN-12)

    private static Text Registered(TimelineFact fact, Subject subject) => subject switch
    {
        Subject.Equipment => new(
            "Equipamento cadastrado",
            $"Situação inicial: {EquipmentStatusLabels.For(Part(fact.EquipmentStatus, fact).To)}.",
            []),
        _ => new("Agente cadastrado", $"Situação inicial: {AgentStatusLabels.For(Part(fact.AgentStatus, fact).To)}.", []),
    };

    private static Text StatusChanged(TimelineFact fact, Subject subject)
    {
        if (subject == Subject.Agent)
        {
            var agentStatus = Part(fact.AgentStatus, fact);
            return new(
                Arrow(agentStatus.From is { } from ? AgentStatusLabels.For(from) : null, AgentStatusLabels.For(agentStatus.To)),
                null,
                []);
        }

        var status = Part(fact.EquipmentStatus, fact);
        return new(
            Arrow(status.From is { } previous ? EquipmentStatusLabels.For(previous) : null, EquipmentStatusLabels.For(status.To)),
            status.Origin == DomainStatusChangeOrigin.MaintenanceCompletion ? "Resultado da conclusão de uma manutenção." : null,
            Details(("Justificativa", status.Justification)));
    }

    // Manutenções (US-05, RN-07 a RN-11, RN-35)

    private static Text MaintenanceOpened(TimelineMaintenance maintenance) => new(
        $"Manutenção {KindLabel(maintenance.Kind)} aberta",
        maintenance.Origin switch
        {
            DomainMaintenanceOrigin.AutomaticOnMalfunction => "Aberta automaticamente pelo mau funcionamento.",
            DomainMaintenanceOrigin.PeriodicRescheduling => "Programada na conclusão da manutenção periódica anterior.",
            _ => null,
        },
        Details(
            ("Data prevista", maintenance.DueDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
            ("Periodicidade", maintenance.PeriodicityDays is { } days ? (days == 1 ? "1 dia" : $"{days} dias") : null),
            ("Descrição", maintenance.Description)));

    private static Text MaintenanceCompleted(TimelineMaintenance maintenance) => new(
        $"Manutenção {KindLabel(maintenance.Kind)} concluída "
            + (maintenance.Result == DomainMaintenanceResult.Success ? "com sucesso" : "sem sucesso"),
        null,
        Details(("Observações", maintenance.CompletionNotes)));

    private static Text MaintenanceCancelled(TimelineMaintenance maintenance) => new(
        $"Manutenção {KindLabel(maintenance.Kind)} cancelada", maintenance.CancellationReason, []);

    // Cadastro (D-49, RN-35)

    private static Text RegistrationUpdated(TimelineRegistration registration) => new(
        "Cadastro alterado",
        registration.Changes.Count == 1 ? "1 campo alterado." : $"{registration.Changes.Count} campos alterados.",
        registration.Changes
            .Select(change => new TimelineDetailData(change.Label, $"{change.Before ?? Empty} → {change.After ?? Empty}"))
            .ToArray());

    private static Text Inactivated(TimelineRegistration registration) =>
        new("Cadastro inativado", null, Details(("Motivo", registration.Reason)));

    // Vínculos (US-07, RF-016, RF-018)

    private static Text CrewAssigned(TimelineCrew crew, Subject subject) => subject == Subject.Equipment
        ? new("Agente vinculado", AgentOf(crew), [])
        : new("Vinculado a equipamento", EquipmentOf(crew), []);

    private static Text CrewUnassigned(TimelineCrew crew, Subject subject) => new(
        subject == Subject.Equipment ? "Agente desvinculado" : "Vínculo encerrado",
        subject == Subject.Equipment ? AgentOf(crew) : EquipmentOf(crew),
        Details(("Motivo", crew.EndReason is { } reason ? EndReasonLabel(reason) : null)));

    private static string AgentOf(TimelineCrew crew) => $"{crew.AgentDisplayName} · matrícula {crew.AgentRegistrationNumber}";

    private static string EquipmentOf(TimelineCrew crew) => $"{crew.EquipmentTypeName} · {crew.EquipmentSerialNumber}";

    private static string EndReasonLabel(DomainAssignmentEndReason reason) => reason switch
    {
        DomainAssignmentEndReason.Reassigned => "Agente vinculado a outro equipamento",
        DomainAssignmentEndReason.Ended => "Encerrado manualmente",
        DomainAssignmentEndReason.AgentInactivated => "Agente inativado",
        DomainAssignmentEndReason.EquipmentInactivated => "Equipamento inativado",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Motivo de encerramento desconhecido."),
    };

    /// <summary>Tipo da manutenção como adjetivo, para "Manutenção periódica concluída com sucesso" (RN-08).</summary>
    private static string KindLabel(DomainMaintenanceKind kind) => kind switch
    {
        DomainMaintenanceKind.Immediate => "imediata",
        DomainMaintenanceKind.Scheduled => "agendada",
        DomainMaintenanceKind.Periodic => "periódica",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Tipo de manutenção desconhecido."),
    };

    private static string Arrow(string? from, string to) => from is null ? to : $"{from} → {to}";

    /// <summary>Só os detalhes com valor, na ordem dada.</summary>
    private static IReadOnlyList<TimelineDetailData> Details(params (string Label, string? Value)[] pairs) =>
        pairs
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => new TimelineDetailData(pair.Label, pair.Value!))
            .ToArray();

    /// <summary>A parte do fato que o tipo exige; a falta dela é erro da consulta, não do usuário.</summary>
    private static T Part<T>(T? part, TimelineFact fact)
        where T : class =>
        part ?? throw new InvalidOperationException($"Fato {fact.Kind} de {fact.OccurredAt:O} sem os dados de {typeof(T).Name}.");

    private enum Subject
    {
        Equipment,
        Agent,
    }

    private sealed record Text(string Title, string? Description, IReadOnlyList<TimelineDetailData> Details);
}
