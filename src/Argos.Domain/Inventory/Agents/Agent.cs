using System.Text.RegularExpressions;
using Argos.Domain.Common;
using Argos.Domain.Inventory.Support;

namespace Argos.Domain.Inventory.Agents;

/// <summary>
/// Agente de campo: de trânsito, guarda municipal ou policial militar (RF-013 a RF-015, US-06, US-09, RN-12, RN-22,
/// RN-25, RN-35; plan 15.2.9). Raiz do agregado: a situação e o histórico de situação só mudam pelas operações daqui.
/// <para>
/// <b>RN-25 (LGPD, Art. VIII.1):</b> só os dados mínimos da operação (RF-014). Não há campo de documento pessoal,
/// endereço residencial, telefone, data de nascimento nem saúde, e um teste compara as propriedades com uma lista
/// fechada. A posição atual entra na rodada da Telemetria, com o recorte da RN-27.
/// </para>
/// Nunca é excluído, só inativado (RN-23, RN-35); inativado, não é editado, não muda de situação e não volta a ficar
/// ativo. O vínculo com equipamento móvel é outro agregado (<see cref="AgentAssignment"/>), e o seu encerramento na
/// inativação é coordenado pela <see cref="AssignmentPolicy"/> (D-62).
/// </summary>
public sealed partial class Agent : IAuditable, IDeactivatable
{
    public const int FullNameMinLength = 3;
    public const int FullNameMaxLength = 150;
    public const int DisplayNameMinLength = 2;
    public const int DisplayNameMaxLength = 50;
    public const int RegistrationNumberMaxLength = 20;
    public const int RankMaxLength = 50;
    public const int InactivationReasonMaxLength = 500;

    /// <summary>Situação de todo agente recém-cadastrado (RN-12, US-06).</summary>
    public const AgentStatus InitialStatus = AgentStatus.OffDuty;

    /// <summary>Situações em que o cadastro pode ser inativado (RN-35).</summary>
    public static readonly IReadOnlyList<AgentStatus> InactivatableStatuses = [AgentStatus.OffDuty, AgentStatus.OnLeave];

    private readonly List<AgentStatusChange> _statusChanges = [];

    // Usado pelo EF Core ao materializar.
    private Agent()
    {
    }

    private Agent(Guid id, Registration registration)
    {
        Id = id;
        Apply(registration);
        Status = InitialStatus;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public string FullName { get; private set; } = string.Empty;

    /// <summary>Nome de exibição ou de guerra, o que aparece no mapa e nos detalhes (US-14).</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Matrícula, única por órgão, inclusive entre inativos (US-06).</summary>
    public string RegistrationNumber { get; private set; } = string.Empty;

    /// <summary>Chave normalizada da matrícula (D-53), que a Aplicação consulta com o órgão antes de gravar (US-06).</summary>
    public string RegistrationKey { get; private set; } = string.Empty;

    public AgentRole Role { get; private set; }

    /// <summary>Órgão do agente, exatamente um (RN-22).</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Posto ou graduação, "quando aplicável" (RF-014).</summary>
    public string? Rank { get; private set; }

    /// <summary>Base de atuação (RF-014). A região do agente é a do bairro da base (D-65).</summary>
    public Guid BaseId { get; private set; }

    public AgentStatus Status { get; private set; }

    /// <summary>Registros de situação acrescentados pelo agregado; nunca alterados nem removidos (Art. V.1).</summary>
    public IReadOnlyCollection<AgentStatusChange> StatusChanges => _statusChanges;

    public bool IsActive { get; private set; }

    public DateTimeOffset? DeactivatedAt { get; private set; }

    public string? DeactivatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; private set; }

    public string? UpdatedBy { get; private set; }

