using Argos.Simulation.Generators;
using Argos.Simulation.Geography;
using Argos.Simulation.Models;
using Argos.Simulation.Movement;

namespace Argos.UnitTests.Simulation;

/// <summary>
/// Simulação da telemetria (US-11, RF-007, RF-015, RF-023; Art. IX.1, IX.3, IX.4; D-74, D-75, D-94; plan 16.5): rotas,
/// escala de referência e lote, todos determinísticos.
/// </summary>
public sealed class TelemetrySimulationTests
{
    private const int Seed = 42;

    private static readonly PrometeiaGeography Geography = PrometeiaGeography.Load();
    private static readonly PatrolRoutes Routes = new(Geography);
    private static readonly DateTimeOffset Instant = new(2026, 10, 26, 15, 0, 0, TimeSpan.Zero);
    private static readonly TelemetryBatchSettings Settings = new(Seed, TimeSpan.FromSeconds(60), TelemetrySpeeds.Default);

    // PatrolRoutes

    [Theory]
    [InlineData("Centro", 50)]
    [InlineData("Zona Norte", 39)]
    [InlineData("Zona Oeste", 31)]
    [InlineData("Zona Sul", 25)]
    [InlineData("Zona Leste", 18)]
    public void Routes_CandidatesOfEachRegion_MatchThePlan_ArtIX4(string region, int expected)
    {
        var zipCodes = Routes.CandidateZipCodes(region);

        Assert.Equal(expected, zipCodes.Count);
        Assert.Equal(zipCodes.Order(StringComparer.Ordinal), zipCodes);
    }

