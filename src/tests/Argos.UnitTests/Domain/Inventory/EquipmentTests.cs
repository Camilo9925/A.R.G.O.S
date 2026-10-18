using Argos.Domain.Common;
using Argos.Domain.Inventory;
using Argos.Domain.Inventory.Equipments;
using Argos.Domain.Inventory.Types;
using static Argos.UnitTests.Domain.Inventory.EquipmentTestData;
using static Argos.UnitTests.Domain.Inventory.InventoryTestData;

namespace Argos.UnitTests.Domain.Inventory;

/// <summary>
/// Equipamento: cadastro, edição e situação (RF-001, RF-002, RF-007, RF-008, US-01, US-04, RN-01, RN-04 a RN-07,
/// RN-22; D-47, D-49, D-59, D-66; plan 15.2.6, 15.2.7, 15.3).
/// </summary>
public sealed class EquipmentTests
{
    // Tabela da RN-05, escrita à mão a partir da spec: os testes conferem o Domínio contra a regra.
    private static readonly HashSet<(EquipmentStatus From, EquipmentStatus To)> Rn05 =
    [
        (EquipmentStatus.NotInstalled, EquipmentStatus.Operational),
        (EquipmentStatus.NotInstalled, EquipmentStatus.Deactivated),
        (EquipmentStatus.Operational, EquipmentStatus.Malfunction),
        (EquipmentStatus.Operational, EquipmentStatus.UnderMaintenance),
        (EquipmentStatus.Operational, EquipmentStatus.Deactivated),
        (EquipmentStatus.Operational, EquipmentStatus.Stolen),
        (EquipmentStatus.Malfunction, EquipmentStatus.UnderMaintenance),
        (EquipmentStatus.Malfunction, EquipmentStatus.Operational),
        (EquipmentStatus.Malfunction, EquipmentStatus.Deactivated),
        (EquipmentStatus.Malfunction, EquipmentStatus.Stolen),
        (EquipmentStatus.UnderMaintenance, EquipmentStatus.Operational),
        (EquipmentStatus.UnderMaintenance, EquipmentStatus.Malfunction),
        (EquipmentStatus.UnderMaintenance, EquipmentStatus.Deactivated),
        (EquipmentStatus.UnderMaintenance, EquipmentStatus.Stolen),
        (EquipmentStatus.Stolen, EquipmentStatus.UnderMaintenance),
        (EquipmentStatus.Stolen, EquipmentStatus.Deactivated),
        (EquipmentStatus.Deactivated, EquipmentStatus.Operational),
    ];

