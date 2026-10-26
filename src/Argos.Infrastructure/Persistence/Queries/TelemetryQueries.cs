using Argos.Application.Telemetry;
using Argos.Contracts.Telemetry;
using Microsoft.EntityFrameworkCore;
using C = Argos.Contracts.Inventory;
using O = Argos.Contracts.Operations;

namespace Argos.Infrastructure.Persistence.Queries;

/// <summary>
/// Consultas da telemetria (plan 16.4, 16.8): os recursos ativos para o PROMETHEUS (D-72) e os fatos de que a ingestão
/// precisa, sem rastreamento. Região da base do móvel e do agente pela base (D-65); o turno da escala vem de
/// <c>agent_duty_shifts</c> (D-94), e quem decide se ele cobre o instante atual é a Aplicação.
/// </summary>
internal sealed class TelemetryQueries(ArgosDbContext dbContext) : ITelemetryQueries
{
    public async Task<TelemetryResourceList> ListActiveResourcesAsync(CancellationToken cancellationToken = default)
    {
        var equipment = await (
                from item in dbContext.Equipment.AsNoTracking()
                join type in dbContext.EquipmentTypes on item.TypeId equals type.Id
                join operationalBase in dbContext.OperationalBases on item.BaseId equals (Guid?)operationalBase.Id into bases
                from operationalBase in bases.DefaultIfEmpty()
                join neighborhood in dbContext.Neighborhoods
                    on operationalBase!.Location.NeighborhoodId equals neighborhood.Id into neighborhoods
                from neighborhood in neighborhoods.DefaultIfEmpty()
                join region in dbContext.Regions on neighborhood!.RegionId equals region.Id into regions
                from region in regions.DefaultIfEmpty()
                where item.IsActive
                orderby item.Id
                select new
                {
                    item.Id,
                    item.Brand,
                    item.SerialNumber,
                    TypeKey = type.Key,
                    item.Category,
                    item.Status,
                    type.CrewAssignable,
                    HasBase = item.BaseId != null,
                    BaseLatitude = operationalBase == null ? (double?)null : operationalBase.Location.Point.Latitude,
                    BaseLongitude = operationalBase == null ? (double?)null : operationalBase.Location.Point.Longitude,
                    BaseRegion = region == null ? null : region.Name,
                })
            .ToListAsync(cancellationToken);

        var agents = await (
                from agent in dbContext.Agents.AsNoTracking()
                join organization in dbContext.Organizations on agent.OrganizationId equals organization.Id
                join operationalBase in dbContext.OperationalBases on agent.BaseId equals operationalBase.Id
                join neighborhood in dbContext.Neighborhoods on operationalBase.Location.NeighborhoodId equals neighborhood.Id
                join region in dbContext.Regions on neighborhood.RegionId equals region.Id
                join dutyShift in dbContext.AgentDutyShifts on agent.Id equals dutyShift.AgentId into shifts
                from dutyShift in shifts.DefaultIfEmpty()
                where agent.IsActive
                orderby agent.Id
                select new
                {
                    agent.Id,
                    organization.Acronym,
                    agent.RegistrationNumber,
                    agent.Role,
                    agent.Status,
                    BaseLatitude = operationalBase.Location.Point.Latitude,
                    BaseLongitude = operationalBase.Location.Point.Longitude,
                    BaseRegion = region.Name,
                    AssignedEquipmentId = dbContext.AgentAssignments
                        .Where(assignment => assignment.AgentId == agent.Id && assignment.EndedAt == null)
                        .Select(assignment => (Guid?)assignment.EquipmentId)
                        .FirstOrDefault(),
                    Shift = dutyShift == null ? (Domain.Operations.DutyShift?)null : dutyShift.Shift,
                })
            .ToListAsync(cancellationToken);

        return new TelemetryResourceList
        {
            Equipment = equipment
                .Select(item => new TelemetryEquipmentResource
                {
                    Id = item.Id,
                    Key = $"{item.Brand}/{item.SerialNumber}",
                    TypeKey = item.TypeKey,
                    Category = ContractMapping.Convert<C.EquipmentCategory>(item.Category),
                    Status = ContractMapping.Convert<C.EquipmentStatus>(item.Status),
                    CrewAssignable = item.CrewAssignable,
                    BaseLatitude = item.HasBase ? item.BaseLatitude : null,
                    BaseLongitude = item.HasBase ? item.BaseLongitude : null,
                    BaseRegion = item.HasBase ? item.BaseRegion : null,
                })
                .ToArray(),
            Agents = agents
                .Select(agent => new TelemetryAgentResource
                {
                    Id = agent.Id,
                    Key = $"{agent.Acronym}/{agent.RegistrationNumber}",
                    Role = ContractMapping.Convert<C.AgentRole>(agent.Role),
                    Status = ContractMapping.Convert<C.AgentStatus>(agent.Status),
                    BaseLatitude = agent.BaseLatitude,
                    BaseLongitude = agent.BaseLongitude,
                    BaseRegion = agent.BaseRegion,
                    AssignedEquipmentId = agent.AssignedEquipmentId,
                    Shift = ContractMapping.ConvertNullable<O.DutyShift>(agent.Shift),
                    ScheduledOnDuty = false,
                })
                .ToArray(),
        };
    }

    public async Task<IReadOnlyDictionary<Guid, TelemetryEquipmentFacts>> FindEquipmentFactsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, TelemetryEquipmentFacts>();
        }

        var values = ids.ToArray();
        var facts = await (
                from equipment in dbContext.Equipment.AsNoTracking()
                join type in dbContext.EquipmentTypes on equipment.TypeId equals type.Id
                where values.Contains(equipment.Id)
                select new { equipment.Id, equipment.IsActive, equipment.Category, type.CrewAssignable })
            .ToListAsync(cancellationToken);

        return facts.ToDictionary(
            item => item.Id,
            item => new TelemetryEquipmentFacts(item.Id, item.IsActive, item.Category, item.CrewAssignable));
    }
}
