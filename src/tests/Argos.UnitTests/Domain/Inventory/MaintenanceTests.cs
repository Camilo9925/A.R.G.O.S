using Argos.Domain.Common;
using Argos.Domain.Common.Parameters;
using Argos.Domain.Inventory;
using Argos.Domain.Inventory.Equipments;
using static Argos.UnitTests.Domain.Inventory.EquipmentTestData;
using static Argos.UnitTests.Domain.Inventory.InventoryTestData;

namespace Argos.UnitTests.Domain.Inventory;

/// <summary>
/// Manutenções, necessidade de manutenção e parâmetros (RF-009, US-05, RN-07 a RN-11, RN-34; D-54, D-55;
/// plan 15.2.8, 15.4, 15.10).
/// </summary>
public sealed class MaintenanceTests
{
    private const int DefaultWindow = 7;

    [Fact]
    public void Register_Immediate_WithoutDateOrPeriodicity_RN08()
    {
        var maintenance = NewRadar().RegisterMaintenance(MaintenanceKind.Immediate, null, null, "Lente trincada", Today, Now, Author);

        Assert.Equal(MaintenanceKind.Immediate, maintenance.Kind);
        Assert.Equal(MaintenanceState.Open, maintenance.State);
        Assert.Equal(MaintenanceOrigin.Manual, maintenance.Origin);
        Assert.Null(maintenance.DueDate);
    }

