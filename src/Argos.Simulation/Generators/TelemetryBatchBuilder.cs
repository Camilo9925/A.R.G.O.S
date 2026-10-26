using Argos.Simulation.Geography;
using Argos.Simulation.Models;
using Argos.Simulation.Movement;

namespace Argos.Simulation.Generators;

/// <summary>
/// Monta o lote de telemetria de um ciclo (D-72 a D-75; plan 16.5). Função pura de (recursos, instante, estado do ciclo
/// anterior, configurações) → lote e estado seguinte, sem I/O (Art. IX.3). Regras:
/// <list type="bullet">
/// <item><b>Agente ativo fora de Afastado:</b> situação reportada só na borda do turno (entrada → Em serviço; saída →
/// Fora de serviço) e, no primeiro ciclo do agente, só onde a situação contradiz a escala. Uma mudança manual entre duas
/// bordas é respeitada. Afastado nunca é tocado.</item>
/// <item><b>Agente em atividade</b> (RN-13, depois das situações do lote): a posição do veículo vinculado, se ele se
/// move; senão, a própria rota a pé.</item>
/// <item><b>Móvel Operacional:</b> viatura e moto (<c>CrewAssignable</c>) andam só com tripulante em atividade e, sem
/// ele, ficam na base; os demais andam sempre. <b>Mau funcionamento ou Em manutenção:</b> na base. <b>Desativado,
/// Furtado ou Ainda não instalado:</b> nada.</item>
/// <item><b>Fixo em Operacional, Mau funcionamento ou Em manutenção:</b> sinal de vida no intervalo, o primeiro logo
/// na subida; nas demais situações, nada.</item>
/// </list>
/// Todos os itens usam o instante do ciclo, para a posição do agente que entra em turno já valer no mesmo lote (16.4).
/// </summary>
public sealed class TelemetryBatchBuilder(PatrolRoutes routes)
{
    private static readonly SimulatedEquipmentStatus[] HeartbeatStatuses =
    [
        SimulatedEquipmentStatus.Operational,
        SimulatedEquipmentStatus.Malfunction,
        SimulatedEquipmentStatus.UnderMaintenance,
    ];

    public TelemetryCycle Build(
        SimulatedTelemetryResources resources, DateTimeOffset now, TelemetryCycleState previous, TelemetryBatchSettings settings)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(settings);

        var (reports, effectiveStatus, nextSchedule) = StatusReports(resources.Agents, now, previous);
        var positions = new List<SimulatedPosition>();
        var movingVehicles = new Dictionary<Guid, GeoCoordinate>();

        foreach (var equipment in resources.Equipment.Where(item => item.Category == SimulatedEquipmentCategory.Mobile))
        {
            if (MobilePosition(equipment, resources.Agents, effectiveStatus, now, settings) is not (var point, var moving))
            {
                continue;
            }

            if (moving)
            {
                movingVehicles[equipment.Id] = point;
            }

            positions.Add(new SimulatedPosition(SimulatedResourceKind.Equipment, equipment.Id, point.Latitude, point.Longitude, now));
        }

        foreach (var agent in resources.Agents.Where(agent => IsInActivity(effectiveStatus[agent.Id])))
        {
            var point = agent.AssignedEquipmentId is { } vehicleId && movingVehicles.TryGetValue(vehicleId, out var vehiclePoint)
                ? vehiclePoint
                : PatrolRoutes.PositionAt(
                    routes.RouteFor(settings.Seed, agent.Key, agent.BaseRegion), settings.Speeds.OnFootKmh, now);
            positions.Add(new SimulatedPosition(SimulatedResourceKind.Agent, agent.Id, point.Latitude, point.Longitude, now));
        }

        var (heartbeats, nextHeartbeats) = Heartbeats(resources.Equipment, now, previous, settings.FixedHeartbeatInterval);

