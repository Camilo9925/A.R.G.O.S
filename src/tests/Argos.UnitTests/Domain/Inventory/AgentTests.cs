using System.Reflection;
using Argos.Domain.Common;
using Argos.Domain.Inventory;
using Argos.Domain.Inventory.Agents;
using static Argos.UnitTests.Domain.Inventory.AgentTestData;
using static Argos.UnitTests.Domain.Inventory.InventoryTestData;

namespace Argos.UnitTests.Domain.Inventory;

/// <summary>
/// Agente: cadastro, edição, situação e inativação (RF-013 a RF-015, US-06, US-09, RN-12, RN-22, RN-25, RN-35; D-49;
/// plan 15.2.9).
/// </summary>
public sealed class AgentTests
{
    private static readonly DateTimeOffset Later = Now.AddHours(3);

    public static TheoryData<AgentStatus, AgentStatus> AllCombinations()
    {
        var data = new TheoryData<AgentStatus, AgentStatus>();
        foreach (var from in Enum.GetValues<AgentStatus>())
        {
            foreach (var to in Enum.GetValues<AgentStatus>())
            {
                data.Add(from, to);
            }
        }

        return data;
    }

    [Fact]
    public void Statuses_AreTheFiveOfTheRule_RN12()
    {
        Assert.Equal(
            ["Em serviço", "Em atendimento", "Em pausa", "Fora de serviço", "Afastado"],
            Enum.GetValues<AgentStatus>().Select(AgentStatusLabels.For));
    }

