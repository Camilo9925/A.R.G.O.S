using Argos.Application.Inventory.Timeline;
using Argos.Contracts.Common;

namespace Argos.UnitTests.Application.Fakes;

/// <summary>
/// Linha do tempo com os fatos escolhidos pelo teste, já na ordem da consulta real (do mais recente para o mais
/// antigo); só pagina e guarda o recurso pedido.
/// </summary>
internal sealed class FakeTimelineQueries : ITimelineQueries
{
    public List<TimelineFact> Facts { get; } = [];

    public (string Resource, Guid Id, int Page, int PageSize)? LastRequest { get; private set; }

    public Task<PagedResult<TimelineFact>> ListEquipmentTimelineAsync(
        Guid equipmentId, int page, int pageSize, CancellationToken cancellationToken = default) =>
        Page("equipment", equipmentId, page, pageSize);

    public Task<PagedResult<TimelineFact>> ListAgentTimelineAsync(
        Guid agentId, int page, int pageSize, CancellationToken cancellationToken = default) =>
        Page("agent", agentId, page, pageSize);

    private Task<PagedResult<TimelineFact>> Page(string resource, Guid id, int page, int pageSize)
    {
        LastRequest = (resource, id, page, pageSize);
        return Task.FromResult(new PagedResult<TimelineFact>
        {
            Items = Facts.Skip((page - 1) * pageSize).Take(pageSize).ToArray(),
            Page = page,
            PageSize = pageSize,
            TotalCount = Facts.Count,
        });
    }
}
