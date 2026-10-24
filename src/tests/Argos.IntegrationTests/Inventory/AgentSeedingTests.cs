using Argos.Application.Common.Abstractions;
using Argos.Application.Inventory.Agents;
using Argos.Application.Inventory.Equipments;
using Argos.Application.Seeding.Inventory;
using Argos.Contracts.Common;
using Argos.Contracts.Inventory;
using Argos.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using DomainAgentRole = Argos.Domain.Inventory.Agents.AgentRole;
using DomainEquipmentStatus = Argos.Domain.Inventory.Equipments.EquipmentStatus;
using EquipmentTypeCatalog = Argos.Domain.Inventory.Types.EquipmentTypeCatalog;

namespace Argos.IntegrationTests.Inventory;

/// <summary>
/// Cargas da Fase 3 contra o PostgreSQL real (T088; US-06, US-07, US-09, RN-35; Art. IX.2, IX.3, XII.7; D-56; plan
/// 15.11.4): sobre um banco com a referência e o catálogo da Fase 2, <c>inventory-agents-v1</c> e
/// <c>inventory-inactivations-v1</c> carregam agentes, vínculos e inativações pelas operações do Domínio, sem tocar no
/// resto, e uma segunda execução não grava nada.
/// </summary>
public sealed class AgentSeedingTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    private const int Seed = 42;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AgentsAndInactivations_OverAPhaseTwoDatabase_LoadOnceWithoutTouchingTheRest_D56_ArtXII7()
    {
        await SeedPhaseTwoAsync(motorcycleTwoStatus: null);
        var phaseTwo = await DatabaseFingerprint.TakeAsync(Fixture.ConnectionString, Cancellation);

        var agents = await SeedAgentsAsync();
        var afterAgents = await DatabaseFingerprint.TakeAsync(Fixture.ConnectionString, Cancellation);

        Assert.False(agents.AlreadyApplied);
        Assert.Equal(
            new Dictionary<string, int> { ["agents"] = 3, ["assignments"] = 2, ["assignmentsSkipped"] = 0 }, agents.Counts);
        foreach (var table in new[] { "equipment", "maintenances", "equipment_status_changes", "inventory_changes", "operational_bases" })
        {
            Assert.Equal(phaseTwo[table], afterAgents[table]);
        }

        Assert.Equal((3, 2), (afterAgents["agents"].Rows, afterAgents["agent_assignments"].Rows));

        var inactivations = await SeedInactivationsAsync();
        Assert.Equal(
            new Dictionary<string, int> { ["equipment"] = 1, ["agents"] = 1, ["inactivationsSkipped"] = 0 }, inactivations.Counts);

        var inactiveAgents = await SearchAgentsAsync(ActivityFilter.Inactive);
        var agentTwo = Assert.Single(inactiveAgents.Items);
        Assert.Equal(("AT-0003", AgentStatus.OnLeave), (agentTwo.RegistrationNumber, agentTwo.Status));
        Assert.Equal(2, (await SearchAgentsAsync(ActivityFilter.Active)).TotalCount);

        var before = await DatabaseFingerprint.TakeAsync(Fixture.ConnectionString, Cancellation);
        Assert.True((await SeedAgentsAsync()).AlreadyApplied);
        Assert.True((await SeedInactivationsAsync()).AlreadyApplied);
        Assert.Equal(before, await DatabaseFingerprint.TakeAsync(Fixture.ConnectionString, Cancellation));
    }

    [Fact]
    public async Task AssignmentRefusedByTheCurrentStatus_IsSkippedAndCounted_D62()
    {
        await SeedPhaseTwoAsync(motorcycleTwoStatus: DomainEquipmentStatus.Deactivated);

        var result = await SeedAgentsAsync();

        Assert.Equal(
            new Dictionary<string, int> { ["agents"] = 3, ["assignments"] = 1, ["assignmentsSkipped"] = 1 }, result.Counts);
    }

    [Fact]
    public async Task Agents_BeforeTheCatalog_StopWithoutWriting()
    {
        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<InventoryReferenceSeeder>().SeedAsync(
                InventorySeedData.Reference(), Seed, InventorySeedData.Anchor.AddDays(-200), Cancellation);
        }

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SeedAgentsAsync());

        Assert.Equal($"Aplique a carga {InventorySeed.CatalogVersion} antes de {InventorySeed.AgentsVersion}.", error.Message);
        await using var check = Fixture.Factory.Services.CreateAsyncScope();
        Assert.Null(await check.ServiceProvider.GetRequiredService<ISeedLedger>()
            .FindAppliedSeedAsync(InventorySeed.AgentsVersion, Cancellation));
    }

    [Fact]
    public async Task InactivationRefusedByRN35_IsSkippedAndCounted_AndTheVersionIsRecorded()
    {
        await SeedPhaseTwoAsync(motorcycleTwoStatus: null);
        await SeedAgentsAsync();

        // MOTO-001 está Operacional: fora de Desativado, Furtado e Ainda não instalado, a RN-35 recusa a inativação.
        var result = await SeedInactivationsAsync(new InactivationSeed(
            [new EquipmentInactivationSeed("Motéris", "MOTO-001", "Baixa patrimonial.", TimeSpan.FromDays(1))], []));

        Assert.False(result.AlreadyApplied);
        Assert.Equal(
            new Dictionary<string, int> { ["equipment"] = 0, ["agents"] = 0, ["inactivationsSkipped"] = 1 }, result.Counts);
        await using var check = Fixture.Factory.Services.CreateAsyncScope();
        Assert.NotNull(await check.ServiceProvider.GetRequiredService<ISeedLedger>()
            .FindAppliedSeedAsync(InventorySeed.InactivationsVersion, Cancellation));
        Assert.Equal(0, (await SearchEquipmentAsync(ActivityFilter.Inactive)).TotalCount);
    }

    [Fact]
    public async Task Inactivations_BeforeTheAgents_StopWithoutWriting()
    {
        await SeedPhaseTwoAsync(motorcycleTwoStatus: null);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SeedInactivationsAsync());

        Assert.Equal("Aplique a carga inventory-agents-v1 antes de inventory-inactivations-v1.", error.Message);
        await using var check = Fixture.Factory.Services.CreateAsyncScope();
        Assert.Null(await check.ServiceProvider.GetRequiredService<ISeedLedger>()
            .FindAppliedSeedAsync(InventorySeed.InactivationsVersion, Cancellation));
        Assert.Equal(0, (await SearchEquipmentAsync(ActivityFilter.Inactive)).TotalCount);
    }

    /// <summary>Referência pequena e um catálogo com duas motos e uma câmera Desativada, como um banco da Fase 2.</summary>
    private async Task SeedPhaseTwoAsync(DomainEquipmentStatus? motorcycleTwoStatus)
    {
        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<InventoryReferenceSeeder>().SeedAsync(
                InventorySeedData.Reference(), Seed, InventorySeedData.Anchor.AddDays(-200), Cancellation);
        }

        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<EquipmentCatalogSeeder>().SeedAsync(
                new EquipmentCatalogSeed(
                [
                    Motorcycle("MOTO-001", null),
                    Motorcycle(
                        "MOTO-002",
                        motorcycleTwoStatus is { } status ? new StatusChangeSeed(status, TimeSpan.FromDays(2), "Baixa por avaria.") : null),
                    new EquipmentSeed(
                        EquipmentTypeCatalog.SurveillanceCamera,
                        "CAM-001",
                        "Ômega Vision",
                        "OV-200",
                        InventorySeedData.Organization,
                        new LocationSeed("16985001", "Rua das Flores", InventorySeedData.Hesperia, "número 181", -22.241, -49.641),
                        null,
                        new Dictionary<string, object?>
                        {
                            ["resolution"] = "full-hd-1080p", ["colorMode"] = "color", ["nightVision"] = true, ["panTiltZoom"] = false,
                        },
                        TimeSpan.FromDays(180),
                        DomainEquipmentStatus.Operational,
                        new PeriodicMaintenanceSeed(30, 180, "Revisão semestral"),
                        new StatusChangeSeed(DomainEquipmentStatus.Deactivated, TimeSpan.FromDays(3), "Fim do contrato.")),
                ]),
                Seed,
                InventorySeedData.Anchor,
                Cancellation);
        }
    }

    private static EquipmentSeed Motorcycle(string serialNumber, StatusChangeSeed? change) =>
        new(
            EquipmentTypeCatalog.TrafficAgentMotorcycle,
            serialNumber,
            "Motéris",
            "MT 300",
            InventorySeedData.Organization,
            null,
            InventorySeedData.Base,
            new Dictionary<string, object?> { ["licensePlate"] = "ABC1D23", ["prefix"] = $"M-{serialNumber}" },
            TimeSpan.FromDays(180),
            DomainEquipmentStatus.Operational,
            null,
            change);

    private async Task<SeedResult> SeedAgentsAsync()
    {
        static AgentSeed Agent(string registration, string displayName) => new(
            $"Agente Fictício {displayName}", displayName, registration, DomainAgentRole.TrafficAgent,
            InventorySeedData.Organization, null, InventorySeedData.Base, TimeSpan.FromDays(170));

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AgentCatalogSeeder>().SeedAsync(
            new AgentCatalogSeed(
                [Agent("AT-0001", "Falcão"), Agent("AT-0002", "Condor"), Agent("AT-0003", "Gavião")],
                [
                    new AssignmentSeed(InventorySeedData.Organization, "AT-0001", "Motéris", "MOTO-001", TimeSpan.FromDays(30)),
                    new AssignmentSeed(InventorySeedData.Organization, "AT-0002", "motéris", "moto-002", TimeSpan.FromDays(30)),
                ]),
            Seed,
            InventorySeedData.Anchor,
            Cancellation);
    }

    private Task<SeedResult> SeedInactivationsAsync() =>
        SeedInactivationsAsync(new InactivationSeed(
            [new EquipmentInactivationSeed("Ômega Vision", "CAM-001", "Baixa patrimonial.", TimeSpan.FromDays(1))],
            [new AgentInactivationSeed(InventorySeedData.Organization, "AT-0003", "Desligamento.", TimeSpan.FromDays(5), TimeSpan.FromDays(2))]));

    private async Task<SeedResult> SeedInactivationsAsync(InactivationSeed inactivations)
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<InactivationSeeder>().SeedAsync(
            inactivations, Seed, InventorySeedData.Anchor, Cancellation);
    }

    private async Task<PagedResult<EquipmentSummary>> SearchEquipmentAsync(ActivityFilter activity)
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EquipmentQueryService>()
            .SearchAsync(new EquipmentSearchQuery { Activity = activity }, Cancellation);
    }

    private async Task<PagedResult<AgentSummary>> SearchAgentsAsync(ActivityFilter activity)
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AgentQueryService>()
            .SearchAsync(new AgentSearchQuery { Activity = activity }, Cancellation);
    }
}
