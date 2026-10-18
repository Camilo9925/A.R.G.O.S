using System.Reflection;
using Argos.Domain.Common;
using Argos.Domain.Inventory;
using Argos.Domain.Inventory.Types;
using ContractIcons = Argos.Contracts.Inventory.EquipmentIcons;

namespace Argos.UnitTests.Domain.Inventory;

/// <summary>
/// Tipos de equipamento, características e catálogo inicial (RF-006, US-02; D-48, D-63; plan 15.2.5, 15.5).
/// </summary>
public sealed class EquipmentTypeTests
{
    private static readonly IReadOnlyList<EquipmentType> Catalog = EquipmentTypeCatalog.CreateAll();

    private static EquipmentType Type(string key) => Catalog.Single(type => type.Key == key);

    private static Dictionary<string, object?> RadarValues(string modality, object? maxSpeed = null) => new()
    {
        ["modality"] = modality,
        ["maxSpeedKmh"] = maxSpeed,
        ["monitoredLanes"] = 2,
    };

    [Fact]
    public void Radar_SpeedModalityWithoutMaxSpeed_ErrorOnlyInThatField_US02()
    {
        var radar = Type(EquipmentTypeCatalog.TrafficRadar);

        var exception = Assert.Throws<DomainValidationException>(() => radar.ValidateAttributes(RadarValues("speed")));

        var error = Assert.Single(exception.Errors);
        Assert.Equal("attributes.maxSpeedKmh", error.Key);
        Assert.Equal(["Obrigatório quando modalidade é Velocidade."], error.Value);
    }

    [Theory]
    [InlineData("red-light")]
    [InlineData("forbidden-turn")]
    [InlineData("forbidden-u-turn")]
    [InlineData("crosswalk-stop")]
    [InlineData("other")]
    public void Radar_OtherModalitiesWithoutMaxSpeed_AreAccepted_US02(string modality)
    {
        var values = Type(EquipmentTypeCatalog.TrafficRadar).ValidateAttributes(RadarValues(modality));

        Assert.False(values.ContainsKey("maxSpeedKmh"));
        Assert.Equal(modality, values["modality"]);
    }

    [Fact]
    public void Radar_SpeedModalityWithMaxSpeed_IsAcceptedAndTyped_US02()
    {
        var values = Type(EquipmentTypeCatalog.TrafficRadar).ValidateAttributes(RadarValues("speed", 60));

        Assert.Equal(60L, values["maxSpeedKmh"]);
        Assert.Equal(2L, values["monitoredLanes"]);
    }

    [Fact]
    public void Catalog_HasTheTenTypesOfPlan155_AllPassingDefinitionValidation()
    {
        Assert.Equal(10, Catalog.Count);
        Assert.Equal(Catalog.Count, Catalog.Select(type => type.Key).Distinct().Count());
        Assert.Equal(5, Catalog.Count(type => type.Category == EquipmentCategory.Fixed));
        Assert.Equal(5, Catalog.Count(type => type.Category == EquipmentCategory.Mobile));
        Assert.All(Catalog, type => Assert.True(type.IsBuiltIn && type.IsActive));
        Assert.All(Catalog, type => Assert.Contains(type.Icon, EquipmentIcons.All));
    }

    [Fact]
    public void Catalog_OnlyMotorcycleAndPatrolVehicleAcceptCrew_D62()
    {
        Assert.Equal(
            [EquipmentTypeCatalog.PatrolVehicle, EquipmentTypeCatalog.TrafficAgentMotorcycle],
            Catalog.Where(type => type.CrewAssignable).Select(type => type.Key).Order());
    }

    [Fact]
    public void Catalog_CreateAll_ReturnsNewInstancesWithSameDefinitions()
    {
        var other = EquipmentTypeCatalog.CreateAll();

        Assert.Equal(Catalog.Select(type => type.Key), other.Select(type => type.Key));
        Assert.NotEqual(Catalog[0].Id, other[0].Id);
    }

