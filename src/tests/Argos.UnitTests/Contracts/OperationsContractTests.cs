using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Argos.Contracts.Inventory;
using Argos.Contracts.Operations;
using Argos.Contracts.Telemetry;
using D = Argos.Domain;

namespace Argos.UnitTests.Contracts;

/// <summary>
/// Contratos da Telemetria e da Central (D-61; plan 16.4, 16.10, 16.11): paridade dos enums, mensagens de validação do
/// lote e da resolução, e o recorte de dados pessoais nos detalhes de agente (RN-25).
/// </summary>
public sealed class OperationsContractTests
{
    private static readonly (Type Contract, Type Domain)[] Mirrors =
    [
        (typeof(ResourceKind), typeof(D.Telemetry.ResourceKind)),
        (typeof(AlertCause), typeof(D.Operations.Alerts.AlertCause)),
        (typeof(AlertSeverity), typeof(D.Operations.Alerts.AlertSeverity)),
        (typeof(AlertState), typeof(D.Operations.Alerts.AlertState)),
        (typeof(AlertAction), typeof(D.Operations.Alerts.AlertAction)),
        (typeof(DutyShift), typeof(D.Operations.DutyShift)),
    ];

    /// <summary>Só do contrato (plan 16.10): a fonte da posição do marcador e o tipo do aviso de tempo real.</summary>
    private static readonly Type[] ContractOnly = [typeof(PositionSource), typeof(OperationsChangeKind)];

    public static TheoryData<Type, Type> Pairs()
    {
        var data = new TheoryData<Type, Type>();
        foreach (var (contract, domain) in Mirrors)
        {
            data.Add(contract, domain);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void ContractEnum_HasSameNamesAndOrderAsDomainEnum_D61(Type contract, Type domain)
    {
        Assert.Equal(Enum.GetNames(domain), Enum.GetNames(contract));
    }

    [Fact]
    public void EveryContractEnumOfOperations_IsCoveredByParityTest_D61()
    {
        var covered = Mirrors.Select(mirror => mirror.Contract).Concat(ContractOnly).ToHashSet();
        var contractEnums = typeof(ResourceKind).Assembly.GetTypes()
            .Where(type => type.IsEnum && type.Namespace is "Argos.Contracts.Operations" or "Argos.Contracts.Telemetry");

        Assert.All(contractEnums, contractEnum => Assert.Contains(contractEnum, covered));
    }

    [Fact]
    public void PositionItem_InvalidLatitude_HasTheMessageOfTheBatch_D71()
    {
        var item = new TelemetryPositionItem
        {
            ResourceKind = ResourceKind.Equipment,
            ResourceId = Guid.NewGuid(),
            Latitude = 91,
            Longitude = -181,
        };

        var errors = Validate(item);

        Assert.Equal(
            [
                ("Latitude", "A latitude deve estar entre -90 e 90."),
                ("Longitude", "A longitude deve estar entre -180 e 180."),
                ("RecordedAt", "Informe o instante da leitura."),
            ],
            errors.Select(error => (error.MemberNames.Single(), error.ErrorMessage!)).Order());
    }

    [Fact]
    public void BatchRequest_MoreThan1000Items_HasTheMessageOfTheBatch_D71()
    {
        var request = new TelemetryBatchRequest
        {
            SentAt = DateTimeOffset.UnixEpoch,
            IsSimulated = true,
            Heartbeats = Enumerable.Range(0, 1001)
                .Select(_ => new TelemetryHeartbeatItem { EquipmentId = Guid.NewGuid(), RecordedAt = DateTimeOffset.UnixEpoch })
                .ToList(),
        };

        var error = Assert.Single(Validate(request));

        Assert.Equal(("Heartbeats", "O lote pode ter no máximo 1.000 itens em cada lista."), (error.MemberNames.Single(), error.ErrorMessage));
    }

    [Fact]
    public void ResolveAlertRequest_NotesRequiredUpTo500_RN20()
    {
        Assert.Equal("Informe a observação da resolução.", Assert.Single(Validate(new ResolveAlertRequest())).ErrorMessage);
        Assert.Equal(
            "A observação deve ter no máximo 500 caracteres.",
            Assert.Single(Validate(new ResolveAlertRequest { Notes = new string('x', 501) })).ErrorMessage);
        Assert.Empty(Validate(new ResolveAlertRequest { Notes = new string('x', 500) }));
    }

    [Fact]
    public void OperationsAgentDetails_HasNoFullNameNorRegistration_RN25()
    {
        var names = typeof(OperationsAgentDetails).GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(property => property.Name);

        Assert.DoesNotContain("FullName", names);
        Assert.DoesNotContain("RegistrationNumber", names);
        Assert.Contains("DisplayName", names);
    }

    [Fact]
    public void AgentStatusReport_UsesTheAgentStatusOfTheInventory_RN12()
    {
        Assert.Equal(typeof(AgentStatus?), typeof(AgentStatusReportItem).GetProperty(nameof(AgentStatusReportItem.Status))!.PropertyType);
    }

    private static List<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results;
    }
}
