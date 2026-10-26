using Argos.Application.Common;
using Argos.Application.Common.Abstractions;
using Argos.Application.Common.Exceptions;
using Argos.Application.Inventory.Support;
using Argos.Application.Operations;
using Argos.Contracts.Inventory;
using Argos.Contracts.Operations;
using Argos.Domain.Inventory;
using Argos.Domain.Inventory.Agents;
using Argos.Domain.Inventory.Support;
using DomainAgentRole = Argos.Domain.Inventory.Agents.AgentRole;

namespace Argos.Application.Inventory.Agents;

/// <summary>
/// Cadastro, edição e inativação de agentes (US-06, US-09, RF-013, RF-014, RN-22, RN-25, RN-35; D-49, D-60, D-62;
/// plan 15.2.9, 15.12). Os erros de campo saem todos de uma vez (D-46); a matrícula é única por órgão, inclusive entre
/// inativos (US-06). Cada caso de uso grava com um único <c>SaveChanges</c>.
/// <para>Depois de cada gravação, avisa a Central com <c>OperationsChanged</c> (D-80; plan 16.8).</para>
/// </summary>
public sealed class AgentService(
    IAgentRepository agents,
    IAgentAssignmentRepository assignments,
    IOrganizationRepository organizations,
    IOperationalBaseRepository bases,
    InventoryReferenceNames referenceNames,
    AgentQueryService agentQueries,
    IInventoryChangeLog changeLog,
    IUnitOfWork unitOfWork,
    IRealtimeNotifier notifier,
    ICurrentActor actor,
    TimeProvider timeProvider)
{
    /// <summary>Mensagem da matrícula repetida no órgão (US-06), a mesma quando a duplicidade é pega pelo banco.</summary>
    public const string DuplicateRegistrationMessage = "Já existe um agente com essa matrícula neste órgão.";

    /// <summary>
    /// Cadastra o agente na situação Fora de serviço, com o registro inicial no histórico (US-06, RN-12). Não gera
    /// registro cadastral de criação: o registro inicial de situação já o representa (plan 15.2.11).
    /// </summary>
    /// <exception cref="NotFoundException">Órgão ou base informados não existem.</exception>
    /// <exception cref="ConflictException">Matrícula repetida no órgão (US-06).</exception>
    public async Task<AgentDetails> CreateAsync(CreateAgentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (organization, operationalBase) = await ReferencesAsync(request, cancellationToken);
        var errors = new DomainErrorCollector();

        var agent = errors.Try(() => Agent.Register(
            request.FullName,
            request.DisplayName,
            request.RegistrationNumber,
            RoleOf(request),
            organization,
            request.Rank,
            operationalBase,
            timeProvider.GetUtcNow(),
            actor.Name));
        errors.ThrowIfAny();

        await EnsureUniqueRegistrationAsync(agent!, cancellationToken);

        agents.Add(agent!);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.PublishOperationsChangedAsync(
            OperationsChangeKind.Registration, timeProvider.GetUtcNow(), cancellationToken);

        return await agentQueries.GetDetailsAsync(agent!.Id, cancellationToken);
    }

    /// <summary>
    /// Edita o cadastro (RF-013) com a versão lida pelo cliente (D-60). Sem alteração, nada é gravado; com alteração,
    /// grava o registro cadastral com os nomes do órgão e da base (D-49). Cadastro inativo não é editado (RN-35).
    /// </summary>
    /// <exception cref="NotFoundException">Agente, órgão ou base inexistentes.</exception>
    /// <exception cref="ConflictException">Versão antiga (D-60) ou matrícula repetida no órgão (US-06).</exception>
    public async Task<AgentDetails> UpdateAsync(Guid id, UpdateAgentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var agent = await FindAsync(id, cancellationToken);

        if (request.Version is not { } version)
        {
            throw new ValidationFailedException("version", "Informe a versão do cadastro lida.");
        }

        if (agents.VersionOf(agent) != version)
        {
            throw new ConflictException(
                "O agente foi alterado por outra pessoa depois que você o abriu. Recarregue os dados e tente de novo.");
        }

        var (organization, operationalBase) = await ReferencesAsync(request, cancellationToken);
        var errors = new DomainErrorCollector();

        var changes = errors.Try(() => agent.UpdateRegistration(
            request.FullName,
            request.DisplayName,
            request.RegistrationNumber,
            RoleOf(request),
            organization,
            request.Rank,
            operationalBase));
        errors.ThrowIfAny();

        if (changes!.Count > 0)
        {
            await EnsureUniqueRegistrationAsync(agent, cancellationToken);
            changeLog.Add(InventoryChange.Updated(
                InventoryEntityType.Agent,
                id,
                await referenceNames.ResolveAsync(changes, cancellationToken),
                timeProvider.GetUtcNow(),
                actor.Name));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await notifier.PublishOperationsChangedAsync(
                OperationsChangeKind.Registration, timeProvider.GetUtcNow(), cancellationToken);
        }

        return await agentQueries.GetDetailsAsync(id, cancellationToken);
    }

    /// <summary>
    /// Inativa o agente (US-09, RN-35): só em Fora de serviço ou Afastado. O vínculo aberto, se houver, é encerrado
    /// pela <see cref="AssignmentPolicy"/> e gravado junto com o registro cadastral, com o motivo (D-49, D-62).
    /// </summary>
    /// <exception cref="NotFoundException">Agente inexistente.</exception>
    /// <exception cref="InventoryRuleException">Já inativo ou situação fora da RN-35 (409).</exception>
    public async Task InactivateAsync(Guid id, InactivationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var agent = await FindAsync(id, cancellationToken);
        var openAssignment = await assignments.FindOpenByAgentAsync(id, cancellationToken);

        var inactivation = AssignmentPolicy.InactivateAgent(
            agent, openAssignment, request.Reason, timeProvider.GetUtcNow(), actor.Name);

        changeLog.Add(inactivation.Change);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.PublishOperationsChangedAsync(
            OperationsChangeKind.Registration, timeProvider.GetUtcNow(), cancellationToken);
    }

    /// <summary>Função pedida; sem ela, um valor fora do enum faz o Domínio apontar <c>role</c> junto com os demais campos (D-46).</summary>
    private static DomainAgentRole RoleOf(AgentRegistrationRequest request) =>
        request.Role is { } role ? EnumNames.Convert<DomainAgentRole>(role) : (DomainAgentRole)(-1);

    /// <summary>Órgão e base informados, ou nulos para que o Domínio aponte o campo. Um id inexistente é 404 (plan 15.7).</summary>
    private async Task<(Organization?, OperationalBase?)> ReferencesAsync(
        AgentRegistrationRequest request, CancellationToken cancellationToken)
    {
        var organization = request.OrganizationId is { } organizationId
            ? await organizations.FindByIdAsync(organizationId, cancellationToken)
                ?? throw new NotFoundException("Órgão não encontrado.")
            : null;

        var operationalBase = request.BaseId is { } baseId
            ? await bases.FindByIdAsync(baseId, cancellationToken)
                ?? throw new NotFoundException("Base operacional não encontrada.")
            : null;

        return (organization, operationalBase);
    }

    private async Task<Agent> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await agents.FindByIdAsync(id, cancellationToken) ?? throw new NotFoundException(AgentQueryService.NotFoundMessage);

    /// <summary>US-06: matrícula única por órgão, inclusive entre inativos, pela chave normalizada.</summary>
    private async Task EnsureUniqueRegistrationAsync(Agent agent, CancellationToken cancellationToken)
    {
        var existingId = await agents.FindIdByRegistrationAsync(agent.OrganizationId, agent.RegistrationKey, cancellationToken);

        if (existingId is { } id && id != agent.Id)
        {
            throw new ConflictException(DuplicateRegistrationMessage);
        }
    }
}