    [Fact]
    public void Validate_AllErrorsAtOnce_EachInItsField_D46()
    {
        var radar = Type(EquipmentTypeCatalog.TrafficRadar);

        var exception = Assert.Throws<DomainValidationException>(() => radar.ValidateAttributes(new Dictionary<string, object?>
        {
            ["modality"] = "teleport",
            ["monitoredLanes"] = 9,
            ["roadDirection"] = new string('x', 121),
            ["color"] = "azul",
        }));

        Assert.Equal(
            ["attributes.color", "attributes.modality", "attributes.monitoredLanes", "attributes.roadDirection"],
            exception.Errors.Keys.Order());
        Assert.Equal(["Informe um valor entre 1 e 8."], exception.Errors["attributes.monitoredLanes"]);
        Assert.Equal(["Característica desconhecida para este tipo de equipamento."], exception.Errors["attributes.color"]);
    }

    [Fact]
    public void Validate_MissingRequired_IsReported_US02()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            Type(EquipmentTypeCatalog.TruckScale).ValidateAttributes(null));

        Assert.Equal(["attributes.maxCapacityTonnes", "attributes.weighingMode"], exception.Errors.Keys.Order());
        Assert.All(exception.Errors.Values, messages => Assert.Equal(["Campo obrigatório."], messages));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12.5")]
    [InlineData("1e3")]
    [InlineData(2.5)]
    [InlineData(true)]
    public void Validate_IntegerWithWrongFormat_IsRejected(object value)
    {
        var values = RadarValues("red-light");
        values["monitoredLanes"] = value;

        var exception = Assert.Throws<DomainValidationException>(() =>
            Type(EquipmentTypeCatalog.TrafficRadar).ValidateAttributes(values));

        Assert.Equal(["Informe um número inteiro."], exception.Errors["attributes.monitoredLanes"]);
    }

    [Fact]
    public void Validate_InvariantTextFromContract_IsAcceptedAndTyped_Plan1514()
    {
        var radar = Type(EquipmentTypeCatalog.TrafficRadar).ValidateAttributes(new Dictionary<string, object?>
        {
            ["modality"] = "speed",
            ["maxSpeedKmh"] = "60",
            ["monitoredLanes"] = " 2 ",
        });
        var scale = Type(EquipmentTypeCatalog.TruckScale).ValidateAttributes(new Dictionary<string, object?>
        {
            ["maxCapacityTonnes"] = "12.5",
            ["weighingMode"] = "static",
        });
        var kiosk = Type(EquipmentTypeCatalog.ParkingKiosk).ValidateAttributes(new Dictionary<string, object?>
        {
            ["paymentMethods"] = "pix, cash",
            ["issuesReceipt"] = "true",
        });

        Assert.Equal(60L, radar["maxSpeedKmh"]);
        Assert.Equal(2L, radar["monitoredLanes"]);
        Assert.Equal(12.5m, scale["maxCapacityTonnes"]);
        Assert.Equal(["cash", "pix"], (IReadOnlyList<string>)kiosk["paymentMethods"]);
        Assert.Equal(true, kiosk["issuesReceipt"]);
    }

    [Fact]
    public void Validate_InvariantTextAndTypedValues_GiveTheSameResult_Plan1514()
    {
        var kiosk = Type(EquipmentTypeCatalog.ParkingKiosk);

        Assert.Equal(
            kiosk.ValidateAttributes(new Dictionary<string, object?> { ["paymentMethods"] = new[] { "cash", "pix" }, ["issuesReceipt"] = false }),
            kiosk.ValidateAttributes(new Dictionary<string, object?> { ["paymentMethods"] = "cash,pix", ["issuesReceipt"] = "false" }));
    }

    [Fact]
    public void Validate_DecimalWithCommaOrText_IsRejectedInField_Plan1514()
    {
        foreach (var value in new[] { "12,5", "abc" })
        {
            var exception = Assert.Throws<DomainValidationException>(() =>
                Type(EquipmentTypeCatalog.TruckScale).ValidateAttributes(new Dictionary<string, object?>
                {
                    ["maxCapacityTonnes"] = value,
                    ["weighingMode"] = "static",
                }));

            Assert.Equal(["Informe um número."], exception.Errors["attributes.maxCapacityTonnes"]);
        }
    }

    [Theory]
    [InlineData("sim")]
    [InlineData("1")]
    [InlineData("abc")]
    public void Validate_BooleanOtherThanTrueOrFalse_IsRejectedInField_Plan1514(string value)
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            Type(EquipmentTypeCatalog.ParkingKiosk).ValidateAttributes(new Dictionary<string, object?>
            {
                ["paymentMethods"] = "cash",
                ["issuesReceipt"] = value,
            }));

        Assert.Equal(["Informe sim ou não."], exception.Errors["attributes.issuesReceipt"]);
    }

    [Fact]
    public void Validate_MultiOptionTextWithUnknownKey_IsRejectedInField_Plan1514()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            Type(EquipmentTypeCatalog.ParkingKiosk).ValidateAttributes(new Dictionary<string, object?>
            {
                ["paymentMethods"] = "cash,abc",
                ["issuesReceipt"] = "true",
            }));

        Assert.Equal(["Escolha só opções da lista."], exception.Errors["attributes.paymentMethods"]);
    }

    [Fact]
    public void Validate_DecimalWithMoreThanTwoPlaces_IsRejected()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            Type(EquipmentTypeCatalog.TruckScale).ValidateAttributes(new Dictionary<string, object?>
            {
                ["maxCapacityTonnes"] = 12.345m,
                ["weighingMode"] = "static",
            }));

        Assert.Equal(["Use no máximo duas casas decimais."], exception.Errors["attributes.maxCapacityTonnes"]);
    }

    [Fact]
    public void Validate_DecimalBelowRange_ShowsRangeWithCommaAndUnit()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            Type(EquipmentTypeCatalog.ParkingEnforcementVehicle).ValidateAttributes(new Dictionary<string, object?>
            {
                ["operationArea"] = "Centro",
                ["autonomyHours"] = 0.25,
                ["identification"] = "AV-01",
            }));

        Assert.Equal(["Informe um valor entre 0,5 e 48 h."], exception.Errors["attributes.autonomyHours"]);
    }

    [Fact]
    public void Validate_MultiOption_KeepsOptionOrderAndRejectsUnknown()
    {
        var kiosk = Type(EquipmentTypeCatalog.ParkingKiosk);

        var values = kiosk.ValidateAttributes(new Dictionary<string, object?>
        {
            ["paymentMethods"] = new[] { "pix", "cash", "pix" },
            ["issuesReceipt"] = false,
        });

        Assert.Equal(["cash", "pix"], (IReadOnlyList<string>)values["paymentMethods"]);

        var exception = Assert.Throws<DomainValidationException>(() => kiosk.ValidateAttributes(new Dictionary<string, object?>
        {
            ["paymentMethods"] = new[] { "pix", "bitcoin" },
            ["issuesReceipt"] = true,
        }));
        Assert.Equal(["attributes.paymentMethods"], exception.Errors.Keys);
    }

    [Fact]
    public void Validate_EmptyValues_CountAsNotInformed()
    {
        var kiosk = Type(EquipmentTypeCatalog.ParkingKiosk);

        var exception = Assert.Throws<DomainValidationException>(() => kiosk.ValidateAttributes(new Dictionary<string, object?>
        {
            ["paymentMethods"] = Array.Empty<string>(),
            ["issuesReceipt"] = true,
            ["coverageArea"] = "   ",
        }));

        Assert.Equal(["attributes.paymentMethods"], exception.Errors.Keys);
    }

    [Fact]
    public void Display_FormatsValuesInPortugueseWithUnits()
    {
        var radar = Type(EquipmentTypeCatalog.TrafficRadar);
        var kiosk = Type(EquipmentTypeCatalog.ParkingKiosk);
        var scale = Type(EquipmentTypeCatalog.TruckScale);

        Assert.Equal("80 km/h", radar.Display("maxSpeedKmh", 80L));
        Assert.Equal("Velocidade", radar.Display("modality", "speed"));
        Assert.Equal("Dinheiro, Pix", kiosk.Display("paymentMethods", (IReadOnlyList<string>)["cash", "pix"]));
        Assert.Equal("Não", kiosk.Display("issuesReceipt", false));
        Assert.Equal("12,5 t", scale.Display("maxCapacityTonnes", 12.5m));
    }

    [Fact]
    public void DescribeChanges_ListsChangedAttributesInDisplayText_D49()
    {
        var radar = Type(EquipmentTypeCatalog.TrafficRadar);
        var before = radar.ValidateAttributes(RadarValues("speed", 60));
        var after = radar.ValidateAttributes(RadarValues("red-light"));

        var changes = radar.DescribeChanges(before, after);

        Assert.Equal(
            [
                new FieldChange("attributes.modality", "Modalidade", "Velocidade", "Avanço de semáforo"),
                new FieldChange("attributes.maxSpeedKmh", "Velocidade máxima", "60 km/h", null),
            ],
            changes);
        Assert.Empty(radar.DescribeChanges(after, radar.ValidateAttributes(RadarValues("red-light"))));
    }

    [Fact]
    public void AttributeValues_EqualByValue_IncludingLists()
    {
        var kiosk = Type(EquipmentTypeCatalog.ParkingKiosk);
        Dictionary<string, object?> input = new() { ["paymentMethods"] = new[] { "pix", "debit" }, ["issuesReceipt"] = true };

        Assert.Equal(kiosk.ValidateAttributes(input), kiosk.ValidateAttributes(input));
    }

    [Fact]
    public void Definition_OptionsOnNonOptionType_IsRejected()
    {
        Assert.Throws<InventoryRuleException>(() => EquipmentTypeAttribute.Create(
            "level", "Nível", AttributeDataType.Integer, 1, options: [new("a", "A"), new("b", "B")]));
    }

    [Fact]
    public void Definition_MinGreaterThanMax_IsRejected()
    {
        Assert.Throws<InventoryRuleException>(() =>
            EquipmentTypeAttribute.Create("level", "Nível", AttributeDataType.Integer, 1, min: 10, max: 1));
    }

    [Fact]
    public void Definition_SingleOption_IsRejected()
    {
        Assert.Throws<InventoryRuleException>(() => EquipmentTypeAttribute.Create(
            "mode", "Modo", AttributeDataType.Option, 1, options: [new("only", "Única")]));
    }

    [Fact]
    public void Definition_ConditionPointingToMissingOption_IsRejected()
    {
        var mode = EquipmentTypeAttribute.Create(
            "mode", "Modo", AttributeDataType.Option, 1, options: [new("a", "A"), new("b", "B")]);
        var level = EquipmentTypeAttribute.Create(
            "level", "Nível", AttributeDataType.Integer, 2, requiredWhen: new AttributeCondition("mode", "c"));

        Assert.Throws<InventoryRuleException>(() => EquipmentType.CreateBuiltIn(
            "test-type", "Tipo de teste", EquipmentCategory.Fixed, EquipmentIcons.Generic, false, [mode, level]));
    }

    [Fact]
    public void Definition_DuplicateKeysOrUnknownIcon_IsRejected()
    {
        var first = EquipmentTypeAttribute.Create("level", "Nível", AttributeDataType.Integer, 1);
        var second = EquipmentTypeAttribute.Create("level", "Outro nível", AttributeDataType.Integer, 2);

        Assert.Throws<InventoryRuleException>(() => EquipmentType.CreateBuiltIn(
            "test-type", "Tipo de teste", EquipmentCategory.Fixed, EquipmentIcons.Generic, false, [first, second]));
        Assert.Throws<InventoryRuleException>(() => EquipmentType.CreateBuiltIn(
            "test-type", "Tipo de teste", EquipmentCategory.Fixed, "rocket", false, [first]));
    }

    [Fact]
    public void Definition_CrewOnFixedType_IsRejected_D62()
    {
        Assert.Throws<InventoryRuleException>(() => EquipmentType.CreateBuiltIn(
            "test-type", "Tipo de teste", EquipmentCategory.Fixed, EquipmentIcons.Generic, crewAssignable: true, []));
    }

    [Fact]
    public void Icons_DomainAndContracts_HaveSameItems()
    {
        Assert.Equal(ConstantsOf(typeof(ContractIcons)), ConstantsOf(typeof(EquipmentIcons)));
        Assert.Equal(ContractIcons.All, EquipmentIcons.All);
        Assert.Equal(ConstantsOf(typeof(EquipmentIcons)).Values.Order(), EquipmentIcons.All.Order());
    }

    private static Dictionary<string, string> ConstantsOf(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .ToDictionary(field => field.Name, field => (string)field.GetRawConstantValue()!);
}
