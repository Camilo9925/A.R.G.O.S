using Argos.Domain.Common;
using Argos.Domain.Inventory.Geography;
using Argos.Domain.Inventory.Support;
using Argos.Domain.Inventory.Types;

namespace Argos.Domain.Inventory.Equipments;

/// <summary>
/// Equipamento fixo ou móvel (RF-001, RF-002, RF-007 a RF-009, US-01 a US-05, RN-01, RN-04 a RN-11, RN-22;
/// plan 15.2.6 a 15.2.8, 15.3, 15.4). Raiz do agregado: situação, histórico de situação e manutenções só mudam pelas
/// operações daqui. As coleções de histórico e de manutenções não precisam vir inteiras do banco: o histórico só
/// recebe registros novos, e as regras usam apenas as manutenções abertas. Nunca é excluído, só inativado (RN-23;
/// a inativação entra na Fase 3).
/// </summary>
public sealed class Equipment : IAuditable, IDeactivatable
{
    public const int SerialNumberMaxLength = 50;
    public const int BrandMaxLength = 60;
    public const int ModelMaxLength = 60;
    public const int JustificationMinLength = 3;
    public const int JustificationMaxLength = 500;
    public const int DescriptionMaxLength = 500;
    public const int CompletionNotesMaxLength = 1000;
    public const int MinPeriodicityDays = 1;
    public const int MaxPeriodicityDays = 3650;

    private readonly List<Maintenance> _maintenances = [];
    private readonly List<EquipmentStatusChange> _statusChanges = [];

    // Usado pelo EF Core ao materializar.
    private Equipment()
    {
    }

    private Equipment(Guid id, EquipmentType type, Registration registration, EquipmentStatus status, DateTimeOffset at)
    {
        Id = id;
        TypeId = type.Id;
        Category = type.Category;
        Apply(registration);
        Status = status;
        StatusChangedAt = at.ToUniversalTime();
        IsActive = true;
    }

    public Guid Id { get; private set; }

    /// <summary>Imutável após o cadastro, assim como a categoria (D-66).</summary>
    public Guid TypeId { get; private set; }

    public EquipmentCategory Category { get; private set; }

    public string SerialNumber { get; private set; } = string.Empty;

    public string Brand { get; private set; } = string.Empty;

    public string Model { get; private set; } = string.Empty;

    /// <summary>Chave normalizada da marca (RN-01; D-53).</summary>
    public string BrandKey { get; private set; } = string.Empty;

    /// <summary>Chave normalizada do número de série (RN-01; D-53).</summary>
    public string SerialKey { get; private set; } = string.Empty;

    /// <summary>Órgão responsável, exatamente um (RN-22).</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Obrigatória para fixos e ausente para móveis (RF-007).</summary>
    public Location? Location { get; private set; }

    /// <summary>Base operacional, obrigatória para móveis e ausente para fixos (RF-007).</summary>
    public Guid? BaseId { get; private set; }

    public AttributeValues Attributes { get; private set; } = AttributeValues.Empty;

    public EquipmentStatus Status { get; private set; }

    public DateTimeOffset StatusChangedAt { get; private set; }

    public IReadOnlyCollection<Maintenance> Maintenances => _maintenances;

    /// <summary>Registros de situação acrescentados pelo agregado; nunca alterados nem removidos (Art. V.1).</summary>
    public IReadOnlyCollection<EquipmentStatusChange> StatusChanges => _statusChanges;

    public bool IsActive { get; private set; }

    public DateTimeOffset? DeactivatedAt { get; private set; }