    /// <summary>
    /// Cadastra o agente (US-06), com todos os erros de campo de uma vez (D-46), na situação inicial Fora de serviço
    /// e com o registro inicial no histórico, sem situação anterior (RN-12). A matrícula única por órgão é consultada
    /// pela Aplicação, com <see cref="RegistrationKey"/>.
    /// </summary>
    /// <param name="organization">Órgão escolhido, ou nulo se não foi informado (RN-22).</param>
    /// <param name="operationalBase">Base escolhida, ou nula se não foi informada.</param>
    public static Agent Register(
        string fullName,
        string displayName,
        string registrationNumber,
        AgentRole role,
        Organization? organization,
        string? rank,
        OperationalBase? operationalBase,
        DateTimeOffset at,
        string by)
    {
        var author = InventoryAuthor.Valid(by);
        var registration = Validate(
            fullName, displayName, registrationNumber, role, organization, rank, operationalBase, current: null);

        var agent = new Agent(Guid.NewGuid(), registration);
        agent._statusChanges.Add(AgentStatusChange.Record(
            agent.Id, null, InitialStatus, AgentStatusChangeOrigin.Registration, at, author));
        return agent;
    }

    /// <summary>
    /// Edita o cadastro (RF-013). Devolve os campos alterados para o registro cadastral (D-49); sem alteração, lista
    /// vazia e nada a gravar. Órgão ou base que ficaram inativos continuam aceitos se já eram os do agente; trocar para
    /// um inativo é recusado (plan 15.2.2). Cadastro inativo não é editado (RN-35).
    /// </summary>
    /// <exception cref="InventoryRuleException">Cadastro inativo (RN-35).</exception>
    public IReadOnlyList<FieldChange> UpdateRegistration(
        string fullName,
        string displayName,
        string registrationNumber,
        AgentRole role,
        Organization? organization,
        string? rank,
        OperationalBase? operationalBase)
    {
        EnsureActive("Agente inativo não pode ser editado.");
        var next = Validate(
            fullName, displayName, registrationNumber, role, organization, rank, operationalBase, current: this);

        var changes = new List<FieldChange>();

        void Compare(string field, string label, string? before, string? after)
        {
            if (before != after)
            {
                changes.Add(new FieldChange(field, label, before, after));
            }
        }

        Compare("fullName", "Nome", FullName, next.FullName);
        Compare("displayName", "Nome de exibição", DisplayName, next.DisplayName);
        Compare("registrationNumber", "Matrícula", RegistrationNumber, next.RegistrationNumber);
        Compare("role", "Função", AgentRoleLabels.For(Role), AgentRoleLabels.For(next.Role));
        Compare("organizationId", "Órgão", OrganizationId.ToString(), next.OrganizationId.ToString());
        Compare("rank", "Posto ou graduação", Rank, next.Rank);
        Compare("baseId", "Base operacional", BaseId.ToString(), next.BaseId.ToString());

        Apply(next);
        return changes;
    }

    /// <summary>
    /// Muda a situação (RN-12): qualquer situação diferente da atual, sem justificativa, registrada com instante e
    /// autor. Recusa cadastro inativo (RN-35) e a mesma situação, nessa ordem.
    /// </summary>
    /// <exception cref="InventoryRuleException">Cadastro inativo (RN-35) ou mesma situação.</exception>
    /// <exception cref="DomainValidationException">Situação desconhecida, no campo <c>status</c>.</exception>
    public void ChangeStatus(AgentStatus target, DateTimeOffset at, string by)
    {
        var author = InventoryAuthor.Valid(by);
        EnsureActive("Agente inativo não pode mudar de situação.");

        if (!Enum.IsDefined(target))
        {
            throw new DomainValidationException("status", "Escolha a situação do agente.");
        }

        if (target == Status)
        {
            throw new InventoryRuleException("O agente já está nessa situação.");
        }

        _statusChanges.Add(AgentStatusChange.Record(Id, Status, target, AgentStatusChangeOrigin.Manual, at, author));
        Status = target;
    }

