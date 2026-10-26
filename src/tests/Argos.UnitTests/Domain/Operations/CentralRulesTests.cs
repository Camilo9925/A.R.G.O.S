using Argos.Domain.Common;
using Argos.Domain.Inventory.Equipments;
using Argos.Domain.Operations;
using static Argos.UnitTests.Domain.Inventory.InventoryTestData;

namespace Argos.UnitTests.Domain.Operations;

/// <summary>
/// Indicadores, escopo dos filtros, gravidade e escala da Central (US-11 a US-13; RF-030, RF-032; RN-14 a RN-16, RN-36;
/// D-79, D-82, D-94; plan 16.3).
/// </summary>
public sealed class CentralRulesTests
{
    private static Dictionary<EquipmentStatus, int> Counts(
        int notInstalled = 0, int operational = 0, int malfunction = 0, int maintenance = 0, int deactivated = 0, int stolen = 0) =>
        new()
        {
            [EquipmentStatus.NotInstalled] = notInstalled,
            [EquipmentStatus.Operational] = operational,
            [EquipmentStatus.Malfunction] = malfunction,
            [EquipmentStatus.UnderMaintenance] = maintenance,
            [EquipmentStatus.Deactivated] = deactivated,
            [EquipmentStatus.Stolen] = stolen,
        };

    // RN-14, RN-15

    [Fact]
    public void Availability_Seed42_Is90Point3_RN14_RN15()
    {
        var summary = AvailabilityCalculator.Calculate(
            Counts(notInstalled: 12, operational: 252, malfunction: 15, maintenance: 9, deactivated: 6, stolen: 3));

        Assert.Equal(new AvailabilitySummary(297, 252, 27, 90.3m), summary);
    }

    [Fact]
    public void Availability_ExcludesNotInstalledAndDeactivatedFromTheDenominator_RN14()
    {
        // 3 ÷ (10 − 4 − 2) × 100 = 75
        var summary = AvailabilityCalculator.Calculate(Counts(notInstalled: 4, operational: 3, malfunction: 1, deactivated: 2));

        Assert.Equal(75m, summary.AvailabilityPercent);
    }

    [Theory]
    [InlineData(1, 2, 33.3)] // 33,333…
    [InlineData(2, 1, 66.7)] // 66,666…
    [InlineData(1, 7, 12.5)] // 12,5 exato
    [InlineData(1, 15, 6.3)] // 6,25: meio para cima
    public void Availability_RoundsToOneDecimalHalfUp_RN14(int operational, int malfunction, double expected)
    {
        var summary = AvailabilityCalculator.Calculate(Counts(operational: operational, malfunction: malfunction));

        Assert.Equal((decimal)expected, summary.AvailabilityPercent);
    }

    [Fact]
    public void Availability_ZeroDenominator_IsNull_RN14()
    {
        Assert.Null(AvailabilityCalculator.Calculate(Counts(notInstalled: 2, deactivated: 3)).AvailabilityPercent);
        Assert.Null(AvailabilityCalculator.Calculate(new Dictionary<EquipmentStatus, int>()).AvailabilityPercent);
    }

    [Fact]
    public void Unavailable_IsMalfunctionPlusMaintenancePlusStolen_RN15()
    {
        var summary = AvailabilityCalculator.Calculate(
            Counts(notInstalled: 100, operational: 50, malfunction: 2, maintenance: 3, deactivated: 100, stolen: 4));

        Assert.Equal(9, summary.Unavailable);
        Assert.Equal(259, summary.Total);
    }

    // RN-36 (A-27)

    [Theory]
    [InlineData(false, false, true, true)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, false, true)]
    [InlineData(true, true, true, true)]
    public void FilterScope_FourCases_RN36(bool equipmentFilters, bool agentFilters, bool includeEquipment, bool includeAgents)
    {
        Assert.Equal(new CentralFilterScope(includeEquipment, includeAgents), CentralFilterScope.From(equipmentFilters, agentFilters));
    }

    // RF-030, D-82

    [Fact]
    public void Severity_FullOrderOfTheWorstStatus_RF030_D82()
    {
        EquipmentStatus[] fromWorst =
        [
            EquipmentStatus.Stolen,
            EquipmentStatus.Malfunction,
            EquipmentStatus.UnderMaintenance,
            EquipmentStatus.Deactivated,
            EquipmentStatus.NotInstalled,
            EquipmentStatus.Operational,
        ];

        Assert.Equal([1, 2, 3, 4, 5, 6], fromWorst.Select(EquipmentStatusSeverity.Rank));
        Assert.Equal(EquipmentStatusSeverity.AgentInActivityRank, EquipmentStatusSeverity.Rank(EquipmentStatus.Operational));
    }

    // D-75, D-94

    [Fact]
    public void DutyShifts_CoverTheWholeDayWithoutOverlap_D75()
    {
        for (var minute = 0; minute < 24 * 60; minute++)
        {
            var time = new TimeOnly(minute / 60, minute % 60);
            Assert.Single(Enum.GetValues<DutyShift>(), shift => DutyShiftSchedule.Covers(shift, time));
        }
    }

    [Theory]
    [InlineData(5, 59, DutyShift.Night)]
    [InlineData(6, 0, DutyShift.Morning)]
    [InlineData(13, 59, DutyShift.Morning)]
    [InlineData(14, 0, DutyShift.Afternoon)]
    [InlineData(21, 59, DutyShift.Afternoon)]
    [InlineData(22, 0, DutyShift.Night)]
    [InlineData(0, 0, DutyShift.Night)]
    public void DutyShifts_Boundaries_D75(int hour, int minute, DutyShift expected)
    {
        Assert.Equal(expected, DutyShiftSchedule.ShiftAt(new TimeOnly(hour, minute)));
    }

    [Fact]
    public void AgentDutyShift_RecordsAgentShiftAndAuthor_D94()
    {
        var agentId = Guid.NewGuid();

        var shift = AgentDutyShift.Assign(agentId, DutyShift.Night, Now, " simulação ");

        Assert.IsAssignableFrom<IAppendOnly>(shift);
        Assert.Equal((agentId, DutyShift.Night, Now, "simulação"), (shift.AgentId, shift.Shift, shift.AssignedAt, shift.AssignedBy));
    }
}