    [Fact]
    public void Register_ImmediateWithDateOrPeriodicity_IsRejectedInFields_RN08()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            NewRadar().RegisterMaintenance(MaintenanceKind.Immediate, Today, 30, null, Today, Now, Author));

        Assert.Equal(["dueDate", "periodicityDays"], exception.Errors.Keys.Order());
    }

    [Fact]
    public void Register_ScheduledWithoutDateOrInThePast_IsRejected_RN08()
    {
        var equipment = NewRadar();

        Assert.Equal(
            ["Informe a data prevista."],
            Assert.Throws<DomainValidationException>(() =>
                equipment.RegisterMaintenance(MaintenanceKind.Scheduled, null, null, null, Today, Now, Author)).Errors["dueDate"]);
        Assert.Equal(
            ["A data prevista não pode ser anterior a hoje."],
            Assert.Throws<DomainValidationException>(() =>
                equipment.RegisterMaintenance(MaintenanceKind.Scheduled, Today.AddDays(-1), null, null, Today, Now, Author)).Errors["dueDate"]);

        var today = equipment.RegisterMaintenance(MaintenanceKind.Scheduled, Today, null, null, Today, Now, Author);
        Assert.Equal(Today, today.DueDate);
    }

    [Fact]
    public void Register_PeriodicWithoutDateAndPeriodicity_IsRejectedInBothFields_RN08()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            NewRadar().RegisterMaintenance(MaintenanceKind.Periodic, null, null, null, Today, Now, Author));

        Assert.Equal(["dueDate", "periodicityDays"], exception.Errors.Keys.Order());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3651)]
    public void Register_PeriodicityOutOfRange_IsRejected_RN08(int periodicityDays)
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            NewRadar().RegisterMaintenance(MaintenanceKind.Periodic, Today.AddDays(30), periodicityDays, null, Today, Now, Author));

        Assert.Equal(["periodicityDays"], exception.Errors.Keys);
    }

    [Theory]
    [InlineData(EquipmentStatus.Deactivated, MaintenanceKind.Immediate)]
    [InlineData(EquipmentStatus.Deactivated, MaintenanceKind.Scheduled)]
    [InlineData(EquipmentStatus.Deactivated, MaintenanceKind.Periodic)]
    [InlineData(EquipmentStatus.Stolen, MaintenanceKind.Immediate)]
    [InlineData(EquipmentStatus.Stolen, MaintenanceKind.Scheduled)]
    [InlineData(EquipmentStatus.Stolen, MaintenanceKind.Periodic)]
    public void Register_InDeactivatedOrStolen_IsRefused_RN11(EquipmentStatus status, MaintenanceKind kind)
    {
        var equipment = InStatus(status);
        DateOnly? dueDate = kind == MaintenanceKind.Immediate ? null : Today.AddDays(10);
        int? periodicity = kind == MaintenanceKind.Periodic ? 90 : null;

        var exception = Assert.Throws<InventoryRuleException>(() =>
            equipment.RegisterMaintenance(kind, dueDate, periodicity, null, Today, Now, Author));

        Assert.Equal("Equipamento desativado ou furtado não recebe manutenção.", exception.Message);
    }

    [Fact]
    public void Register_SecondImmediate_IsRefused_RN07()
    {
        var equipment = NewRadar(EquipmentStatus.Malfunction);

        var exception = Assert.Throws<InventoryRuleException>(() =>
            equipment.RegisterMaintenance(MaintenanceKind.Immediate, null, null, null, Today, Now, Author));

        Assert.Equal("Já existe uma manutenção imediata aberta.", exception.Message);
        Assert.Single(equipment.Maintenances);
    }

    [Fact]
    public void Complete_Success_FromMalfunction_ReturnsToOperational_RN11()
    {
        var equipment = NewRadar(EquipmentStatus.Malfunction);
        var immediate = equipment.Maintenances.Single();

        var completion = equipment.CompleteMaintenance(
            immediate.Id, MaintenanceResult.Success, Now.AddHours(1), "Lente trocada", SaoPaulo, Now.AddHours(2), "tecnico");

        Assert.Equal(EquipmentStatus.Operational, completion.Status);
        Assert.Equal(MaintenanceState.Completed, immediate.State);
        Assert.Equal(MaintenanceResult.Success, immediate.Result);
        Assert.Equal(Now.AddHours(1), immediate.CompletedAt);
        Assert.Equal("Lente trocada", immediate.CompletionNotes);
        Assert.Equal("tecnico", immediate.CompletedBy);

        var change = equipment.StatusChanges.Last();
        Assert.Equal(StatusChangeOrigin.MaintenanceCompletion, change.Origin);
        Assert.Equal(immediate.Id, change.MaintenanceId);
        Assert.Equal(EquipmentStatus.Operational, change.ToStatus);
    }

    [Fact]
    public void Complete_Success_FromUnderMaintenance_ReturnsToOperational_RN11()
    {
        var equipment = NewRadar(EquipmentStatus.UnderMaintenance);
        var maintenance = equipment.RegisterMaintenance(MaintenanceKind.Scheduled, Today, null, null, Today, Now, Author);

        Assert.Equal(
            EquipmentStatus.Operational,
            equipment.CompleteMaintenance(maintenance.Id, MaintenanceResult.Success, Now, null, SaoPaulo, Now, Author).Status);
    }

    [Fact]
    public void Complete_Success_OperationalStaysOperational_RN11()
    {
        var equipment = NewRadar();
        var maintenance = equipment.RegisterMaintenance(MaintenanceKind.Scheduled, Today, null, null, Today, Now, Author);

        equipment.CompleteMaintenance(maintenance.Id, MaintenanceResult.Success, Now, null, SaoPaulo, Now, Author);

        Assert.Equal(EquipmentStatus.Operational, equipment.Status);
        Assert.Single(equipment.StatusChanges);
    }

    [Theory]
    [InlineData(EquipmentStatus.Operational)]
    [InlineData(EquipmentStatus.UnderMaintenance)]
    public void Complete_Failure_GoesToMalfunctionAndOpensImmediate_RN11_RN07(EquipmentStatus status)
    {
        var equipment = NewRadar(status);
        var maintenance = equipment.RegisterMaintenance(MaintenanceKind.Scheduled, Today, null, null, Today, Now, Author);

        var completion = equipment.CompleteMaintenance(maintenance.Id, MaintenanceResult.Failure, Now, null, SaoPaulo, Now, Author);

        Assert.Equal(EquipmentStatus.Malfunction, completion.Status);
        Assert.True(equipment.HasOpenImmediateMaintenance);
    }

    [Fact]
    public void Complete_FailureOfTheImmediateInMalfunction_OpensNewImmediate_RN11()
    {
        var equipment = NewRadar(EquipmentStatus.Malfunction);
        var first = equipment.Maintenances.Single();

        equipment.CompleteMaintenance(first.Id, MaintenanceResult.Failure, Now, null, SaoPaulo, Now, Author);

        Assert.Equal(EquipmentStatus.Malfunction, equipment.Status);
        var open = Assert.Single(equipment.Maintenances, maintenance => maintenance.IsOpen);
        Assert.NotEqual(first.Id, open.Id);
        Assert.Equal(MaintenanceKind.Immediate, open.Kind);
        Assert.Single(equipment.StatusChanges);
    }

    [Theory]
    [InlineData(MaintenanceResult.Success)]
    [InlineData(MaintenanceResult.Failure)]
    public void Complete_InNotInstalled_DoesNotChangeStatus_RN11(MaintenanceResult result)
    {
        var equipment = NewRadar(EquipmentStatus.NotInstalled);
        var maintenance = equipment.RegisterMaintenance(MaintenanceKind.Scheduled, Today, null, null, Today, Now, Author);

        equipment.CompleteMaintenance(maintenance.Id, result, Now, null, SaoPaulo, Now, Author);

        Assert.Equal(EquipmentStatus.NotInstalled, equipment.Status);
        Assert.False(equipment.HasOpenImmediateMaintenance);
        Assert.Single(equipment.StatusChanges);
    }

    [Theory]
    [InlineData(MaintenanceResult.Success)]
    [InlineData(MaintenanceResult.Failure)]
    public void Complete_Periodic_SchedulesNextFromCompletionDateInMunicipalTimeZone_RN09(MaintenanceResult result)
    {
        var equipment = NewRadar();
        var periodic = equipment.RegisterMaintenance(
            MaintenanceKind.Periodic, Today, 180, "Aferição anual", Today, Now.AddDays(-1), Author);

        // 01:30 UTC de 19/10 é 22:30 de 18/10 em São Paulo: a próxima conta a partir de 18/10.
        var completedAt = new DateTimeOffset(2026, 10, 19, 1, 30, 0, TimeSpan.Zero);
        var completion = equipment.CompleteMaintenance(
            periodic.Id, result, completedAt, null, SaoPaulo, completedAt.AddMinutes(5), Author);

        var next = Assert.IsType<Maintenance>(completion.NextPeriodic);
        Assert.Equal(new DateOnly(2026, 10, 18).AddDays(180), next.DueDate);
        Assert.Equal(MaintenanceKind.Periodic, next.Kind);
        Assert.Equal(180, next.PeriodicityDays);
        Assert.Equal("Aferição anual", next.Description);
        Assert.Equal(MaintenanceOrigin.PeriodicRescheduling, next.Origin);
        Assert.Equal(periodic.Id, next.PreviousMaintenanceId);
        Assert.True(next.IsOpen);
    }

    [Fact]
    public void Complete_NonPeriodic_DoesNotScheduleNext_RN09()
    {
        var equipment = NewRadar();
        var scheduled = equipment.RegisterMaintenance(MaintenanceKind.Scheduled, Today, null, null, Today, Now, Author);

        Assert.Null(equipment.CompleteMaintenance(scheduled.Id, MaintenanceResult.Success, Now, null, SaoPaulo, Now, Author).NextPeriodic);
    }

    [Fact]
    public void Complete_InFutureOrBeforeOpening_IsRejectedInCompletedAt_US05()
    {
        var equipment = NewRadar();
        var maintenance = equipment.RegisterMaintenance(MaintenanceKind.Scheduled, Today, null, null, Today, Now, Author);

        Assert.Equal(
            ["completedAt"],
            Assert.Throws<DomainValidationException>(() => equipment.CompleteMaintenance(
                maintenance.Id, MaintenanceResult.Success, Now.AddMinutes(1), null, SaoPaulo, Now, Author)).Errors.Keys);
        Assert.Equal(
            ["completedAt"],
            Assert.Throws<DomainValidationException>(() => equipment.CompleteMaintenance(
                maintenance.Id, MaintenanceResult.Success, Now.AddMinutes(-1), null, SaoPaulo, Now, Author)).Errors.Keys);
        Assert.True(maintenance.IsOpen);
    }

    [Fact]
    public void Complete_TwiceOrUnknown_IsRefused_RN08()
    {
        var equipment = NewRadar();
        var maintenance = equipment.RegisterMaintenance(MaintenanceKind.Scheduled, Today, null, null, Today, Now, Author);
        equipment.CompleteMaintenance(maintenance.Id, MaintenanceResult.Success, Now, null, SaoPaulo, Now, Author);

        Assert.Throws<InventoryRuleException>(() => equipment.CompleteMaintenance(
            maintenance.Id, MaintenanceResult.Success, Now, null, SaoPaulo, Now, Author));
        Assert.Throws<InventoryRuleException>(() => equipment.CompleteMaintenance(
            Guid.NewGuid(), MaintenanceResult.Success, Now, null, SaoPaulo, Now, Author));
    }

    [Fact]
    public void Need_None_WithoutOpenMaintenance_RN10()
    {
        Assert.Equal(MaintenanceNeed.None, NewRadar().CalculateMaintenanceNeed(Today, DefaultWindow));
    }

    [Theory]
    [InlineData(-1, MaintenanceNeed.Overdue)]
    [InlineData(0, MaintenanceNeed.Upcoming)]
    [InlineData(7, MaintenanceNeed.Upcoming)]
    [InlineData(8, MaintenanceNeed.Scheduled)]
    public void Need_ByDueDate_WithDefaultWindowOf7Days_RN10(int daysFromToday, MaintenanceNeed expected)
    {
        var equipment = NewRadar();
        equipment.RegisterMaintenance(
            MaintenanceKind.Periodic, Today.AddDays(daysFromToday), 30, null, Today.AddDays(-30), Now, Author);

        Assert.Equal(expected, equipment.CalculateMaintenanceNeed(Today, DefaultWindow));
    }

    [Theory]
    [InlineData(MaintenanceKind.Immediate, null, MaintenanceNeed.Immediate)]
    [InlineData(MaintenanceKind.Scheduled, -1, MaintenanceNeed.Overdue)]
    [InlineData(MaintenanceKind.Periodic, 0, MaintenanceNeed.Upcoming)]
    [InlineData(MaintenanceKind.Scheduled, 7, MaintenanceNeed.Upcoming)]
    [InlineData(MaintenanceKind.Periodic, 8, MaintenanceNeed.Scheduled)]
    public void Classify_SingleOpenMaintenance_SameRuleAsCalculate_RN10(
        MaintenanceKind kind, int? daysFromToday, MaintenanceNeed expected)
    {
        var dueDate = daysFromToday is { } days ? Today.AddDays(days) : (DateOnly?)null;

        Assert.Equal(expected, MaintenanceNeedCalculator.Classify(kind, dueDate, Today, DefaultWindow));
    }

    [Fact]
    public void Need_WindowFromParameter_ChangesUpcoming_RN10_RN34()
    {
        var equipment = NewRadar();
        equipment.RegisterMaintenance(MaintenanceKind.Scheduled, Today.AddDays(10), null, null, Today, Now, Author);

        Assert.Equal(MaintenanceNeed.Scheduled, equipment.CalculateMaintenanceNeed(Today, DefaultWindow));
        Assert.Equal(MaintenanceNeed.Upcoming, equipment.CalculateMaintenanceNeed(Today, upcomingWindowDays: 15));
    }

    [Fact]
    public void Need_MostSevereWins_D54()
    {
        var equipment = NewRadar();
        equipment.RegisterMaintenance(MaintenanceKind.Scheduled, Today.AddDays(30), null, null, Today, Now, Author);
        Assert.Equal(MaintenanceNeed.Scheduled, equipment.CalculateMaintenanceNeed(Today, DefaultWindow));

        equipment.RegisterMaintenance(MaintenanceKind.Scheduled, Today.AddDays(2), null, null, Today, Now, Author);
        Assert.Equal(MaintenanceNeed.Upcoming, equipment.CalculateMaintenanceNeed(Today, DefaultWindow));

        equipment.RegisterMaintenance(MaintenanceKind.Periodic, Today.AddDays(-3), 30, null, Today, Now, Author);
        Assert.Equal(MaintenanceNeed.Overdue, equipment.CalculateMaintenanceNeed(Today, DefaultWindow));

        equipment.RegisterMaintenance(MaintenanceKind.Immediate, null, null, null, Today, Now, Author);
        Assert.Equal(MaintenanceNeed.Immediate, equipment.CalculateMaintenanceNeed(Today, DefaultWindow));
    }

    [Fact]
    public void Need_IgnoresCompletedAndCancelled_D54()
    {
        var equipment = NewRadar(EquipmentStatus.Malfunction);
        equipment.ChangeStatus(EquipmentStatus.Stolen, "Levado durante a madrugada", false, Now, Author);

        Assert.Equal(MaintenanceNeed.None, equipment.CalculateMaintenanceNeed(Today, DefaultWindow));
    }

    [Fact]
    public void Parameter_UpcomingWindow_DefaultAndRange_RN34()
    {
        var definition = BusinessParameterCatalog.Get(BusinessParameter.MaintenanceUpcomingWindowDays);

        Assert.Equal("maintenance.upcoming-window-days", definition.Key);
        Assert.Equal(7, definition.Default);
        Assert.Equal((1, 60), (definition.Min, definition.Max));
        Assert.Same(definition, BusinessParameterCatalog.FindByKey("maintenance.upcoming-window-days"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    public void Parameter_WithinRange_IsAccepted_RN34(int value)
    {
        Assert.Equal(value, BusinessParameterCatalog.Validate(BusinessParameter.MaintenanceUpcomingWindowDays, value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void Parameter_OutOfRange_IsRejectedInValueField_RN34(int value)
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            BusinessParameterCatalog.Validate(BusinessParameter.MaintenanceUpcomingWindowDays, value));

        Assert.Equal(["Informe um valor entre 1 e 60 dias."], exception.Errors["value"]);
    }

    [Fact]
    public void Parameter_EveryDefaultIsWithinItsRange_RN34()
    {
        Assert.All(BusinessParameterCatalog.All, definition =>
            Assert.InRange(definition.Default, definition.Min, definition.Max));
    }
}
