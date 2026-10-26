using Argos.Application.Common;
using Argos.Application.Common.Abstractions;
using Argos.Application.Common.Exceptions;
using Argos.Application.Inventory.Agents;
using Argos.Application.Inventory.Equipments;
using Argos.Contracts.Operations;
using Argos.Domain.Inventory.Agents;
using DomainAgentStatus = Argos.Domain.Inventory.Agents.AgentStatus;
using DomainResourceKind = Argos.Domain.Telemetry.ResourceKind;

namespace Argos.Application.Operations;

/// <summary>
/// Detalhes pelo mapa (US-14, RF-034; D-81; plan 16.8, 16.10): os detalhes do Inventário (com as situações de destino
/// já filtradas pela permissão, D-59) + estado atual + alertas abertos. A posição de agentes, e a de viaturas e motos,
/// só para quem tem <c>agents.positions.view</c> (RN-27, A-38).
/// </summary>
public sealed class OperationsDetailsService(
    IOperationsQueries queries,
    EquipmentQueryService equipmentQueries,
    AgentQueryService agentQueries,
    ICurrentActor actor)
{
    public const string AgentPositionsForbiddenMessage = "Seu perfil não pode ver a posição de agentes.";

    /// <exception cref="NotFoundException">Equipamento inexistente.</exception>
    public async Task<OperationsEquipmentDetails> GetEquipmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var equipment = await equipmentQueries.GetDetailsAsync(id, cancellationToken);
        var live = await queries.FindEquipmentLiveAsync(id, cancellationToken);
        var canSeeAgents = OperationsPermissions.CanSeeAgentPositions(actor);
        var canHandleAlerts = OperationsPermissions.CanHandleAlerts(actor);

        var position = live?.Live is { Position: { } point, PositionRecordedAt: { } recordedAt }
            && (!live.CrewAssignable || canSeeAgents)
                ? new PositionData { Latitude = point.Latitude, Longitude = point.Longitude, RecordedAt = recordedAt }
                : null;

        var alerts = await queries.ListOpenAlertsOfAsync(DomainResourceKind.Equipment, id, cancellationToken);

        return new OperationsEquipmentDetails
        {
            Equipment = equipment,
            CurrentPosition = position,
            LastCommunicationAt = live?.Live?.LastCommunicationAt,
            OpenAlerts = OperationsMapping.Ordered(alerts)
                .Select(row => OperationsMapping.ToSummary(row, outsideFilter: false, canHandleAlerts, canSeeAgents))
                .ToArray(),
        };
    }

    /// <exception cref="ForbiddenOperationException">Perfil sem <c>agents.positions.view</c> (RN-27).</exception>
    /// <exception cref="NotFoundException">Agente inexistente.</exception>
    public async Task<OperationsAgentDetails> GetAgentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!OperationsPermissions.CanSeeAgentPositions(actor))
        {
            throw new ForbiddenOperationException(AgentPositionsForbiddenMessage);
        }

        var agent = await agentQueries.GetDetailsAsync(id, cancellationToken);
        var live = await queries.FindAgentLiveAsync(id, cancellationToken);

        // Art. VIII.2 e V.4: só a posição medida no período de atividade atual, como no marcador.
        var inActivity = agent.IsActive && AgentActivity.IsInActivity(EnumNames.Convert<DomainAgentStatus>(agent.Status));
        var position = inActivity && live is { Position: { } point, PositionRecordedAt: { } recordedAt }
            && recordedAt >= agent.StatusChangedAt
                ? new PositionData { Latitude = point.Latitude, Longitude = point.Longitude, RecordedAt = recordedAt }
                : null;

        return new OperationsAgentDetails
        {
            Id = agent.Id,
            DisplayName = agent.DisplayName,
            Role = agent.Role,
            OrganizationName = agent.OrganizationName,
            OrganizationAcronym = agent.OrganizationAcronym,
            Status = agent.Status,
            StatusChangedAt = agent.StatusChangedAt,
            BaseName = agent.BaseName,
            AssignedEquipment = agent.AssignedEquipment,
            Position = position,
            AllowedStatusChanges = agent.AllowedStatusChanges,
        };
    }
}
