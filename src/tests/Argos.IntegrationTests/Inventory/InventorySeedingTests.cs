using Argos.Application.Common.Abstractions;
using Argos.Application.Common.Exceptions;
using Argos.Application.Inventory;
using Argos.Application.Seeding.Inventory;
using Argos.Contracts.Common;
using Argos.Contracts.Inventory;
using Argos.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using EquipmentStatus = Argos.Domain.Inventory.Equipments.EquipmentStatus;
using EquipmentTypeCatalog = Argos.Domain.Inventory.Types.EquipmentTypeCatalog;

namespace Argos.IntegrationTests.Inventory;

/// <summary>
/// Carga do Inventário contra o PostgreSQL real (T065; D-56; plan 15.11.4): cada versão é gravada na mesma transação do
/// seu registro no histórico do seed, então uma falha no meio da gravação não deixa linhas nem registro, e a carga pode
/// ser repetida depois. A falha é forçada pelo banco (índice único), no meio do <c>SaveChanges</c>, depois de linhas
/// válidas já enviadas.
/// </summary>
public sealed class InventorySeedingTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    private const int Seed = 42;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ReferenceFailingMidSave_LeavesNoRowsAndNoSeedRecord_T065()
    {
        // "Centro" e "CENTRO" têm a mesma chave normalizada: o índice único de bairros recusa o segundo na gravação.
        var reference = InventorySeedData.Reference(
        [
            new NeighborhoodSeed(InventorySeedData.Saint, "Zona Única", new SeedPoint(-22.24, -49.64)),
            new NeighborhoodSeed(InventorySeedData.Downtown, "Zona Única", new SeedPoint(-22.26, -49.66)),
            new NeighborhoodSeed("CENTRO", "Zona Única", new SeedPoint(-22.27, -49.67)),
        ]);

        await Assert.ThrowsAsync<ConflictException>(() => SeedReferenceAsync(reference));

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IInventoryQueries>();
        Assert.Null(await queries.GetMunicipalityAsync(Cancellation));
        Assert.Empty(await queries.ListRegionsAsync(includeInactive: true, Cancellation));
        Assert.Empty(await queries.ListNeighborhoodsAsync(regionId: null, includeInactive: true, Cancellation));
        Assert.Empty(await queries.ListEquipmentTypesAsync(includeInactive: true, Cancellation));
        Assert.Null(await FindAppliedSeedAsync(InventorySeed.ReferenceVersion));

        var retry = await SeedReferenceAsync(InventorySeedData.Reference());
        Assert.False(retry.AlreadyApplied);
    }

    [Fact]
    public async Task CatalogFailingMidSave_LeavesNoRowsAndNoSeedRecord_ThenLoadsOnRetry_T065()
    {
        await SeedReferenceAsync(InventorySeedData.Reference());
        var valid = new[]
        {
            Camera("SEED-001", EquipmentStatus.Operational, new PeriodicMaintenanceSeed(-3, 30, "Revisão mensal")),
            Camera("SEED-002", EquipmentStatus.Malfunction, null),
            Camera("SEED-003", EquipmentStatus.Operational, null, new StatusChangeSeed(EquipmentStatus.Stolen, TimeSpan.FromDays(2), "Furto registrado")),
        };

        // Mesma marca e número de série do primeiro: a carga não confere a RN-01, e o índice único recusa no banco.
        var failing = new EquipmentCatalogSeed([.. valid, Camera("SEED-001", EquipmentStatus.Operational, null)]);
        await Assert.ThrowsAsync<ConflictException>(() => SeedCatalogAsync(failing));

        Assert.Equal(0, (await SearchAllAsync()).TotalCount);
        Assert.Null(await FindAppliedSeedAsync(InventorySeed.CatalogVersion));
        Assert.Equal(Seed, await FindAppliedSeedAsync(InventorySeed.ReferenceVersion));

        var retry = await SeedCatalogAsync(new EquipmentCatalogSeed(valid));
        Assert.False(retry.AlreadyApplied);
        Assert.Equal(3, retry.Counts["equipment"]);

        var again = await SeedCatalogAsync(new EquipmentCatalogSeed(valid));
        Assert.True(again.AlreadyApplied);

        var all = await SearchAllAsync();
        Assert.Equal(3, all.TotalCount);
        Assert.Equal(Seed, await FindAppliedSeedAsync(InventorySeed.CatalogVersion));
    }

    private static EquipmentSeed Camera(
        string serialNumber,
        EquipmentStatus status,
        PeriodicMaintenanceSeed? periodic,
        StatusChangeSeed? change = null) =>
        new(
            EquipmentTypeCatalog.SurveillanceCamera,
            serialNumber,
            "Ômega Vision",
            "OV-200",
            InventorySeedData.Organization,
            new LocationSeed("16985001", "Rua das Flores", InventorySeedData.Saint, "número 181", -22.241, -49.641),
            null,
            new Dictionary<string, object?>
            {
                ["resolution"] = "full-hd-1080p",
                ["colorMode"] = "color",
                ["nightVision"] = true,
                ["panTiltZoom"] = false,
            },
            TimeSpan.FromDays(180),
            status,
            periodic,
            change);

    private async Task<SeedResult> SeedReferenceAsync(InventoryReferenceSeed reference)
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<InventoryReferenceSeeder>()
            .SeedAsync(reference, Seed, InventorySeedData.Anchor.AddDays(-200), Cancellation);
    }

    private async Task<SeedResult> SeedCatalogAsync(EquipmentCatalogSeed catalog)
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EquipmentCatalogSeeder>()
            .SeedAsync(catalog, Seed, InventorySeedData.Anchor, Cancellation);
    }

    private async Task<PagedResult<EquipmentSummary>> SearchAllAsync()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IInventoryQueries>()
            .SearchEquipmentAsync(new EquipmentSearchQuery(), DateOnly.FromDateTime(InventorySeedData.Anchor.UtcDateTime), 7, Cancellation);
    }

    private async Task<int?> FindAppliedSeedAsync(string version)
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISeedLedger>().FindAppliedSeedAsync(version, Cancellation);
    }
}
