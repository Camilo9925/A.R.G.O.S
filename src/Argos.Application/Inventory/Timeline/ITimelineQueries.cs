using Argos.Contracts.Common;
using Argos.Contracts.Inventory;
using Argos.Domain.Inventory;
using DomainAgentStatus = Argos.Domain.Inventory.Agents.AgentStatus;
using DomainAssignmentEndReason = Argos.Domain.Inventory.Agents.AssignmentEndReason;
using DomainEquipmentStatus = Argos.Domain.Inventory.Equipments.EquipmentStatus;
using DomainMaintenanceKind = Argos.Domain.Inventory.Equipments.MaintenanceKind;
using DomainMaintenanceOrigin = Argos.Domain.Inventory.Equipments.MaintenanceOrigin;
using DomainMaintenanceResult = Argos.Domain.Inventory.Equipments.MaintenanceResult;
using DomainStatusChangeOrigin = Argos.Domain.Inventory.Equipments.StatusChangeOrigin;

namespace Argos.Application.Inventory.Timeline;

/// <summary>
/// Linha do tempo de equipamento e de agente (US-08, RF-011, RF-018; Art. V.1; D-49; plan 15.6). A consulta lê as
/// fontes do recurso (registros de situação, manutenções, registros cadastrais e vínculos), projeta cada fato num
/// <see cref="TimelineFact"/> e ordena do mais recente para o mais antigo, de forma estável, com paginação por página.
/// Os textos em pt-BR ficam com o <see cref="TimelineService"/>.
/// </summary>
public interface ITimelineQueries
{
    /// <summary>
    /// Fatos do equipamento: cadastro e mudanças de situação, manutenções abertas, concluídas e canceladas,
    /// alterações de cadastro, inativação e início e fim dos vínculos com agentes.
    /// </summary>
    Task<PagedResult<TimelineFact>> ListEquipmentTimelineAsync(
        Guid equipmentId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Fatos do agente: cadastro e mudanças de situação, alterações de cadastro, inativação e vínculos.</summary>
    Task<PagedResult<TimelineFact>> ListAgentTimelineAsync(
        Guid agentId, int page, int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>
/// Um fato do histórico de um recurso, com o instante de negócio e o autor (Art. V.1), e os dados da fonte de onde
/// veio: só a parte do <see cref="Kind"/> é preenchida.
/// </summary>
/// <param name="Kind">
/// <see cref="TimelineEntryKind.Registered"/> e <see cref="TimelineEntryKind.StatusChanged"/> trazem
/// <see cref="EquipmentStatus"/> ou <see cref="AgentStatus"/>; os de manutenção, <see cref="Maintenance"/>;
/// <see cref="TimelineEntryKind.RegistrationUpdated"/> e <see cref="TimelineEntryKind.Inactivated"/>,
/// <see cref="Registration"/>; os de vínculo, <see cref="Crew"/>.
/// </param>
public sealed record TimelineFact(TimelineEntryKind Kind, DateTimeOffset OccurredAt, string Author)
{
    public TimelineEquipmentStatus? EquipmentStatus { get; init; }

    public TimelineAgentStatus? AgentStatus { get; init; }

    public TimelineMaintenance? Maintenance { get; init; }

    public TimelineRegistration? Registration { get; init; }

    public TimelineCrew? Crew { get; init; }
}

/// <summary>Registro de situação do equipamento (plan 15.2.7); <paramref name="From"/> nulo no registro inicial.</summary>
public sealed record TimelineEquipmentStatus(
    DomainEquipmentStatus? From, DomainEquipmentStatus To, DomainStatusChangeOrigin Origin, string? Justification);

/// <summary>Registro de situação do agente (plan 15.2.9); <paramref name="From"/> nulo no registro inicial.</summary>
public sealed record TimelineAgentStatus(DomainAgentStatus? From, DomainAgentStatus To);

/// <summary>
/// Manutenção (plan 15.2.8). A mesma manutenção gera o fato da abertura e, depois, o da conclusão ou do cancelamento.
/// </summary>
public sealed record TimelineMaintenance(
    DomainMaintenanceKind Kind,
    DomainMaintenanceOrigin Origin,
    string? Description,
    DateOnly? DueDate,
    int? PeriodicityDays,
    DomainMaintenanceResult? Result,
    string? CompletionNotes,
    string? CancellationReason);

/// <summary>Registro cadastral (plan 15.2.11): os campos alterados, já com os nomes, e o motivo da inativação.</summary>
public sealed record TimelineRegistration(IReadOnlyList<FieldChange> Changes, string? Reason);

/// <summary>
/// Vínculo entre agente e equipamento móvel (plan 15.2.10), com os dois lados identificados: o fato do início não tem
/// <paramref name="EndReason"/>; o do fim tem.
/// </summary>
public sealed record TimelineCrew(
    Guid AgentId,
    string AgentDisplayName,
    string AgentRegistrationNumber,
    Guid EquipmentId,
    string EquipmentTypeName,
    string EquipmentSerialNumber,
    DomainAssignmentEndReason? EndReason);
