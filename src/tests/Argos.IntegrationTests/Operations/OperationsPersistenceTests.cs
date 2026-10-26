using System.Net;
using Argos.Application.Common.Abstractions;
using Argos.Application.Common.Exceptions;
using Argos.Application.Operations;
using Argos.Contracts.Operations;
using Argos.Contracts.Telemetry;
using Argos.Domain.Operations.Alerts;
using Argos.Domain.Telemetry;
using Argos.Infrastructure.Persistence;
using Argos.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DomainEquipmentStatus = Argos.Domain.Inventory.Equipments.EquipmentStatus;
using DomainResourceKind = Argos.Domain.Telemetry.ResourceKind;

namespace Argos.IntegrationTests.Operations;

/// <summary>
/// Persistência da Fase 4 (T113; Art. V.1; RN-19, RN-23; D-70; plan 16.9): a leitura de telemetria é append-only e o
/// índice único parcial recusa um segundo alerta aberto da mesma causa no mesmo recurso, com o 409 de 16.9.
/// </summary>
public sealed class OperationsPersistenceTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TelemetryReading_CannotBeChanged_ArtV1()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var admin = Fixture.CreateUserClient(Fixture.Tokens.For(Contracts.Access.UserRoles.Administrator));
        var camera = await admin.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);
        using var system = Fixture.CreateSystemClient();
        var response = await system.PostJsonAsync(TelemetryHttp.BatchesPath, TelemetryHttp.EmptyBatch() with
        {
            Heartbeats = [new TelemetryHeartbeatItem { EquipmentId = camera.Id, RecordedAt = DateTimeOffset.UtcNow }],
        }, Cancellation);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = DbContextOf(scope.ServiceProvider);
        var reading = await dbContext.Set<TelemetryReading>().SingleAsync(Cancellation);
        dbContext.Entry(reading).Property(nameof(TelemetryReading.Author)).CurrentValue = "outro";

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync(Cancellation));
        Assert.Equal("Registros de histórico não podem ser alterados nem removidos (TelemetryReading).", refused.Message);
    }

    [Fact]
    public async Task SecondOpenAlertOfTheSameCause_IsRefusedByTheIndex_RN19()
    {
        var resourceId = Guid.NewGuid();
        var definition = AlertCatalog.ForStatusChange(null, DomainEquipmentStatus.Malfunction)!;
        await SaveAlertAsync(Alert.Open(definition, DomainResourceKind.Equipment, resourceId, DateTimeOffset.UtcNow, "admin"));

        var conflict = await Assert.ThrowsAsync<ConflictException>(() => SaveAlertAsync(
            Alert.Open(definition, DomainResourceKind.Equipment, resourceId, DateTimeOffset.UtcNow, "operador")));

        Assert.Equal(EquipmentAlertRaiser.DuplicateOpenAlertMessage, conflict.Message);
        Assert.Equal("Já existe um alerta aberto para essa causa neste recurso.", conflict.Message);
        Assert.Equal(1, await Fixture.CountAsync("SELECT count(*) FROM alerts", Cancellation));
    }

    private async Task SaveAlertAsync(Alert alert)
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IAlertRepository>().Add(alert);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Cancellation);
    }

    /// <summary>O <c>DbContext</c> é interno da Infraestrutura (Art. III.5); o teste o obtém do contêiner pelo tipo.</summary>
    private static DbContext DbContextOf(IServiceProvider services)
    {
        var type = typeof(IDatabaseMigrator).Assembly.GetType("Argos.Infrastructure.Persistence.ArgosDbContext", throwOnError: true)!;
        return (DbContext)services.GetRequiredService(type);
    }
}
