using Argos.Application.Common;
using Argos.Application.Inventory;
using Argos.Contracts.Telemetry;
using Argos.Domain.Operations;

namespace Argos.Application.Telemetry;

/// <summary>
/// Descoberta dos recursos pelo PROMETHEUS (RF-023; D-72, D-94; plan 16.4). Diz a cada agente se o turno da escala de
/// referência cobre o instante atual no fuso do município (<see cref="DutyShiftSchedule"/>, no Domínio): o Simulador não
/// calcula turno, só reage às bordas.
/// </summary>
public sealed class TelemetryResourceService(ITelemetryQueries queries, MunicipalCalendar calendar, TimeProvider timeProvider)
{
    public async Task<TelemetryResourceList> GetResourcesAsync(CancellationToken cancellationToken = default)
    {
        var resources = await queries.ListActiveResourcesAsync(cancellationToken);
        var municipality = await calendar.GetMunicipalityAsync(cancellationToken);
        var localTime = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), municipality.TimeZone).DateTime);

        return resources with
        {
            Agents = resources.Agents
                .Select(agent => agent with
                {
                    ScheduledOnDuty = agent.Shift is { } shift
                        && DutyShiftSchedule.Covers(EnumNames.Convert<DutyShift>(shift), localTime),
                })
                .ToArray(),
        };
    }
}