        return new TelemetryCycle(
            new SimulatedTelemetryBatch(now, reports, positions, heartbeats),
            new TelemetryCycleState(nextSchedule, nextHeartbeats));
    }

    /// <summary>RN-13, do lado da simulação: Em serviço ou Em atendimento.</summary>
    private static bool IsInActivity(SimulatedAgentStatus status) =>
        status is SimulatedAgentStatus.OnDuty or SimulatedAgentStatus.Responding;

    private static (List<SimulatedStatusReport> Reports, Dictionary<Guid, SimulatedAgentStatus> Effective, Dictionary<Guid, bool> Schedule)
        StatusReports(IReadOnlyList<SimulatedTelemetryAgent> agents, DateTimeOffset now, TelemetryCycleState previous)
    {
        var reports = new List<SimulatedStatusReport>();
        var effective = new Dictionary<Guid, SimulatedAgentStatus>();
        var schedule = new Dictionary<Guid, bool>();

        foreach (var agent in agents)
        {
            schedule[agent.Id] = agent.ScheduledOnDuty;
            effective[agent.Id] = agent.Status;

            if (agent.Status == SimulatedAgentStatus.OnLeave)
            {
                continue;
            }

            SimulatedAgentStatus? target;
            if (!previous.ScheduledOnDuty.TryGetValue(agent.Id, out var applied))
            {
                // Primeiro ciclo do agente: só corrige o que contradiz a escala.
                target = agent.ScheduledOnDuty switch
                {
                    true when agent.Status == SimulatedAgentStatus.OffDuty => SimulatedAgentStatus.OnDuty,
                    false when agent.Status is SimulatedAgentStatus.OnDuty or SimulatedAgentStatus.Responding
                        or SimulatedAgentStatus.OnBreak => SimulatedAgentStatus.OffDuty,
                    _ => null,
                };
            }
            else if (applied != agent.ScheduledOnDuty)
            {
                // Borda do turno.
                target = agent.ScheduledOnDuty ? SimulatedAgentStatus.OnDuty : SimulatedAgentStatus.OffDuty;
            }
            else
            {
                target = null;
            }

            if (target is { } status && status != agent.Status)
            {
                reports.Add(new SimulatedStatusReport(agent.Id, status, now));
                effective[agent.Id] = status;
            }
        }

        return (reports, effective, schedule);
    }

    private (GeoCoordinate Point, bool Moving)? MobilePosition(
        SimulatedTelemetryEquipment equipment,
        IReadOnlyList<SimulatedTelemetryAgent> agents,
        Dictionary<Guid, SimulatedAgentStatus> effectiveStatus,
        DateTimeOffset now,
        TelemetryBatchSettings settings)
    {
        if (equipment.Base is not { } basePoint)
        {
            return null;
        }

        switch (equipment.Status)
        {
            case SimulatedEquipmentStatus.Malfunction or SimulatedEquipmentStatus.UnderMaintenance:
                return (basePoint, false);

            case SimulatedEquipmentStatus.Operational:
                var crewInActivity = agents.Any(agent =>
                    agent.AssignedEquipmentId == equipment.Id && IsInActivity(effectiveStatus[agent.Id]));

                if ((equipment.CrewAssignable && !crewInActivity) || equipment.BaseRegion is null)
                {
                    return (basePoint, false);
                }

                var route = routes.RouteFor(settings.Seed, equipment.Key, equipment.BaseRegion);
                return (PatrolRoutes.PositionAt(route, settings.Speeds.ForType(equipment.TypeKey), now), true);

            default:
                return null;
        }
    }

    private static (List<SimulatedFixedHeartbeat> Heartbeats, Dictionary<Guid, DateTimeOffset> Next) Heartbeats(
        IReadOnlyList<SimulatedTelemetryEquipment> equipment, DateTimeOffset now, TelemetryCycleState previous, TimeSpan interval)
    {
        var heartbeats = new List<SimulatedFixedHeartbeat>();
        var next = new Dictionary<Guid, DateTimeOffset>();

        foreach (var item in equipment.Where(item =>
            item.Category == SimulatedEquipmentCategory.Fixed && HeartbeatStatuses.Contains(item.Status)))
        {
            if (previous.LastHeartbeatAt.TryGetValue(item.Id, out var last) && now - last < interval)
            {
                next[item.Id] = last;
                continue;
            }

            heartbeats.Add(new SimulatedFixedHeartbeat(item.Id, now));
            next[item.Id] = now;
        }

        return (heartbeats, next);
    }
}