    public string? DeactivatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; private set; }

    public string? UpdatedBy { get; private set; }

    public bool HasOpenImmediateMaintenance =>
        _maintenances.Any(maintenance => maintenance is { IsOpen: true, Kind: MaintenanceKind.Immediate });

    /// <summary>
    /// Cadastra o equipamento (US-01, D-66), com todos os erros de campo de uma vez (D-46). Cria o registro inicial
    /// no histórico, sem situação anterior, e, se a situação inicial for Mau funcionamento, abre a imediata (RN-07).
    /// A unicidade de marca e número de série (RN-01) é consultada pela Aplicação, com <see cref="BrandKey"/> e
    /// <see cref="SerialKey"/>.
    /// </summary>
    /// <param name="organization">Órgão escolhido, ou nulo se não foi informado.</param>
    /// <param name="location">Localização já montada por <see cref="Location.Create"/>; só para fixos.</param>
    /// <param name="operationalBase">Base escolhida; só para móveis.</param>
    public static Equipment Register(
        EquipmentType type,
        string serialNumber,
        string brand,
        string model,
        Organization? organization,
        Location? location,
        OperationalBase? operationalBase,
        IReadOnlyDictionary<string, object?>? attributes,
        EquipmentStatus initialStatus,
        DateTimeOffset at,
        string by)
    {
        ArgumentNullException.ThrowIfNull(type);
        var author = InventoryAuthor.Valid(by);
        var errors = new FieldErrors();

        if (!type.IsActive)
        {
            errors.Add("typeId", "O tipo escolhido está inativo.");
        }

        if (!EquipmentStatusTransitions.InitialStatuses.Contains(initialStatus))
        {
            errors.Add(
                "status",
                "Situação inicial permitida: Ainda não instalado, Operacional, Mau funcionamento ou Em manutenção.");
        }

        var registration = Validate(
            errors, type, serialNumber, brand, model, organization, location, operationalBase, attributes, current: null);

        var equipment = new Equipment(Guid.NewGuid(), type, registration, initialStatus, at);
        equipment._statusChanges.Add(EquipmentStatusChange.Record(
            equipment.Id, null, initialStatus, StatusChangeOrigin.Registration, null, null, at, author));

        if (initialStatus == EquipmentStatus.Malfunction)
        {
            equipment.OpenAutomaticImmediate(at, author);
        }

        return equipment;
    }

    /// <summary>
    /// Edita o cadastro; tipo e categoria não mudam (D-66). Devolve os campos alterados para o registro cadastral
    /// (D-49); sem alteração, lista vazia e nada a gravar. Órgão ou base que ficaram inativos continuam aceitos se já
    /// eram os do equipamento; trocar para um inativo é recusado (plan 15.2.2).
    /// </summary>
    public IReadOnlyList<FieldChange> UpdateRegistration(
        EquipmentType type,
        string serialNumber,
        string brand,
        string model,
        Organization? organization,
        Location? location,
        OperationalBase? operationalBase,
        IReadOnlyDictionary<string, object?>? attributes)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (type.Id != TypeId)
        {
            throw new ArgumentException("O tipo do equipamento não muda depois do cadastro (D-66).", nameof(type));
        }

        var errors = new FieldErrors();
        var next = Validate(
            errors, type, serialNumber, brand, model, organization, location, operationalBase, attributes, current: this);

        var changes = new List<FieldChange>();

        void Compare(string field, string label, string? before, string? after)
        {
            if (before != after)
            {
                changes.Add(new FieldChange(field, label, before, after));
            }
        }

        Compare("serialNumber", "Número de série", SerialNumber, next.SerialNumber);
        Compare("brand", "Marca", Brand, next.Brand);
        Compare("model", "Modelo", Model, next.Model);
        Compare("organizationId", "Órgão responsável", OrganizationId.ToString(), next.OrganizationId.ToString());
        changes.AddRange(Location.Changes(Location, next.Location));
        Compare("baseId", "Base operacional", BaseId?.ToString(), next.BaseId?.ToString());
        changes.AddRange(type.DescribeChanges(Attributes, next.Attributes));

        Apply(next);
        return changes;
    }

    /// <summary>Situações de destino a partir da atual, com a indicação de justificativa, já filtradas pela permissão (D-59).</summary>
    public IReadOnlyList<StatusChangeOption> AllowedStatusChanges(bool canReactivate) =>
        EquipmentStatusTransitions.TargetsFrom(Status)
            .Where(target => canReactivate || !EquipmentStatusTransitions.IsReactivation(Status, target))
            .Select(target => new StatusChangeOption(target, EquipmentStatusTransitions.RequiresJustification(Status, target)))
            .ToArray();

    /// <summary>
    /// Muda a situação (US-04), com as verificações 2 a 5 de plan 15.3, nessa ordem; a verificação 1 (cadastro
    /// ativo, RN-35) entra na Fase 3. Efeitos: registro no histórico; RN-07 ao entrar em Mau funcionamento;
    /// cancelamento das manutenções abertas ao entrar em Desativado ou Furtado (RN-11).
    /// </summary>
    /// <param name="canReactivate">Se o ator tem <c>resources.reactivate</c> (D-47); a carga inicial passa <c>true</c>.</param>
    /// <exception cref="InventoryRuleException">Mesma situação ou transição fora da RN-05.</exception>
    /// <exception cref="ReactivationNotAllowedException">Reativação sem permissão.</exception>
    /// <exception cref="DomainValidationException">Justificativa ausente ou inválida (RN-06), no campo <c>justification</c>.</exception>
    public void ChangeStatus(
        EquipmentStatus target, string? justification, bool canReactivate, DateTimeOffset at, string by)
    {
        var author = InventoryAuthor.Valid(by);

        if (target == Status)
        {
            throw new InventoryRuleException("O equipamento já está nessa situação.");
        }

        if (!EquipmentStatusTransitions.IsAllowed(Status, target))
        {
            throw new InventoryRuleException(
                $"Não é permitido passar de {EquipmentStatusLabels.For(Status)} para {EquipmentStatusLabels.For(target)}.");
        }

        if (EquipmentStatusTransitions.IsReactivation(Status, target) && !canReactivate)
        {
            throw new ReactivationNotAllowedException();
        }

        var validJustification = ValidJustification(justification, EquipmentStatusTransitions.RequiresJustification(Status, target));
        ApplyStatus(target, StatusChangeOrigin.Manual, validJustification, maintenanceId: null, at, author);
    }

    /// <summary>
    /// Registra uma manutenção (US-05), com as regras de plan 15.4: campos por tipo (RN-08, erros de campo antes das
    /// regras sem campo), nenhuma em Desativado ou Furtado (RN-11) e uma só imediata aberta (RN-07). A verificação
    /// de cadastro ativo (RN-35) entra na Fase 3.
    /// </summary>
    /// <param name="today">Data de hoje no fuso do município (D-54).</param>
    public Maintenance RegisterMaintenance(
        MaintenanceKind kind,
        DateOnly? dueDate,
        int? periodicityDays,
        string? description,
        DateOnly today,
        DateTimeOffset at,
        string by)
    {
        var author = InventoryAuthor.Valid(by);
        var errors = new FieldErrors();

        if (!Enum.IsDefined(kind))
        {
            errors.Add("kind", "Escolha o tipo da manutenção.");
        }

        switch (kind)
        {
            case MaintenanceKind.Immediate:
                if (dueDate is not null)
                {
                    errors.Add("dueDate", "A manutenção imediata não tem data prevista.");
                }

                if (periodicityDays is not null)
                {
                    errors.Add("periodicityDays", "Só a manutenção periódica tem periodicidade.");
                }

                break;

            case MaintenanceKind.Scheduled:
                if (dueDate is null)
                {
                    errors.Add("dueDate", "Informe a data prevista.");
                }
                else if (dueDate < today)
                {
                    errors.Add("dueDate", "A data prevista não pode ser anterior a hoje.");
                }

                if (periodicityDays is not null)
                {
                    errors.Add("periodicityDays", "Só a manutenção periódica tem periodicidade.");
                }

                break;

            case MaintenanceKind.Periodic:
                if (dueDate is null)
                {
                    errors.Add("dueDate", "Informe a data prevista.");
                }

                if (periodicityDays is null)
                {
                    errors.Add("periodicityDays", "Informe a periodicidade, em dias.");
                }
                else if (periodicityDays is < MinPeriodicityDays or > MaxPeriodicityDays)
                {
                    errors.Add("periodicityDays", $"A periodicidade deve estar entre {MinPeriodicityDays} e 3.650 dias.");
                }

                break;
        }

        var validDescription = errors.OptionalText("description", description, DescriptionMaxLength, "a descrição");
        errors.ThrowIfAny();

        if (Status is EquipmentStatus.Deactivated or EquipmentStatus.Stolen)
        {
            throw new InventoryRuleException("Equipamento desativado ou furtado não recebe manutenção.");
        }

        if (kind == MaintenanceKind.Immediate && HasOpenImmediateMaintenance)
        {
            throw new InventoryRuleException("Já existe uma manutenção imediata aberta.");
        }

        var maintenance = Maintenance.Open(
            Id, kind, dueDate, periodicityDays, validDescription, MaintenanceOrigin.Manual, null, at, author);
        _maintenances.Add(maintenance);
        return maintenance;
    }

    /// <summary>
    /// Conclui uma manutenção aberta (US-05; plan 15.4): grava a conclusão; aplica a RN-11 à situação; e, se ela
    /// era periódica, programa a próxima para a data da conclusão no fuso do município + periodicidade, com sucesso ou
    /// sem (RN-09). Conferir se a manutenção existe (404) é da Aplicação; aqui, uma que não esteja entre as abertas é
    /// recusada com 409.
    /// </summary>
    /// <param name="completedAt">Instante da conclusão: não futuro nem anterior à abertura.</param>
    /// <param name="timeZone">Fuso do município, para a data da conclusão (RN-09; D-54).</param>
    public MaintenanceCompletion CompleteMaintenance(
        Guid maintenanceId,
        MaintenanceResult result,
        DateTimeOffset completedAt,
        string? notes,
        TimeZoneInfo timeZone,
        DateTimeOffset at,
        string by)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var author = InventoryAuthor.Valid(by);

        var maintenance = _maintenances.FirstOrDefault(candidate => candidate.Id == maintenanceId && candidate.IsOpen)
            ?? throw new InventoryRuleException("A manutenção não está aberta.");

        var errors = new FieldErrors();

        if (!Enum.IsDefined(result))
        {
            errors.Add("result", "Informe se a manutenção teve sucesso.");
        }

        if (completedAt > at)
        {
            errors.Add("completedAt", "A conclusão não pode estar no futuro.");
        }
        else if (completedAt < maintenance.OpenedAt)
        {
            errors.Add("completedAt", "A conclusão não pode ser anterior à abertura da manutenção.");
        }

        var validNotes = errors.OptionalText("notes", notes, CompletionNotesMaxLength, "as observações");
        errors.ThrowIfAny();

        maintenance.Complete(result, completedAt, validNotes, author);
        ApplyCompletionResult(maintenance, result, at, author);

        Maintenance? next = null;
        if (maintenance.Kind == MaintenanceKind.Periodic)
        {
            var completedOn = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(completedAt, timeZone).DateTime);
            next = Maintenance.Open(
                Id,
                MaintenanceKind.Periodic,
                completedOn.AddDays(maintenance.PeriodicityDays!.Value),
                maintenance.PeriodicityDays,
                maintenance.Description,
                MaintenanceOrigin.PeriodicRescheduling,
                maintenance.Id,
                at,
                author);
            _maintenances.Add(next);
        }

        return new MaintenanceCompletion(maintenance, Status, next);
    }

    /// <summary>Necessidade de manutenção a partir das manutenções abertas (RN-10; D-54).</summary>
    public MaintenanceNeed CalculateMaintenanceNeed(DateOnly today, int upcomingWindowDays) =>
        MaintenanceNeedCalculator.Calculate(_maintenances, today, upcomingWindowDays);

    /// <summary>RN-11: com sucesso volta a Operacional; sem sucesso passa para, ou permanece em, Mau funcionamento.</summary>
    private void ApplyCompletionResult(Maintenance maintenance, MaintenanceResult result, DateTimeOffset at, string author)
    {
        if (Status == EquipmentStatus.NotInstalled)
        {
            return;
        }

        if (result == MaintenanceResult.Success)
        {
            if (Status is EquipmentStatus.Malfunction or EquipmentStatus.UnderMaintenance)
            {
                ApplyStatus(EquipmentStatus.Operational, StatusChangeOrigin.MaintenanceCompletion, null, maintenance.Id, at, author);
            }

            return;
        }

        if (Status is EquipmentStatus.Operational or EquipmentStatus.UnderMaintenance)
        {
            ApplyStatus(EquipmentStatus.Malfunction, StatusChangeOrigin.MaintenanceCompletion, null, maintenance.Id, at, author);
        }
        else if (Status == EquipmentStatus.Malfunction && !HasOpenImmediateMaintenance)
        {
            OpenAutomaticImmediate(at, author);
        }
    }

    private void ApplyStatus(
        EquipmentStatus target, StatusChangeOrigin origin, string? justification, Guid? maintenanceId, DateTimeOffset at, string author)
    {
        _statusChanges.Add(EquipmentStatusChange.Record(Id, Status, target, origin, justification, maintenanceId, at, author));
        Status = target;
        StatusChangedAt = at.ToUniversalTime();

        if (target == EquipmentStatus.Malfunction && !HasOpenImmediateMaintenance)
        {
            OpenAutomaticImmediate(at, author);
        }

        if (target is EquipmentStatus.Deactivated or EquipmentStatus.Stolen)
        {
            var reason = $"Cancelada automaticamente: equipamento {EquipmentStatusLabels.For(target).ToLowerInvariant()}.";
            foreach (var open in _maintenances.Where(maintenance => maintenance.IsOpen).ToArray())
            {
                open.Cancel(reason, at, author);
            }
        }
    }

    /// <summary>RN-07: imediata aberta automaticamente ao entrar em Mau funcionamento (ou ao seguir nele, RN-11).</summary>
    private void OpenAutomaticImmediate(DateTimeOffset at, string author) =>
        _maintenances.Add(Maintenance.Open(
            Id,
            MaintenanceKind.Immediate,
            null,
            null,
            "Aberta automaticamente: equipamento em mau funcionamento.",
            MaintenanceOrigin.AutomaticOnMalfunction,
            null,
            at,
            author));

    private static string? ValidJustification(string? justification, bool required)
    {
        var trimmed = string.IsNullOrWhiteSpace(justification) ? null : justification.Trim();

        if ((required && trimmed is null) || trimmed?.Length is < JustificationMinLength or > JustificationMaxLength)
        {
            throw new DomainValidationException(
                "justification",
                $"Informe a justificativa, com {JustificationMinLength} a {JustificationMaxLength} caracteres.");
        }

        return trimmed;
    }

    private static Registration Validate(
        FieldErrors errors,
        EquipmentType type,
        string serialNumber,
        string brand,
        string model,
        Organization? organization,
        Location? location,
        OperationalBase? operationalBase,
        IReadOnlyDictionary<string, object?>? attributes,
        Equipment? current)
    {
        var validSerial = errors.RequiredText("serialNumber", serialNumber, 1, SerialNumberMaxLength, "o número de série");
        var validBrand = errors.RequiredText("brand", brand, 1, BrandMaxLength, "a marca");
        var validModel = errors.RequiredText("model", model, 1, ModelMaxLength, "o modelo");

        if (organization is null)
        {
            errors.Add("organizationId", "Escolha o órgão responsável.");
        }
        else if (!organization.IsActive && organization.Id != current?.OrganizationId)
        {
            errors.Add("organizationId", "O órgão escolhido está inativo.");
        }

        if (type.Category == EquipmentCategory.Fixed)
        {
            if (location is null)
            {
                errors.Add("location", "Informe a localização do equipamento fixo.");
            }

            if (operationalBase is not null)
            {
                errors.Add("baseId", "Equipamento fixo não tem base operacional.");
            }
        }
        else
        {
            if (location is not null)
            {
                errors.Add("location", "Equipamento móvel não tem localização fixa: a posição vem da telemetria.");
            }

            if (operationalBase is null)
            {
                errors.Add("baseId", "Escolha a base operacional do equipamento móvel.");
            }
            else if (!operationalBase.IsActive && operationalBase.Id != current?.BaseId)
            {
                errors.Add("baseId", "A base escolhida está inativa.");
            }
        }

        var validAttributes = AttributeValues.Empty;
        try
        {
            validAttributes = type.ValidateAttributes(attributes);
        }
        catch (DomainValidationException exception)
        {
            errors.AddAll(exception);
        }

        errors.ThrowIfAny();
        return new Registration(
            validSerial, validBrand, validModel, organization!.Id, location, operationalBase?.Id, validAttributes);
    }

    private void Apply(Registration registration)
    {
        SerialNumber = registration.SerialNumber;
        Brand = registration.Brand;
        Model = registration.Model;
        SerialKey = TextNormalization.ToKey(registration.SerialNumber);
        BrandKey = TextNormalization.ToKey(registration.Brand);
        OrganizationId = registration.OrganizationId;
        Location = registration.Location;
        BaseId = registration.BaseId;
        Attributes = registration.Attributes;
    }

    private sealed record Registration(
        string SerialNumber,
        string Brand,
        string Model,
        Guid OrganizationId,
        Location? Location,
        Guid? BaseId,
        AttributeValues Attributes);
}

/// <summary>Resultado da conclusão (plan 15.14): a manutenção concluída, a situação final e a próxima periódica (RN-09).</summary>
public sealed record MaintenanceCompletion(Maintenance Completed, EquipmentStatus Status, Maintenance? NextPeriodic);
