using Argos.Domain.Common;
using Argos.Domain.Inventory;
using Argos.Domain.Inventory.Equipments;
using static Argos.UnitTests.Domain.Inventory.EquipmentTestData;
using static Argos.UnitTests.Domain.Inventory.InventoryTestData;

namespace Argos.UnitTests.Domain.Inventory;

/// <summary>
/// Inativação do cadastro de equipamento (RF-001, US-09, RN-23, RN-35; D-49; plan 15.2.6, 15.3, 15.4): só em Ainda
/// não instalado, Desativado ou Furtado; cancela as manutenções abertas; cadastro inativo não é editado, não muda de
/// situação e não recebe manutenção.
/// </summary>
public sealed class EquipmentInactivationTests
{
    private static readonly DateTimeOffset Later = Now.AddDays(2);

    [Theory]
    [InlineData(EquipmentStatus.NotInstalled, true)]
    [InlineData(EquipmentStatus.Deactivated, true)]
    [InlineData(EquipmentStatus.Stolen, true)]
    [InlineData(EquipmentStatus.Operational, false)]
    [InlineData(EquipmentStatus.Malfunction, false)]
    [InlineData(EquipmentStatus.UnderMaintenance, false)]
    public void Inactivate_OnlyInNotInstalledDeactivatedOrStolen_RN35(EquipmentStatus status, bool accepted)
    {
        var equipment = InStatus(status);

        if (accepted)
        {
            equipment.Inactivate(null, Later, "admin");

            Assert.False(equipment.IsActive);
            Assert.Equal(Later, equipment.DeactivatedAt);
            Assert.Equal("admin", equipment.DeactivatedBy);
            Assert.Equal(status, equipment.Status);
        }
        else
        {
            var exception = Assert.Throws<InventoryRuleException>(() => equipment.Inactivate(null, Later, "admin"));

            Assert.Equal(
                $"Equipamento {EquipmentStatusLabels.For(status)} não pode ser inativado. A inativação só é possível "
                + "nas situações Ainda não instalado, Desativado ou Furtado.",
                exception.Message);
            Assert.True(equipment.IsActive);
            Assert.Null(equipment.DeactivatedAt);
        }
    }

    [Fact]
    public void Inactivate_InactivatableStatuses_AreTheThreeOfTheRule_RN35()
    {
        Assert.Equal(
            [EquipmentStatus.NotInstalled, EquipmentStatus.Deactivated, EquipmentStatus.Stolen],
            Equipment.InactivatableStatuses);
    }

    [Fact]
    public void Inactivate_NotInstalledWithScheduledMaintenance_CancelsItWithTheReason_RN35()
    {
        var equipment = InStatus(EquipmentStatus.NotInstalled);
        var scheduled = equipment.RegisterMaintenance(
            MaintenanceKind.Scheduled, Today.AddDays(10), null, "Instalação e aferição", Today, Now, Author);

        equipment.Inactivate("Contrato encerrado", Later, "admin");

        Assert.Equal(MaintenanceState.Cancelled, scheduled.State);
        Assert.Equal("Cancelada automaticamente: equipamento inativado.", scheduled.CancellationReason);
        Assert.Equal(Later, scheduled.CancelledAt);
        Assert.Equal("admin", scheduled.CancelledBy);
        Assert.DoesNotContain(equipment.Maintenances, maintenance => maintenance.IsOpen);
    }

    [Fact]
    public void Inactivate_KeepsClosedMaintenancesAsTheyWere_RN35()
    {
        var equipment = InStatus(EquipmentStatus.Malfunction);
        equipment.ChangeStatus(EquipmentStatus.Deactivated, "Fim da vida útil", canReactivate: false, Now, Author);
        var cancelledBefore = Assert.Single(equipment.Maintenances);

        equipment.Inactivate(null, Later, "admin");

        Assert.Equal("Cancelada automaticamente: equipamento desativado.", cancelledBefore.CancellationReason);
        Assert.Equal(Now, cancelledBefore.CancelledAt);
    }