    /// <summary>
    /// Inativa o cadastro (US-09, RN-35; plan 15.2.9): só em Fora de serviço ou Afastado, uma única vez, sem volta.
    /// Devolve o registro cadastral <see cref="InventoryChangeKind.Inactivated"/>, com o motivo, para a Aplicação
    /// gravar (D-49). Não existe exclusão (RN-23). O encerramento do vínculo aberto é feito pela
    /// <see cref="AssignmentPolicy.InactivateAgent"/>, que chama este método (D-62).
    /// </summary>
    /// <param name="reason">Motivo opcional, até <see cref="InactivationReasonMaxLength"/> caracteres.</param>
    /// <exception cref="InventoryRuleException">Cadastro já inativo ou situação fora da RN-35.</exception>
    /// <exception cref="DomainValidationException">Motivo longo demais, no campo <c>reason</c>.</exception>
    public InventoryChange Inactivate(string? reason, DateTimeOffset at, string by)
    {
        var author = InventoryAuthor.Valid(by);
        EnsureActive("O agente já está inativo.");

        var errors = new FieldErrors();
        var validReason = errors.OptionalText("reason", reason, InactivationReasonMaxLength, "o motivo");
        errors.ThrowIfAny();

        if (!InactivatableStatuses.Contains(Status))
        {
            throw new InventoryRuleException(
                $"Agente {AgentStatusLabels.For(Status)} não pode ser inativado. A inativação só é possível nas "
                + "situações Fora de serviço ou Afastado.");
        }

        IsActive = false;
        DeactivatedAt = at.ToUniversalTime();
        DeactivatedBy = author;
        return InventoryChange.Inactivated(InventoryEntityType.Agent, Id, validReason, at, author);
    }

    private void EnsureActive(string message)
    {
        if (!IsActive)
        {
            throw new InventoryRuleException(message);
        }
    }

    private static Registration Validate(
        string fullName,
        string displayName,
        string registrationNumber,
        AgentRole role,
        Organization? organization,
        string? rank,
        OperationalBase? operationalBase,
        Agent? current)
    {
        var errors = new FieldErrors();
        var validFullName = errors.RequiredText("fullName", fullName, FullNameMinLength, FullNameMaxLength, "o nome");
        var validDisplayName = errors.RequiredText(
            "displayName", displayName, DisplayNameMinLength, DisplayNameMaxLength, "o nome de exibição");
        var validRegistration = errors.RequiredText(
            "registrationNumber", registrationNumber, 1, RegistrationNumberMaxLength, "a matrícula");

        if (validRegistration.Length > 0 && !errors.Has("registrationNumber") && !RegistrationPattern().IsMatch(validRegistration))
        {
            errors.Add("registrationNumber", "A matrícula aceita só letras sem acento, números, ponto, barra e hífen.");
        }

        if (!Enum.IsDefined(role))
        {
            errors.Add("role", "Escolha a função do agente.");
        }

        if (organization is null)
        {
            errors.Add("organizationId", "Escolha o órgão do agente.");
        }
        else if (!organization.IsActive && organization.Id != current?.OrganizationId)
        {
            errors.Add("organizationId", "O órgão escolhido está inativo.");
        }

        var validRank = errors.OptionalText("rank", rank, RankMaxLength, "o posto ou graduação");

        if (operationalBase is null)
        {
            errors.Add("baseId", "Escolha a base de atuação do agente.");
        }
        else if (!operationalBase.IsActive && operationalBase.Id != current?.BaseId)
        {
            errors.Add("baseId", "A base escolhida está inativa.");
        }

        errors.ThrowIfAny();
        return new Registration(
            validFullName, validDisplayName, validRegistration, role, organization!.Id, validRank, operationalBase!.Id);
    }

    private void Apply(Registration registration)
    {
        FullName = registration.FullName;
        DisplayName = registration.DisplayName;
        RegistrationNumber = registration.RegistrationNumber;
        RegistrationKey = TextNormalization.ToKey(registration.RegistrationNumber);
        Role = registration.Role;
        OrganizationId = registration.OrganizationId;
        Rank = registration.Rank;
        BaseId = registration.BaseId;
    }

    [GeneratedRegex("^[A-Za-z0-9./-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex RegistrationPattern();

    private sealed record Registration(
        string FullName,
        string DisplayName,
        string RegistrationNumber,
        AgentRole Role,
        Guid OrganizationId,
        string? Rank,
        Guid BaseId);
}