    [Fact]
    public void Register_StartsOffDutyWithInitialRecordWithoutPreviousStatus_RN12_US06()
    {
        var agent = NewAgent();

        Assert.Equal(AgentStatus.OffDuty, agent.Status);
        Assert.Equal(AgentStatus.OffDuty, Agent.InitialStatus);
        Assert.True(agent.IsActive);
        var initial = Assert.Single(agent.StatusChanges);
        Assert.Equal(agent.Id, initial.AgentId);
        Assert.Null(initial.FromStatus);
        Assert.Equal(AgentStatus.OffDuty, initial.ToStatus);
        Assert.Equal(AgentStatusChangeOrigin.Registration, initial.Origin);
        Assert.Equal(Now, initial.OccurredAt);
        Assert.Equal(Author, initial.Author);
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void ChangeStatus_AnyDifferentStatusWithoutJustification_SameIsRefused_RN12(AgentStatus from, AgentStatus to)
    {
        var agent = AgentIn(from);
        var recordsBefore = agent.StatusChanges.Count;

        if (from == to)
        {
            var exception = Assert.Throws<InventoryRuleException>(() => agent.ChangeStatus(to, Later, "operador"));

            Assert.Equal("O agente já está nessa situação.", exception.Message);
            Assert.Equal(recordsBefore, agent.StatusChanges.Count);
        }
        else
        {
            agent.ChangeStatus(to, Later, "operador");

            Assert.Equal(to, agent.Status);
            var change = agent.StatusChanges.Last();
            Assert.Equal(from, change.FromStatus);
            Assert.Equal(to, change.ToStatus);
            Assert.Equal(AgentStatusChangeOrigin.Manual, change.Origin);
            Assert.Equal(Later, change.OccurredAt);
            Assert.Equal("operador", change.Author);
        }
    }

    [Fact]
    public void ChangeStatus_UnknownStatus_IsRejectedInStatusField_RN12()
    {
        var agent = NewAgent();

        var exception = Assert.Throws<DomainValidationException>(() => agent.ChangeStatus((AgentStatus)99, Later, Author));

        Assert.Equal(["status"], exception.Errors.Keys);
    }

    [Fact]
    public void StatusChange_HasNoJustification_RN12()
    {
        Assert.Equal(
            ["AgentId", "Author", "FromStatus", "Id", "OccurredAt", "Origin", "ToStatus"],
            PublicPropertiesOf<AgentStatusChange>());
    }

    [Fact]
    public void Register_WithoutOrganizationOrWithInactive_IsRejected_RN22()
    {
        var inactive = NewOrganization("Guarda Civil Municipal", "GCM");
        var operationalBase = NewBase(inactive);
        inactive.Inactivate(Now, Author);

        Assert.Equal(
            ["organizationId"],
            Assert.Throws<DomainValidationException>(() =>
                NewAgent(organization: inactive, operationalBase: operationalBase)).Errors.Keys);
        Assert.Equal(
            ["organizationId"],
            Assert.Throws<DomainValidationException>(() => Agent.Register(
                "Agente Fictício", "Falcão", "GCM-1", AgentRole.MunicipalGuard, organization: null, null,
                operationalBase, Now, Author)).Errors.Keys);
    }

    [Fact]
    public void UpdateRegistration_KeepsOwnOrganizationEvenIfInactive_RN22()
    {
        var organization = NewOrganization("Guarda Civil Municipal", "GCM");
        var operationalBase = NewBase(organization);
        var agent = NewAgent(organization: organization, operationalBase: operationalBase);
        organization.Inactivate(Now, Author);

        var changes = agent.UpdateRegistration(
            agent.FullName, "Gavião", agent.RegistrationNumber, agent.Role, organization, agent.Rank, operationalBase);

        Assert.Equal(["displayName"], changes.Select(change => change.Field));
    }

    [Fact]
    public void UpdateRegistration_ToAnotherInactiveOrganization_IsRejected_RN22()
    {
        var agent = NewAgent();
        var other = NewOrganization("Polícia Militar", "PM");
        other.Inactivate(Now, Author);

        var exception = Assert.Throws<DomainValidationException>(() => agent.UpdateRegistration(
            agent.FullName, agent.DisplayName, agent.RegistrationNumber, agent.Role, other, agent.Rank, NewBase()));

        Assert.Equal(["organizationId"], exception.Errors.Keys);
    }

    [Fact]
    public void Register_AllFieldErrorsAtOnce_D46()
    {
        var exception = Assert.Throws<DomainValidationException>(() => Agent.Register(
            "ab", " ", "GCM 01", (AgentRole)99, NewOrganization(), new string('p', Agent.RankMaxLength + 1),
            operationalBase: null, Now, Author));

        Assert.Equal(
            ["baseId", "displayName", "fullName", "rank", "registrationNumber", "role"],
            exception.Errors.Keys.Order());
    }

    [Theory]
    [InlineData("GCM-0042")]
    [InlineData("12.345/6")]
    [InlineData("a")]
    [InlineData("ABCDEFGHIJ0123456789")]
    public void Register_RegistrationNumberInTheAllowedFormat_IsAccepted_US06(string registrationNumber)
    {
        Assert.Equal(registrationNumber, NewAgent(registrationNumber).RegistrationNumber);
    }

    [Theory]
    [InlineData("GCM 0042")]
    [InlineData("Matrícula")]
    [InlineData("GCM_0042")]
    [InlineData("ABCDEFGHIJ01234567890")]
    public void Register_RegistrationNumberOutOfTheFormat_IsRejected_US06(string registrationNumber)
    {
        var exception = Assert.Throws<DomainValidationException>(() => NewAgent(registrationNumber));

        Assert.Equal(["registrationNumber"], exception.Errors.Keys);
    }

    [Fact]
    public void Register_RegistrationKeyIgnoresCase_ForUniquenessPerOrganization_US06()
    {
        Assert.Equal(NewAgent("gcm-0042").RegistrationKey, NewAgent(" GCM-0042 ").RegistrationKey);
        Assert.Equal("GCM-0042", NewAgent("gcm-0042").RegistrationKey);
    }

    [Fact]
    public void Register_RankIsOptional_RF014()
    {
        Assert.Null(NewAgent(rank: "  ").Rank);
    }

    [Fact]
    public void Register_InactiveBase_IsRejected_RF014()
    {
        var organization = NewOrganization();
        var operationalBase = NewBase(organization);
        operationalBase.Inactivate(hasActiveDependents: false, Now, Author);

        var exception = Assert.Throws<DomainValidationException>(() =>
            NewAgent(organization: organization, operationalBase: operationalBase));

        Assert.Equal(["baseId"], exception.Errors.Keys);
    }

    [Fact]
    public void UpdateRegistration_ReturnsChangedFieldsInDisplayText_D49()
    {
        var agent = NewAgent();
        var organization = NewOrganization("Polícia Militar", "PM");
        var operationalBase = NewBase(organization);

        var changes = agent.UpdateRegistration(
            "Agente Fictício Quarenta e Três", "Falcão", "PM-77", AgentRole.MilitaryPolice, organization, null,
            operationalBase);

        Assert.Equal(
            ["fullName", "registrationNumber", "role", "organizationId", "rank", "baseId"],
            changes.Select(change => change.Field));
        Assert.Contains(new FieldChange("role", "Função", "Guarda municipal", "Policial militar"), changes);
        Assert.Contains(new FieldChange("rank", "Posto ou graduação", "Guarda de 2ª classe", null), changes);
        Assert.Equal("PM-77", agent.RegistrationKey);
        Assert.Equal(organization.Id, agent.OrganizationId);
    }

    [Fact]
    public void UpdateRegistration_WithoutChanges_ReturnsEmpty_D49()
    {
        var organization = NewOrganization();
        var operationalBase = NewBase(organization);
        var agent = NewAgent(organization: organization, operationalBase: operationalBase);

        var changes = agent.UpdateRegistration(
            agent.FullName, agent.DisplayName, agent.RegistrationNumber, agent.Role, organization, agent.Rank,
            operationalBase);

        Assert.Empty(changes);
    }

    // RN-25: lista fechada do que o agente guarda. Um campo novo exige mudar esta lista, o plan (15.2.9) e a revisão.
    [Fact]
    public void Agent_HasOnlyTheFieldsOfTheClosedList_RN25()
    {
        Assert.Equal(
            [
                "BaseId", "CreatedAt", "CreatedBy", "DeactivatedAt", "DeactivatedBy", "DisplayName", "FullName", "Id",
                "IsActive", "OrganizationId", "Rank", "RegistrationKey", "RegistrationNumber", "Role", "Status",
                "StatusChanges", "UpdatedAt", "UpdatedBy",
            ],
            PublicPropertiesOf<Agent>());
        Assert.Equal(
            ["_statusChanges"],
            typeof(Agent).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(field => !field.Name.Contains("k__BackingField", StringComparison.Ordinal))
                .Select(field => field.Name));
    }

    [Theory]
    [InlineData("cpf")]
    [InlineData("document")]
    [InlineData("address")]
    [InlineData("phone")]
    [InlineData("email")]
    [InlineData("birth")]
    [InlineData("health")]
    public void Agent_HasNoPersonalDataBeyondTheMinimum_RN25(string forbidden)
    {
        Assert.DoesNotContain(
            PublicPropertiesOf<Agent>(),
            name => name.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(AgentStatus.OffDuty, true)]
    [InlineData(AgentStatus.OnLeave, true)]
    [InlineData(AgentStatus.OnDuty, false)]
    [InlineData(AgentStatus.Responding, false)]
    [InlineData(AgentStatus.OnBreak, false)]
    public void Inactivate_OnlyOffDutyOrOnLeave_RN35(AgentStatus status, bool accepted)
    {
        var agent = AgentIn(status);

        if (accepted)
        {
            agent.Inactivate(null, Later, "admin");

            Assert.False(agent.IsActive);
            Assert.Equal(Later, agent.DeactivatedAt);
            Assert.Equal("admin", agent.DeactivatedBy);
            Assert.Equal(status, agent.Status);
        }
        else
        {
            var exception = Assert.Throws<InventoryRuleException>(() => agent.Inactivate(null, Later, "admin"));

            Assert.Equal(
                $"Agente na situação {AgentStatusLabels.For(status)} não pode ser inativado. A inativação só é possível nas "
                + "situações Fora de serviço ou Afastado.",
                exception.Message);
            Assert.True(agent.IsActive);
        }
    }

    [Fact]
    public void Inactivate_ReturnsInactivatedChangeWithReason_RN35_D49()
    {
        var agent = AgentIn(AgentStatus.OnLeave);

        var change = agent.Inactivate("  Desligado do órgão  ", Later, "admin");

        Assert.Equal(InventoryChangeKind.Inactivated, change.Kind);
        Assert.Equal(InventoryEntityType.Agent, change.EntityType);
        Assert.Equal(agent.Id, change.EntityId);
        Assert.Equal("Desligado do órgão", change.Reason);
        Assert.Equal(Later, change.OccurredAt);
    }

    [Fact]
    public void Inactivate_ReasonTooLong_IsRejectedInReasonField_RN35()
    {
        var agent = NewAgent();

        var exception = Assert.Throws<DomainValidationException>(() =>
            agent.Inactivate(new string('m', Agent.InactivationReasonMaxLength + 1), Later, "admin"));

        Assert.Equal(["reason"], exception.Errors.Keys);
        Assert.True(agent.IsActive);
    }

    [Fact]
    public void Inactivate_Twice_IsRefused_RN35()
    {
        var agent = NewAgent();
        agent.Inactivate(null, Now, "admin");

        var exception = Assert.Throws<InventoryRuleException>(() => agent.Inactivate(null, Later, "outro"));

        Assert.Equal("O agente já está inativo.", exception.Message);
        Assert.Equal(Now, agent.DeactivatedAt);
    }

    [Fact]
    public void ChangeStatus_WhenInactive_IsRefused_RN35()
    {
        var agent = NewAgent();
        agent.Inactivate(null, Now, "admin");

        var exception = Assert.Throws<InventoryRuleException>(() => agent.ChangeStatus(AgentStatus.OnDuty, Later, Author));

        Assert.Equal("Agente inativo não pode mudar de situação.", exception.Message);
        Assert.Equal(AgentStatus.OffDuty, agent.Status);
        Assert.Single(agent.StatusChanges);
    }

    [Fact]
    public void UpdateRegistration_WhenInactive_IsRefused_RN35()
    {
        var organization = NewOrganization();
        var operationalBase = NewBase(organization);
        var agent = NewAgent(organization: organization, operationalBase: operationalBase);
        agent.Inactivate(null, Now, "admin");

        var exception = Assert.Throws<InventoryRuleException>(() => agent.UpdateRegistration(
            agent.FullName, "Gavião", agent.RegistrationNumber, agent.Role, organization, agent.Rank, operationalBase));

        Assert.Equal("Agente inativo não pode ser editado.", exception.Message);
        Assert.Equal("Falcão", agent.DisplayName);
    }

    private static string[] PublicPropertiesOf<T>() =>
        typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
}
