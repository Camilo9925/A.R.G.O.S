using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Argos.Contracts.Common;
using Argos.Contracts.Inventory;

namespace Argos.UnitTests.Contracts;

/// <summary>
/// Contratos de agentes, vínculos e inativação (RF-010, RF-013, RF-014, US-06, US-09, RN-25, RN-35; D-61; plan 15.8,
/// 15.14).
/// </summary>
public sealed class AgentContractTests
{
    // RN-25: lista fechada do que o cadastro de agente pede (RF-014). Um campo novo exige mudar esta lista, o plan
    // (15.2.9) e a revisão. A versão da edição é controle de concorrência (D-60), não dado pessoal.
    private static readonly string[] RegistrationFields =
        ["BaseId", "DisplayName", "FullName", "OrganizationId", "Rank", "RegistrationNumber", "Role"];

    [Fact]
    public void CreateAgentRequest_HasOnlyTheFieldsOfTheClosedList_RN25()
    {
        Assert.Equal(RegistrationFields, PublicPropertiesOf<CreateAgentRequest>());
    }

    [Fact]
    public void UpdateAgentRequest_HasOnlyTheClosedListAndTheVersion_RN25_D60()
    {
        Assert.Equal(RegistrationFields.Append("Version").Order(StringComparer.Ordinal), PublicPropertiesOf<UpdateAgentRequest>());
    }

    public static TheoryData<Type, string> AgentContractsAndPersonalData()
    {
        var data = new TheoryData<Type, string>();
        Type[] contracts = [typeof(CreateAgentRequest), typeof(UpdateAgentRequest), typeof(AgentSummary), typeof(AgentDetails)];
        foreach (var contract in contracts)
        {
            foreach (var forbidden in new[] { "cpf", "document", "address", "phone", "email", "birth", "health" })
            {
                data.Add(contract, forbidden);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AgentContractsAndPersonalData))]
    public void AgentContracts_HaveNoPersonalDataBeyondTheMinimum_RN25(Type contract, string forbidden)
    {
        Assert.DoesNotContain(
            contract.GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(property => property.Name),
            name => name.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateAgentRequest_Empty_ListsTheRequiredFieldsInPortuguese_US06()
    {
        var errors = Validate(new CreateAgentRequest());

        Assert.Equal(
            ["BaseId", "DisplayName", "FullName", "OrganizationId", "RegistrationNumber", "Role"],
            errors.SelectMany(error => error.MemberNames).Order(StringComparer.Ordinal));
        Assert.Contains(errors, error => error.ErrorMessage == "Informe a matrícula.");
    }

    [Fact]
    public void InactivationRequest_ReasonIsOptionalUpTo500Characters_RN35()
    {
        Assert.Empty(Validate(new InactivationRequest()));
        Assert.Empty(Validate(new InactivationRequest { Reason = new string('x', 500) }));

        var error = Assert.Single(Validate(new InactivationRequest { Reason = new string('x', 501) }));
        Assert.Equal("O motivo deve ter no máximo 500 caracteres.", error.ErrorMessage);
    }

    [Fact]
    public void Searches_ShowOnlyActiveByDefault_US09()
    {
        Assert.Equal(ActivityFilter.Active, new EquipmentSearchQuery().Activity);
        Assert.Equal(ActivityFilter.Active, new AgentSearchQuery().Activity);
        Assert.Equal(["Active", "Inactive", "All"], Enum.GetNames<ActivityFilter>());
    }

    [Fact]
    public void Contracts_HaveNoProjectOrPackageDependencies()
    {
        var references = typeof(AgentDetails).Assembly.GetReferencedAssemblies().Select(reference => reference.Name!);

        Assert.All(references, name => Assert.True(
            name.StartsWith("System", StringComparison.Ordinal) || name is "netstandard" or "mscorlib",
            $"Argos.Contracts referencia {name}."));
    }

    private static IEnumerable<string> PublicPropertiesOf<T>() =>
        typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal);

    private static List<ValidationResult> Validate(object request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }
}