    public static TheoryData<EquipmentStatus, EquipmentStatus> AllCombinations()
    {
        var data = new TheoryData<EquipmentStatus, EquipmentStatus>();
        foreach (var from in Enum.GetValues<EquipmentStatus>())
        {
            foreach (var to in Enum.GetValues<EquipmentStatus>())
            {
                data.Add(from, to);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void ChangeStatus_EveryOriginAndTarget_FollowsTable_RN05(EquipmentStatus from, EquipmentStatus to)
    {
        var equipment = InStatus(from);
        const string Justification = "Justificativa de teste";

        if (from == to)
        {
            var same = Assert.Throws<InventoryRuleException>(() =>
                equipment.ChangeStatus(to, Justification, canReactivate: true, Now, Author));
            Assert.Equal("O equipamento já está nessa situação.", same.Message);
        }
        else if (Rn05.Contains((from, to)))
        {
            equipment.ChangeStatus(to, Justification, canReactivate: true, Now, Author);
            Assert.Equal(to, equipment.Status);
        }
        else
        {
            var refused = Assert.Throws<InventoryRuleException>(() =>
                equipment.ChangeStatus(to, Justification, canReactivate: true, Now, Author));
            Assert.Equal(
                $"Não é permitido passar de {EquipmentStatusLabels.For(from)} para {EquipmentStatusLabels.For(to)}.",
                refused.Message);
            Assert.Equal(from, equipment.Status);
        }
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void AllowedStatusChanges_MatchesTheTable_RN05_D59(EquipmentStatus from, EquipmentStatus to)
    {
        var options = InStatus(from).AllowedStatusChanges(canReactivate: true);

        Assert.Equal(Rn05.Contains((from, to)), options.Any(option => option.Target == to));
    }

    [Theory]
    [InlineData(EquipmentStatus.Operational, EquipmentStatus.Stolen)]
    [InlineData(EquipmentStatus.Operational, EquipmentStatus.Deactivated)]
    [InlineData(EquipmentStatus.Deactivated, EquipmentStatus.Operational)]
    public void ChangeStatus_WithoutJustification_IsRejectedInJustificationField_RN06(
        EquipmentStatus from, EquipmentStatus to)
    {
        var equipment = InStatus(from);

        foreach (var justification in new[] { null, "  ", "ab" })
        {
            var exception = Assert.Throws<DomainValidationException>(() =>
                equipment.ChangeStatus(to, justification, canReactivate: true, Now, Author));
            Assert.Equal(["justification"], exception.Errors.Keys);
        }

        Assert.Equal(from, equipment.Status);
    }

    [Fact]
    public void ChangeStatus_JustificationOnlyInTheThreeCases_RN06()
    {
        var operational = InStatus(EquipmentStatus.Operational);

        Assert.Equal(
            [EquipmentStatus.Deactivated, EquipmentStatus.Stolen],
            operational.AllowedStatusChanges(canReactivate: true)
                .Where(option => option.RequiresJustification)
                .Select(option => option.Target)
                .Order());

        operational.ChangeStatus(EquipmentStatus.UnderMaintenance, justification: null, canReactivate: false, Now, Author);
        Assert.Equal(EquipmentStatus.UnderMaintenance, operational.Status);
    }

    [Fact]
    public void ChangeStatus_RecordsHistoryWithAuthorAndJustification_US04()
    {
        var equipment = NewRadar();
        var later = Now.AddHours(2);

        equipment.ChangeStatus(EquipmentStatus.Stolen, "  Levado durante a madrugada  ", canReactivate: false, later, "operador");

        var change = equipment.StatusChanges.Last();
        Assert.Equal(EquipmentStatus.Operational, change.FromStatus);
        Assert.Equal(EquipmentStatus.Stolen, change.ToStatus);
        Assert.Equal(StatusChangeOrigin.Manual, change.Origin);
        Assert.Equal("Levado durante a madrugada", change.Justification);
        Assert.Equal("operador", change.Author);
        Assert.Equal(later, change.OccurredAt);
        Assert.Equal(later, equipment.StatusChangedAt);
    }

    [Fact]
    public void Reactivation_WithoutPermission_IsRefused_D47()
    {
        var equipment = InStatus(EquipmentStatus.Deactivated);

        var exception = Assert.Throws<ReactivationNotAllowedException>(() =>
            equipment.ChangeStatus(EquipmentStatus.Operational, "Recuperado e testado", canReactivate: false, Now, Author));

        Assert.Equal("Somente o Administrador pode reativar um equipamento desativado.", exception.Message);
        Assert.Equal(EquipmentStatus.Deactivated, equipment.Status);
    }

    [Fact]
    public void Reactivation_WithPermissionAndJustification_ReturnsToOperational_RN05()
    {
        var equipment = InStatus(EquipmentStatus.Deactivated);

        equipment.ChangeStatus(EquipmentStatus.Operational, "Recuperado e testado", canReactivate: true, Now, Author);

        Assert.Equal(EquipmentStatus.Operational, equipment.Status);
    }

    [Fact]
    public void AllowedStatusChanges_WithoutPermission_HidesReactivation_D59()
    {
        var equipment = InStatus(EquipmentStatus.Deactivated);

        Assert.Empty(equipment.AllowedStatusChanges(canReactivate: false));
        Assert.Equal(
            [new StatusChangeOption(EquipmentStatus.Operational, RequiresJustification: true)],
            equipment.AllowedStatusChanges(canReactivate: true));
    }

    [Fact]
    public void Register_CreatesInitialRecordWithoutPreviousStatus_US01()
    {
        var equipment = NewRadar();

        var initial = Assert.Single(equipment.StatusChanges);
        Assert.Null(initial.FromStatus);
        Assert.Equal(EquipmentStatus.Operational, initial.ToStatus);
        Assert.Equal(StatusChangeOrigin.Registration, initial.Origin);
        Assert.Equal(Author, initial.Author);
        Assert.Equal(EquipmentCategory.Fixed, equipment.Category);
        Assert.Empty(equipment.Maintenances);
    }

    [Fact]
    public void Register_InitialMalfunction_OpensImmediateOnce_RN07()
    {
        var equipment = NewRadar(EquipmentStatus.Malfunction);

        var immediate = Assert.Single(equipment.Maintenances);
        Assert.Equal(MaintenanceKind.Immediate, immediate.Kind);
        Assert.Equal(MaintenanceOrigin.AutomaticOnMalfunction, immediate.Origin);
        Assert.Equal("Aberta automaticamente: equipamento em mau funcionamento.", immediate.Description);
        Assert.True(immediate.IsOpen);
    }

    [Fact]
    public void ChangeStatus_ToMalfunction_OpensImmediateOnlyIfNoneOpen_RN07()
    {
        var equipment = NewRadar();

        equipment.ChangeStatus(EquipmentStatus.Malfunction, null, false, Now, Author);
        equipment.ChangeStatus(EquipmentStatus.UnderMaintenance, null, false, Now, Author);
        equipment.ChangeStatus(EquipmentStatus.Malfunction, null, false, Now, Author);

        Assert.Single(equipment.Maintenances, maintenance => maintenance.Kind == MaintenanceKind.Immediate);
    }

    [Theory]
    [InlineData(EquipmentStatus.Deactivated, "Cancelada automaticamente: equipamento desativado.")]
    [InlineData(EquipmentStatus.Stolen, "Cancelada automaticamente: equipamento furtado.")]
    public void ChangeStatus_ToDeactivatedOrStolen_CancelsOpenMaintenances_RN11(EquipmentStatus target, string reason)
    {
        var equipment = NewRadar(EquipmentStatus.Malfunction);
        equipment.RegisterMaintenance(MaintenanceKind.Scheduled, Today.AddDays(3), null, null, Today, Now, Author);

        equipment.ChangeStatus(target, "Justificativa de teste", canReactivate: false, Now, Author);

        Assert.Equal(2, equipment.Maintenances.Count);
        Assert.All(equipment.Maintenances, maintenance =>
        {
            Assert.Equal(MaintenanceState.Cancelled, maintenance.State);
            Assert.Equal(reason, maintenance.CancellationReason);
            Assert.Equal(Author, maintenance.CancelledBy);
        });
    }

    [Theory]
    [InlineData(EquipmentStatus.Deactivated)]
    [InlineData(EquipmentStatus.Stolen)]
    public void Register_InitialStatusRequiringJustification_IsRejected_D66(EquipmentStatus status)
    {
        var exception = Assert.Throws<DomainValidationException>(() => NewRadar(status));

        Assert.Equal(["status"], exception.Errors.Keys);
    }

    [Fact]
    public void Register_FixedWithoutLocation_IsRejected_RF007()
    {
        var exception = Assert.Throws<DomainValidationException>(() => Equipment.Register(
            Radar, "RD-1", "Vigilis", "VX-300", NewOrganization(), location: null, operationalBase: null,
            RadarAttributes(), EquipmentStatus.Operational, Now, Author));

        Assert.Equal(["location"], exception.Errors.Keys);
    }

    [Fact]
    public void Register_MobileWithoutBase_IsRejected_RF007()
    {
        var exception = Assert.Throws<DomainValidationException>(() => Equipment.Register(
            Patrol, "VT-1", "Orbis", "Patrulha", NewOrganization(), location: null, operationalBase: null,
            PatrolAttributes(), EquipmentStatus.Operational, Now, Author));

        Assert.Equal(["baseId"], exception.Errors.Keys);
    }

    [Fact]
    public void Register_MobileWithLocation_IsRejected_RF007()
    {
        var organization = NewOrganization();

        var exception = Assert.Throws<DomainValidationException>(() => Equipment.Register(
            Patrol, "VT-1", "Orbis", "Patrulha", organization, NewLocation(), NewBase(organization),
            PatrolAttributes(), EquipmentStatus.Operational, Now, Author));

        Assert.Equal(["location"], exception.Errors.Keys);
    }

    [Fact]
    public void Register_Mobile_HasBaseAndNoLocation_RF007()
    {
        var equipment = NewPatrol();

        Assert.Equal(EquipmentCategory.Mobile, equipment.Category);
        Assert.NotNull(equipment.BaseId);
        Assert.Null(equipment.Location);
    }

    [Fact]
    public void Register_WithoutOrganizationOrWithInactive_IsRejected_RN22()
    {
        var inactive = NewOrganization();
        inactive.Inactivate(Now, Author);

        Assert.Equal(
            ["organizationId"],
            Assert.Throws<DomainValidationException>(() => NewRadar(organization: inactive)).Errors.Keys);
        Assert.Equal(
            ["organizationId"],
            Assert.Throws<DomainValidationException>(() => Equipment.Register(
                Radar, "RD-1", "Vigilis", "VX-300", organization: null, NewLocation(), null,
                RadarAttributes(), EquipmentStatus.Operational, Now, Author)).Errors.Keys);
    }

    [Fact]
    public void Register_AllFieldErrorsAtOnce_IncludingAttributes_D46()
    {
        var exception = Assert.Throws<DomainValidationException>(() => Equipment.Register(
            Radar, " ", new string('m', 61), "VX-300", NewOrganization(), NewLocation(), null,
            new Dictionary<string, object?> { ["modality"] = "speed", ["monitoredLanes"] = 2 },
            EquipmentStatus.Operational, Now, Author));

        Assert.Equal(["attributes.maxSpeedKmh", "brand", "serialNumber"], exception.Errors.Keys.Order());
    }

    [Fact]
    public void Register_KeysForUniqueness_IgnoreCaseAccentsAndSpaces_RN01()
    {
        var first = Equipment.Register(
            Radar, " rd-2024-0057 ", "Vig ílis", "VX-300", NewOrganization(), NewLocation(), null,
            RadarAttributes(), EquipmentStatus.Operational, Now, Author);
        var second = NewRadar();

        Assert.Equal(second.BrandKey, first.BrandKey);
        Assert.Equal(second.SerialKey, first.SerialKey);
        Assert.Equal("VIGILIS", first.BrandKey);
        Assert.Equal("RD-2024-0057", first.SerialKey);
    }

    [Fact]
    public void UpdateRegistration_ReturnsChangedFieldsInDisplayText_D49()
    {
        var equipment = NewRadar();
        var attributes = RadarAttributes();
        attributes["maxSpeedKmh"] = 80;

        var changes = equipment.UpdateRegistration(
            Radar, "RD-2024-0057", "Vigilis", "VX-400", NewOrganization(), equipment.Location, null, attributes);

        Assert.Contains(new FieldChange("model", "Modelo", "VX-300", "VX-400"), changes);
        Assert.Contains(new FieldChange("attributes.maxSpeedKmh", "Velocidade máxima", "60 km/h", "80 km/h"), changes);
        Assert.Contains(changes, change => change.Field == "organizationId");
        Assert.Equal("VX-400", equipment.Model);
    }

    [Fact]
    public void UpdateRegistration_WithoutChanges_ReturnsEmpty_D49()
    {
        var organization = NewOrganization();
        var equipment = NewRadar(organization: organization);

        var changes = equipment.UpdateRegistration(
            Radar, "RD-2024-0057", "Vigilis", "VX-300", organization, equipment.Location, null, RadarAttributes());

        Assert.Empty(changes);
    }

    [Fact]
    public void UpdateRegistration_OtherType_IsRefused_D66()
    {
        var equipment = NewRadar();

        Assert.Throws<ArgumentException>(() => equipment.UpdateRegistration(
            TypeOf(EquipmentTypeCatalog.SurveillanceCamera), "RD-2024-0057", "Vigilis", "VX-300",
            NewOrganization(), equipment.Location, null, null));
    }

    [Fact]
    public void UpdateRegistration_KeepsOwnOrganizationEvenIfInactive_RN22()
    {
        var organization = NewOrganization();
        var equipment = NewRadar(organization: organization);
        organization.Inactivate(Now, Author);

        var changes = equipment.UpdateRegistration(
            Radar, "RD-2024-0057", "Vigilis", "VX-301", organization, equipment.Location, null, RadarAttributes());

        Assert.Equal(["model"], changes.Select(change => change.Field));
    }

    [Fact]
    public void InventoryChange_Updated_RequiresAtLeastOneChange_D49()
    {
        var equipment = NewRadar();

        Assert.Throws<ArgumentException>(() =>
            InventoryChange.Updated(InventoryEntityType.Equipment, equipment.Id, [], Now, Author));

        var change = InventoryChange.Updated(
            InventoryEntityType.Equipment, equipment.Id, [new FieldChange("model", "Modelo", "A", "B")], Now, Author);
        Assert.Equal(InventoryChangeKind.Updated, change.Kind);
        Assert.Single(change.Changes);
    }
}