    [Fact]
    public void Routes_SameSeedKeyAndInstant_SamePoint_ArtIX3()
    {
        var first = PatrolRoutes.PositionAt(Routes.RouteFor(Seed, "Orbis/VT-0001", "Centro"), 30, Instant);
        var second = PatrolRoutes.PositionAt(new PatrolRoutes(Geography).RouteFor(Seed, "Orbis/VT-0001", "Centro"), 30, Instant);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Routes_OtherSeed_ChoosesOtherRoutes_ArtIX3()
    {
        var keys = Enumerable.Range(1, 20).Select(index => $"Orbis/VT-{index:0000}").ToArray();

        var withSeed = keys.Select(key => Routes.RouteFor(Seed, key, "Centro")).Select(route => (route.ZipCode, route.PhaseMeters));
        var withOther = keys.Select(key => Routes.RouteFor(Seed + 1, key, "Centro")).Select(route => (route.ZipCode, route.PhaseMeters));

        Assert.NotEqual(withSeed, withOther);
    }

    [Fact]
    public void StableHash_IsFnv1a64_ArtIX3()
    {
        // Valores de referência do FNV-1a de 64 bits.
        Assert.Equal(0xcbf29ce484222325UL, StableHash.Fnv1a64(string.Empty));
        Assert.Equal(0xaf63dc4c8601ec8cUL, StableHash.Fnv1a64("a"));
    }

    [Theory]
    [InlineData("Centro")]
    [InlineData("Zona Norte")]
    [InlineData("Zona Oeste")]
    [InlineData("Zona Sul")]
    [InlineData("Zona Leste")]
    public void Routes_PointIsOnTheRouteAndInsideTheRegionOfTheBase_ArtIX4(string region)
    {
        var boundary = Geography.Regions.Single(item => item.Name == region).Boundary;

        foreach (var key in Enumerable.Range(1, 10).Select(index => $"GCM/{index:0000}"))
        {
            var route = Routes.RouteFor(Seed, key, region);
            Assert.True(route.LengthMeters >= PatrolRoutes.MinimumSegmentMeters);

            for (var minute = 0; minute < 60; minute += 7)
            {
                var point = PatrolRoutes.PositionAt(route, 30, Instant.AddMinutes(minute));
                Assert.True(GeoMath.Contains(boundary, point), $"{key} fora de {region} no minuto {minute}.");
                Assert.True(GeoMath.DistanceToRingMeters(route.Path, point) < 0.5, $"{key} fora da rota no minuto {minute}.");
            }
        }
    }

    [Fact]
    public void Routes_GoAndComeBack_RepeatingAfterTheRoundTrip_D74()
    {
        var route = Routes.RouteFor(Seed, "Orbis/VT-0001", "Zona Sul");
        var speedKmh = 36.0; // 10 m/s
        var roundTrip = TimeSpan.FromSeconds(2 * route.LengthMeters / 10);

        var start = PatrolRoutes.PositionAt(route, speedKmh, Instant);
        var after = PatrolRoutes.PositionAt(route, speedKmh, Instant + roundTrip);

        Assert.True(GeoMath.DistanceMeters(start, after) < 0.5);
        Assert.NotEqual(start, PatrolRoutes.PositionAt(route, speedKmh, Instant.AddSeconds(10)));
    }

    // DutyRosterGenerator

    private static readonly RosterAgent[] RosterAgents =
    [
        new("GCM", "GCM-0001", "Orbis/VT-0001"),
        new("GCM", "GCM-0002", "Orbis/VT-0001"),
        new("GCM", "GCM-0003", null),
        new("GCM", "GCM-0004", null),
        new("GCM", "GCM-0005", "Orbis/VT-0002"),
        new("SMM", "SMM-0001", null),
        new("SMM", "SMM-0002", "Rota/MT-0001"),
        new("SMM", "SMM-0003", null),
        new("SMM", "SMM-0004", null),
        new("PM", "PM-0001", "Orbis/VT-0100"),
        new("PM", "PM-0002", "Orbis/VT-0100"),
    ];

    [Fact]
    public void Roster_IsDeterministicAndIndependentOfTheInputOrder_ArtIX3()
    {
        var roster = DutyRosterGenerator.Generate(Seed, RosterAgents);

        Assert.Equal(roster, DutyRosterGenerator.Generate(Seed, RosterAgents));
        Assert.Equal(roster, DutyRosterGenerator.Generate(Seed, RosterAgents.Reverse()));
        Assert.NotEqual(roster, DutyRosterGenerator.Generate(Seed + 1, RosterAgents));
    }

    [Fact]
    public void Roster_EachAgentInExactlyOneShift_D75()
    {
        var roster = DutyRosterGenerator.Generate(Seed, RosterAgents);

        Assert.Equal(
            RosterAgents.Select(agent => agent.RegistrationNumber).Order(StringComparer.Ordinal),
            roster.Select(entry => entry.RegistrationNumber).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Roster_CrewIsInTheShiftOfTheVehicle_D75()
    {
        var roster = DutyRosterGenerator.Generate(Seed, RosterAgents).ToDictionary(entry => entry.RegistrationNumber);

        foreach (var crew in RosterAgents.Where(agent => agent.VehicleKey is not null).GroupBy(agent => agent.VehicleKey))
        {
            Assert.Single(crew.Select(agent => roster[agent.RegistrationNumber].Shift).Distinct());
        }
    }

    [Fact]
    public void Roster_ShiftsBalancedByOrganization_AndStartingAtSeedMod3_D75()
    {
        var roster = DutyRosterGenerator.Generate(Seed, RosterAgents).ToDictionary(entry => entry.RegistrationNumber);

        // Unidades da GCM em ordem de chave: GCM-0003, GCM-0004, Orbis/VT-0001, Orbis/VT-0002 → 42 mod 3 = 0 (Manhã).
        Assert.Equal(
            [SimulatedDutyShift.Morning, SimulatedDutyShift.Afternoon, SimulatedDutyShift.Night, SimulatedDutyShift.Morning],
            new[] { "GCM-0003", "GCM-0004", "GCM-0001", "GCM-0005" }.Select(registration => roster[registration].Shift));

        foreach (var organization in RosterAgents.GroupBy(agent => agent.OrganizationAcronym))
        {
            var unitsPerShift = organization
                .GroupBy(agent => agent.VehicleKey ?? agent.RegistrationNumber)
                .GroupBy(unit => roster[unit.First().RegistrationNumber].Shift)
                .Select(shift => shift.Count())
                .ToArray();
            var totalUnits = organization.GroupBy(agent => agent.VehicleKey ?? agent.RegistrationNumber).Count();
            var counts = Enum.GetValues<SimulatedDutyShift>()
                .Select(shift => organization
                    .GroupBy(agent => agent.VehicleKey ?? agent.RegistrationNumber)
                    .Count(unit => roster[unit.First().RegistrationNumber].Shift == shift))
                .ToArray();

            Assert.Equal(totalUnits, unitsPerShift.Sum());
            Assert.True(counts.Max() - counts.Min() <= 1, $"{organization.Key}: {string.Join(", ", counts)}");
        }
    }

    // TelemetryBatchBuilder

    private static readonly Guid Patrol = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Scooter = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Camera = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly GeoCoordinate BasePoint = new(-22.2, -49.6);

    private static SimulatedTelemetryEquipment Equipment(
        Guid id, string typeKey, SimulatedEquipmentStatus status, bool crew = false, bool mobile = true) =>
        new(id, $"Orbis/{id:N}", typeKey, mobile ? SimulatedEquipmentCategory.Mobile : SimulatedEquipmentCategory.Fixed, status, crew,
            mobile ? BasePoint : null, mobile ? "Centro" : null);

    private static SimulatedTelemetryAgent Agent(
        int number, SimulatedAgentStatus status, bool scheduled, Guid? vehicle = null) =>
        new(Guid.Parse($"10000000-0000-0000-0000-{number:000000000000}"), $"GCM/{number:0000}", status, BasePoint, "Centro", vehicle, scheduled);

    private static TelemetryCycle Build(
        IReadOnlyList<SimulatedTelemetryEquipment> equipment,
        IReadOnlyList<SimulatedTelemetryAgent> agents,
        TelemetryCycleState? previous = null,
        DateTimeOffset? now = null) =>
        new TelemetryBatchBuilder(Routes).Build(
            new SimulatedTelemetryResources(equipment, agents), now ?? Instant, previous ?? TelemetryCycleState.Empty, Settings);

    [Fact]
    public void Batch_IsDeterministic_ArtIX3()
    {
        SimulatedTelemetryAgent[] agents = [Agent(1, SimulatedAgentStatus.OffDuty, scheduled: true, Patrol)];
        SimulatedTelemetryEquipment[] equipment =
            [Equipment(Patrol, "patrol-vehicle", SimulatedEquipmentStatus.Operational, crew: true)];

        var first = Build(equipment, agents).Batch;
        var second = Build(equipment, agents).Batch;

        Assert.Equal(first.StatusReports, second.StatusReports);
        Assert.Equal(first.Positions, second.Positions);
        Assert.Equal(first.Heartbeats, second.Heartbeats);
    }

    [Fact]
    public void Batch_FirstCycle_ReconcilesOnlyWhatContradictsTheRoster_D75()
    {
        SimulatedTelemetryAgent[] agents =
        [
            Agent(1, SimulatedAgentStatus.OffDuty, scheduled: true),
            Agent(2, SimulatedAgentStatus.OnBreak, scheduled: false),
            Agent(3, SimulatedAgentStatus.OnDuty, scheduled: true),
            Agent(4, SimulatedAgentStatus.OffDuty, scheduled: false),
            Agent(5, SimulatedAgentStatus.Responding, scheduled: false),
        ];

        var reports = Build([], agents).Batch.StatusReports;

        Assert.Equal(
            [(agents[0].Id, SimulatedAgentStatus.OnDuty), (agents[1].Id, SimulatedAgentStatus.OffDuty), (agents[4].Id, SimulatedAgentStatus.OffDuty)],
            reports.Select(report => (report.AgentId, report.Status)));
        Assert.All(reports, report => Assert.Equal(Instant, report.ReportedAt));
    }

    [Fact]
    public void Batch_ReportsOnlyAtTheEdges_AndRespectsManualChangesBetweenThem_D75()
    {
        var entering = Agent(1, SimulatedAgentStatus.OffDuty, scheduled: true);
        var leaving = Agent(2, SimulatedAgentStatus.OnBreak, scheduled: false);
        var manual = Agent(3, SimulatedAgentStatus.OnBreak, scheduled: true);
        var previous = new TelemetryCycleState(
            new Dictionary<Guid, bool> { [entering.Id] = false, [leaving.Id] = true, [manual.Id] = true },
            new Dictionary<Guid, DateTimeOffset>());

        var cycle = Build([], [entering, leaving, manual], previous);

        Assert.Equal(
            [(entering.Id, SimulatedAgentStatus.OnDuty), (leaving.Id, SimulatedAgentStatus.OffDuty)],
            cycle.Batch.StatusReports.Select(report => (report.AgentId, report.Status)));
        Assert.Equal(
            new Dictionary<Guid, bool> { [entering.Id] = true, [leaving.Id] = false, [manual.Id] = true },
            cycle.NextState.ScheduledOnDuty);
    }

    [Fact]
    public void Batch_OnLeave_IsNeverTouched_D75()
    {
        var onLeave = Agent(1, SimulatedAgentStatus.OnLeave, scheduled: true);
        var previous = new TelemetryCycleState(new Dictionary<Guid, bool> { [onLeave.Id] = false }, new Dictionary<Guid, DateTimeOffset>());

        Assert.Empty(Build([], [onLeave]).Batch.StatusReports);
        Assert.Empty(Build([], [onLeave], previous).Batch.StatusReports);
        Assert.Empty(Build([], [onLeave]).Batch.Positions);
    }

    [Fact]
    public void Batch_CrewEnteringTheShift_MovesWithTheVehicleInTheSameBatch_D74()
    {
        var crew = Agent(1, SimulatedAgentStatus.OffDuty, scheduled: true, Patrol);

        var positions = Build([Equipment(Patrol, "patrol-vehicle", SimulatedEquipmentStatus.Operational, crew: true)], [crew])
            .Batch.Positions.ToDictionary(position => position.ResourceId);

        Assert.NotEqual((BasePoint.Latitude, BasePoint.Longitude), (positions[Patrol].Latitude, positions[Patrol].Longitude));
        Assert.Equal((positions[Patrol].Latitude, positions[Patrol].Longitude), (positions[crew.Id].Latitude, positions[crew.Id].Longitude));
        Assert.Equal(SimulatedResourceKind.Agent, positions[crew.Id].ResourceKind);
    }

    [Fact]
    public void Batch_CrewVehicleWithoutCrewInActivity_StaysAtTheBase_D74()
    {
        var crew = Agent(1, SimulatedAgentStatus.OffDuty, scheduled: false, Patrol);

        var position = Assert.Single(
            Build([Equipment(Patrol, "patrol-vehicle", SimulatedEquipmentStatus.Operational, crew: true)], [crew]).Batch.Positions);

        Assert.Equal((Patrol, BasePoint.Latitude, BasePoint.Longitude), (position.ResourceId, position.Latitude, position.Longitude));
    }

    [Fact]
    public void Batch_AgentWhoseVehicleIsAtTheBase_WalksItsOwnRoute_D74()
    {
        var crew = Agent(1, SimulatedAgentStatus.OnDuty, scheduled: true, Patrol);

        var positions = Build([Equipment(Patrol, "patrol-vehicle", SimulatedEquipmentStatus.Malfunction, crew: true)], [crew])
            .Batch.Positions.ToDictionary(position => position.ResourceId);
        var walk = PatrolRoutes.PositionAt(Routes.RouteFor(Seed, crew.Key, "Centro"), TelemetrySpeeds.Default.OnFootKmh, Instant);

        Assert.Equal((BasePoint.Latitude, BasePoint.Longitude), (positions[Patrol].Latitude, positions[Patrol].Longitude));
        Assert.Equal((walk.Latitude, walk.Longitude), (positions[crew.Id].Latitude, positions[crew.Id].Longitude));
    }

    [Theory]
    [InlineData(SimulatedEquipmentStatus.Operational, "route")]
    [InlineData(SimulatedEquipmentStatus.Malfunction, "base")]
    [InlineData(SimulatedEquipmentStatus.UnderMaintenance, "base")]
    [InlineData(SimulatedEquipmentStatus.Deactivated, "none")]
    [InlineData(SimulatedEquipmentStatus.Stolen, "none")]
    [InlineData(SimulatedEquipmentStatus.NotInstalled, "none")]
    public void Batch_MobileByStatus_D74(SimulatedEquipmentStatus status, string expected)
    {
        var positions = Build([Equipment(Scooter, "electric-scooter", status)], []).Batch.Positions;

        var actual = positions.Count == 0
            ? "none"
            : positions[0].Latitude == BasePoint.Latitude && positions[0].Longitude == BasePoint.Longitude ? "base" : "route";
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(SimulatedEquipmentStatus.Operational, true)]
    [InlineData(SimulatedEquipmentStatus.Malfunction, true)]
    [InlineData(SimulatedEquipmentStatus.UnderMaintenance, true)]
    [InlineData(SimulatedEquipmentStatus.NotInstalled, false)]
    [InlineData(SimulatedEquipmentStatus.Deactivated, false)]
    [InlineData(SimulatedEquipmentStatus.Stolen, false)]
    public void Batch_FixedHeartbeatOnlyInTheStatusesThatCommunicate_D76(SimulatedEquipmentStatus status, bool expected)
    {
        var batch = Build([Equipment(Camera, "surveillance-camera", status, mobile: false)], []).Batch;

        Assert.Equal(expected, batch.Heartbeats.Count == 1);
        Assert.Empty(batch.Positions);
    }

    [Fact]
    public void Batch_FixedHeartbeat_FirstAtStartupThenOnlyAfterTheInterval_D76()
    {
        SimulatedTelemetryEquipment[] camera = [Equipment(Camera, "surveillance-camera", SimulatedEquipmentStatus.Operational, mobile: false)];

        var first = Build(camera, []);
        var tooSoon = Build(camera, [], first.NextState, Instant.AddSeconds(30));
        var due = Build(camera, [], tooSoon.NextState, Instant.AddSeconds(60));

        Assert.Equal([Instant], first.Batch.Heartbeats.Select(heartbeat => heartbeat.RecordedAt));
        Assert.Empty(tooSoon.Batch.Heartbeats);
        Assert.Equal([Instant.AddSeconds(60)], due.Batch.Heartbeats.Select(heartbeat => heartbeat.RecordedAt));
    }

    [Fact]
    public void Speeds_ByTypeKey_D74()
    {
        Assert.Equal(
            [30d, 30d, 30d, 35d, 12d, 30d],
            new[]
            {
                "patrol-vehicle", "passenger-transport-vehicle", "parking-enforcement-vehicle", "traffic-agent-motorcycle",
                "electric-scooter", "custom-type",
            }.Select(TelemetrySpeeds.Default.ForType));
    }
}