    [Fact]
    public void Inactivate_ReturnsInactivatedChangeWithTrimmedReason_D49()
    {
        var equipment = InStatus(EquipmentStatus.Stolen);

        var change = equipment.Inactivate("  Não foi recuperado  ", Later, "admin");

        Assert.Equal(InventoryChangeKind.Inactivated, change.Kind);
        Assert.Equal(InventoryEntityType.Equipment, change.EntityType);
        Assert.Equal(equipment.Id, change.EntityId);
        Assert.Equal("Não foi recuperado", change.Reason);
        Assert.Equal(Later, change.OccurredAt);
        Assert.Equal("admin", change.Author);
        Assert.Empty(change.Changes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Inactivate_ReasonIsOptional_RN35(string? reason)
    {
        var change = InStatus(EquipmentStatus.Deactivated).Inactivate(reason, Later, "admin");

        Assert.Null(change.Reason);
    }

    [Fact]
    public void Inactivate_ReasonTooLong_IsRejectedInReasonField_RN35()
    {
        var equipment = InStatus(EquipmentStatus.Deactivated);

        var exception = Assert.Throws<DomainValidationException>(() =>
            equipment.Inactivate(new string('m', Equipment.InactivationReasonMaxLength + 1), Later, "admin"));

        Assert.Equal(["reason"], exception.Errors.Keys);
        Assert.True(equipment.IsActive);
    }

    [Fact]
    public void Inactivate_Twice_IsRefused_RN35()
    {
        var equipment = InStatus(EquipmentStatus.Deactivated);
        equipment.Inactivate(null, Now, "admin");

        var exception = Assert.Throws<InventoryRuleException>(() => equipment.Inactivate(null, Later, "outro"));

        Assert.Equal("O equipamento já está inativo.", exception.Message);
        Assert.Equal(Now, equipment.DeactivatedAt);
        Assert.Equal("admin", equipment.DeactivatedBy);
    }

    [Fact]
    public void UpdateRegistration_WhenInactive_IsRefused_RN35()
    {
        var equipment = Inactive(EquipmentStatus.Deactivated);

        var exception = Assert.Throws<InventoryRuleException>(() => equipment.UpdateRegistration(
            Radar, "RD-2024-0057", "Vigilis", "VX-400", NewOrganization(), equipment.Location, null, RadarAttributes()));

        Assert.Equal("Equipamento inativo não pode ser editado.", exception.Message);
        Assert.Equal("VX-300", equipment.Model);
    }

    [Fact]
    public void ChangeStatus_WhenInactive_IsRefusedBeforeAnyOtherCheck_RN35()
    {
        var equipment = Inactive(EquipmentStatus.Deactivated);

        // Reativação permitida pela RN-05 e com justificativa: só a inativação a recusa (verificação 1 de plan 15.3).
        var exception = Assert.Throws<InventoryRuleException>(() =>
            equipment.ChangeStatus(EquipmentStatus.Operational, "Recuperado e testado", canReactivate: true, Later, Author));

        Assert.Equal("Equipamento inativo não pode mudar de situação.", exception.Message);
        Assert.Equal(EquipmentStatus.Deactivated, equipment.Status);
        Assert.Equal(2, equipment.StatusChanges.Count);
    }

    [Fact]
    public void AllowedStatusChanges_WhenInactive_IsEmpty_RN35_D59()
    {
        var equipment = Inactive(EquipmentStatus.NotInstalled);

        Assert.Empty(equipment.AllowedStatusChanges(canReactivate: true));
    }

    [Theory]
    [InlineData(MaintenanceKind.Immediate)]
    [InlineData(MaintenanceKind.Scheduled)]
    [InlineData(MaintenanceKind.Periodic)]
    public void RegisterMaintenance_WhenInactive_IsRefused_RN35(MaintenanceKind kind)
    {
        var equipment = Inactive(EquipmentStatus.NotInstalled);
        DateOnly? dueDate = kind == MaintenanceKind.Immediate ? null : Today.AddDays(5);
        int? periodicity = kind == MaintenanceKind.Periodic ? 30 : null;

        var exception = Assert.Throws<InventoryRuleException>(() =>
            equipment.RegisterMaintenance(kind, dueDate, periodicity, null, Today, Later, Author));

        Assert.Equal("Equipamento inativo não recebe manutenção.", exception.Message);
        Assert.Empty(equipment.Maintenances);
    }

    [Fact]
    public void RegisterMaintenance_WhenInactive_IsRefusedEvenWithFieldErrors_RN35()
    {
        var equipment = Inactive(EquipmentStatus.NotInstalled);

        // Sem data prevista, a agendada teria erro de campo; o cadastro inativo vem antes (plan 15.4).
        var exception = Assert.Throws<InventoryRuleException>(() =>
            equipment.RegisterMaintenance(MaintenanceKind.Scheduled, null, null, null, Today, Later, Author));

        Assert.Equal("Equipamento inativo não recebe manutenção.", exception.Message);
    }

    [Fact]
    public void CompleteMaintenance_WhenInactive_IsRefused_RN35()
    {
        var equipment = InStatus(EquipmentStatus.NotInstalled);
        var scheduled = equipment.RegisterMaintenance(MaintenanceKind.Scheduled, Today, null, null, Today, Now, Author);
        equipment.Inactivate(null, Now, Author);

        var exception = Assert.Throws<InventoryRuleException>(() => equipment.CompleteMaintenance(
            scheduled.Id, MaintenanceResult.Success, Now, null, SaoPaulo, Later, Author));

        Assert.Equal("Equipamento inativo não recebe manutenção.", exception.Message);
        Assert.Equal(MaintenanceState.Cancelled, scheduled.State);
    }

    private static Equipment Inactive(EquipmentStatus status)
    {
        var equipment = InStatus(status);
        equipment.Inactivate(null, Now, Author);
        return equipment;
    }
}
