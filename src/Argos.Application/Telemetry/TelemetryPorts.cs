using Argos.Contracts.Telemetry;
using Argos.Domain.Inventory.Agents;
using Argos.Domain.Inventory.Types;
using Argos.Domain.Telemetry;

namespace Argos.Application.Telemetry;

// Portas da telemetria (plan 16.8). Implementadas pela Infraestrutura no T113.

/// <summary>
/// Persistência da telemetria (D-70): estados atuais por tipo e ids, inclusão e atualização; inclusão das leituras do
/// lote. Sem remoção (Art. V.1). Também carrega os agentes do lote, rastreados, para a situação reportada (RN-12) ser
/// gravada no mesmo <c>SaveChanges</c> das leituras.
/// </summary>
public interface ITelemetryRepository
{
    /// <summary>Agentes pelos ids, inclusive inativos; os inexistentes ficam de fora.</summary>
    Task<IReadOnlyList<Agent>> FindAgentsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>Estados atuais existentes dos recursos do tipo informado.</summary>
    Task<IReadOnlyList<ResourceLiveState>> FindLiveStatesAsync(
        ResourceKind resourceKind, IReadOnlyCollection<Guid> resourceIds, CancellationToken cancellationToken = default);

    void AddLiveState(ResourceLiveState state);

    void AddReadings(IEnumerable<TelemetryReading> readings);
}

/// <summary>Consultas da telemetria (plan 16.4, 16.8): projeção direta, sem regra de negócio.</summary>
public interface ITelemetryQueries
{
    /// <summary>
    /// Recursos <b>ativos</b> para o Simulador (D-72), com o turno da escala de cada agente (<c>shift</c>, D-94). O
    /// <c>scheduledOnDuty</c> vem falso: quem o calcula é a Aplicação, pelo Domínio.
    /// </summary>
    Task<TelemetryResourceList> ListActiveResourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>Existência, atividade e categoria dos equipamentos do lote, numa consulta.</summary>
    Task<IReadOnlyDictionary<Guid, TelemetryEquipmentFacts>> FindEquipmentFactsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
}

/// <summary>O que a ingestão precisa saber de um equipamento do lote (D-71).</summary>
public sealed record TelemetryEquipmentFacts(Guid Id, bool IsActive, EquipmentCategory Category, bool CrewAssignable);
