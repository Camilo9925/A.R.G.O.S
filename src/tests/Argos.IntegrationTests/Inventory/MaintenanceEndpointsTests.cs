using System.Net;
using Argos.Contracts.Access;
using Argos.Contracts.Common;
using Argos.Contracts.Inventory;
using Argos.IntegrationTests.Infrastructure;
using static Argos.IntegrationTests.Infrastructure.InventoryHttp;

namespace Argos.IntegrationTests.Inventory;

/// <summary>
/// Endpoints de manutenção (T067; US-05, RN-07 a RN-11; plan 15.4, 15.14, 15.16): lista, registro e conclusão. Caminho
/// feliz de cada rota (Art. XII.3) e os erros com o campo exato ou o motivo no <c>detail</c> (Art. XII.4).
/// </summary>
public sealed class MaintenanceEndpointsTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    private const string Justification = "Ocorrência registrada pela guarda.";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // Lista

    [Fact]
    public async Task List_ReturnsOpenAndClosed_FilteredByState_ToManager_US05()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        var today = await Fixture.TodayAsync(Cancellation);
        using var admin = Client(UserRoles.Administrator);
        var created = await admin.CreateEquipmentAsync(
            scenario.Camera("CAM-001", EquipmentStatus.UnderMaintenance, InventoryScenario.Periodic(today.AddDays(-1))),
            Cancellation);
        await CompleteAsync(admin, created.Id, created.OpenMaintenances.Single().Id, MaintenanceResult.Success, "Lente limpa.");
        using var manager = Client(UserRoles.Manager);

        var all = await ListAsync(manager, created.Id);
        var open = await ListAsync(manager, created.Id, "?state=Open");
        var completed = await ListAsync(manager, created.Id, "?state=Completed");

        Assert.Equal(2, all.Length);
        var next = Assert.Single(open);
        Assert.Equal((MaintenanceOrigin.PeriodicRescheduling, today.AddDays(30), MaintenanceNeed.Scheduled),
            (next.Origin, next.DueDate, next.Need));
        var done = Assert.Single(completed);
        Assert.Equal((MaintenanceResult.Success, "Lente limpa.", "admin"), (done.Result, done.CompletionNotes, done.CompletedBy));
        Assert.NotNull(done.CompletedAt);
        Assert.Null(done.Need);
    }

    [Fact]
    public async Task List_UnknownEquipment_IsNotFound_ByOperator_IsForbidden()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var admin = Client(UserRoles.Administrator);
        var created = await admin.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);
        using var operatorClient = Client(UserRoles.Operator);

        await ProblemAssert.IsNotFoundAsync(
            await admin.GetAsync($"{Equipment}/{Guid.NewGuid()}/maintenances", Cancellation),
            "Equipamento não encontrado.",
            Cancellation);
        await ProblemAssert.IsForbiddenAsync(
            await operatorClient.GetAsync($"{Equipment}/{created.Id}/maintenances", Cancellation), Cancellation);
    }

    // Registro (RN-08)

    [Fact]
    public async Task Register_Scheduled_Returns201WithTheNeed_AndShowsInTheDetails_RN10()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        var today = await Fixture.TodayAsync(Cancellation);
        using var client = Client(UserRoles.Administrator);
        var created = await client.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);

        var response = await client.PostJsonAsync(
            $"{Equipment}/{created.Id}/maintenances",
            new RegisterMaintenanceRequest { Kind = MaintenanceKind.Scheduled, DueDate = today.AddDays(3), Description = "Troca de lente" },
            Cancellation);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var maintenance = await response.ReadAsync<MaintenanceData>(Cancellation);
        Assert.Equal(
            (MaintenanceKind.Scheduled, MaintenanceState.Open, today.AddDays(3), MaintenanceNeed.Upcoming, "Troca de lente", "admin"),
            (maintenance.Kind, maintenance.State, maintenance.DueDate, maintenance.Need, maintenance.Description, maintenance.OpenedBy));
        var details = await client.GetEquipmentAsync(created.Id, Cancellation);
        Assert.Equal(MaintenanceNeed.Upcoming, details.MaintenanceNeed);
        Assert.Equal(maintenance.Id, Assert.Single(details.OpenMaintenances).Id);
    }

    [Fact]
    public async Task Register_ScheduledWithoutDueDate_IsBadRequestOnDueDate_RN08()
    {
        var created = await CreateCameraAsync();
        using var client = Client(UserRoles.Administrator);

        var response = await client.PostJsonAsync(
            $"{Equipment}/{created.Id}/maintenances", new RegisterMaintenanceRequest { Kind = MaintenanceKind.Scheduled }, Cancellation);

        var errors = await ProblemAssert.IsValidationAsync(response, Cancellation);
        Assert.Equal(["dueDate"], errors.Keys);
        Assert.Equal(["Informe a data prevista."], errors["dueDate"]);
    }

    [Fact]
    public async Task Register_ScheduledInThePast_IsBadRequestOnDueDate_RN08()
    {
        var created = await CreateCameraAsync();
        var today = await Fixture.TodayAsync(Cancellation);
        using var client = Client(UserRoles.Administrator);

        var response = await client.PostJsonAsync(
            $"{Equipment}/{created.Id}/maintenances", InventoryScenario.Scheduled(today.AddDays(-1)), Cancellation);

        var errors = await ProblemAssert.IsValidationAsync(response, Cancellation);
        Assert.Equal(["dueDate"], errors.Keys);
        Assert.Equal(["A data prevista não pode ser anterior a hoje."], errors["dueDate"]);
    }

    [Fact]
    public async Task Register_PeriodicWithoutPeriodicity_IsBadRequestOnPeriodicityDays_RN08()
    {
        var created = await CreateCameraAsync();
        var today = await Fixture.TodayAsync(Cancellation);
        using var client = Client(UserRoles.Administrator);

        var response = await client.PostJsonAsync(
            $"{Equipment}/{created.Id}/maintenances",
            new RegisterMaintenanceRequest { Kind = MaintenanceKind.Periodic, DueDate = today.AddDays(10) },
            Cancellation);

        var errors = await ProblemAssert.IsValidationAsync(response, Cancellation);
        Assert.Equal(["periodicityDays"], errors.Keys);
        Assert.Equal(["Informe a periodicidade, em dias."], errors["periodicityDays"]);
    }

    [Fact]
    public async Task Register_ImmediateWithDueDate_IsBadRequestOnDueDate_RN08()
    {
        var created = await CreateCameraAsync();
        var today = await Fixture.TodayAsync(Cancellation);
        using var client = Client(UserRoles.Administrator);

        var response = await client.PostJsonAsync(
            $"{Equipment}/{created.Id}/maintenances",
            new RegisterMaintenanceRequest { Kind = MaintenanceKind.Immediate, DueDate = today.AddDays(1) },
            Cancellation);

        var errors = await ProblemAssert.IsValidationAsync(response, Cancellation);
        Assert.Equal(["dueDate"], errors.Keys);
        Assert.Equal(["A manutenção imediata não tem data prevista."], errors["dueDate"]);
    }

    [Fact]
    public async Task Register_SecondImmediate_IsConflictWithTheReason_RN07()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var created = await client.CreateEquipmentAsync(scenario.Camera("CAM-001", EquipmentStatus.Malfunction), Cancellation);

        var response = await client.PostJsonAsync(
            $"{Equipment}/{created.Id}/maintenances", new RegisterMaintenanceRequest { Kind = MaintenanceKind.Immediate }, Cancellation);

        await ProblemAssert.IsBusinessRuleAsync(response, "Já existe uma manutenção imediata aberta.", Cancellation);
    }

    [Theory]
    [InlineData(EquipmentStatus.Stolen)]
    [InlineData(EquipmentStatus.Deactivated)]
    public async Task Register_OnStolenOrDeactivated_IsConflictWithTheReason_RN11(EquipmentStatus status)
    {
        var created = await CreateCameraAsync();
        using var client = Client(UserRoles.Administrator);
        await client.ChangeStatusAsync(created.Id, status, Justification, Cancellation);

        var response = await client.PostJsonAsync(
            $"{Equipment}/{created.Id}/maintenances", new RegisterMaintenanceRequest { Kind = MaintenanceKind.Immediate }, Cancellation);

        await ProblemAssert.IsBusinessRuleAsync(response, "Equipamento desativado ou furtado não recebe manutenção.", Cancellation);
    }

    [Fact]
    public async Task Register_UnknownEquipmentOrByManager_IsNotFoundOrForbidden()
    {
        var created = await CreateCameraAsync();
        using var admin = Client(UserRoles.Administrator);
        using var manager = Client(UserRoles.Manager);
        var request = new RegisterMaintenanceRequest { Kind = MaintenanceKind.Immediate };

        await ProblemAssert.IsNotFoundAsync(
            await admin.PostJsonAsync($"{Equipment}/{Guid.NewGuid()}/maintenances", request, Cancellation),
            "Equipamento não encontrado.",
            Cancellation);
        await ProblemAssert.IsForbiddenAsync(
            await manager.PostJsonAsync($"{Equipment}/{created.Id}/maintenances", request, Cancellation), Cancellation);
    }

    // Conclusão (RN-09, RN-11)

    [Fact]
    public async Task Complete_PeriodicWithSuccess_ReturnsToOperational_AndSchedulesTheNext_RN09_RN11()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        var today = await Fixture.TodayAsync(Cancellation);
        using var client = Client(UserRoles.Administrator);
        var created = await client.CreateEquipmentAsync(
            scenario.Camera("CAM-001", EquipmentStatus.UnderMaintenance, InventoryScenario.Periodic(today.AddDays(-2), 45)),
            Cancellation);
        var periodic = created.OpenMaintenances.Single();

        var completion = await CompleteAsync(client, created.Id, periodic.Id, MaintenanceResult.Success, "Aferição feita.");

        Assert.Equal(EquipmentStatus.Operational, completion.Status);
        Assert.Equal((periodic.Id, MaintenanceState.Completed, MaintenanceResult.Success, "Aferição feita."),
            (completion.Completed.Id, completion.Completed.State, completion.Completed.Result, completion.Completed.CompletionNotes));
        var next = completion.NextPeriodic!;
        Assert.Equal((MaintenanceKind.Periodic, today.AddDays(45), 45, MaintenanceOrigin.PeriodicRescheduling, periodic.Id),
            (next.Kind, next.DueDate, next.PeriodicityDays, next.Origin, next.PreviousMaintenanceId));

        var history = await (await client.GetAsync($"{Equipment}/{created.Id}/status-changes", Cancellation))
            .ReadAsync<PagedResult<StatusChangeData>>(Cancellation);
        var change = history.Items[0];
        Assert.Equal((EquipmentStatus.UnderMaintenance, EquipmentStatus.Operational, StatusChangeOrigin.MaintenanceCompletion, periodic.Id),
            (change.FromStatus, change.ToStatus, change.Origin, change.MaintenanceId));
    }

    [Fact]
    public async Task Complete_WithFailure_GoesToMalfunctionAndOpensTheImmediate_RN11()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        var today = await Fixture.TodayAsync(Cancellation);
        using var client = Client(UserRoles.Administrator);
        var created = await client.CreateEquipmentAsync(
            scenario.Camera("CAM-001", initialMaintenance: InventoryScenario.Scheduled(today)), Cancellation);

        var completion = await CompleteAsync(
            client, created.Id, created.OpenMaintenances.Single().Id, MaintenanceResult.Failure, "Placa queimada.");

        Assert.Equal(EquipmentStatus.Malfunction, completion.Status);
        Assert.Null(completion.NextPeriodic);
        var details = await client.GetEquipmentAsync(created.Id, Cancellation);
        Assert.Equal(MaintenanceKind.Immediate, Assert.Single(details.OpenMaintenances).Kind);
    }

    [Fact]
    public async Task Complete_Twice_IsConflictWithTheReason_RN08()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var created = await client.CreateEquipmentAsync(scenario.Camera("CAM-001", EquipmentStatus.Malfunction), Cancellation);
        var immediate = created.OpenMaintenances.Single();
        await CompleteAsync(client, created.Id, immediate.Id, MaintenanceResult.Success, null);

        var response = await client.PostJsonAsync(
            $"{Equipment}/{created.Id}/maintenances/{immediate.Id}/completion",
            new CompleteMaintenanceRequest { Result = MaintenanceResult.Success, CompletedAt = DateTimeOffset.UtcNow },
            Cancellation);

        await ProblemAssert.IsBusinessRuleAsync(response, "A manutenção não está aberta.", Cancellation);
    }

    [Fact]
    public async Task Complete_InTheFuture_IsBadRequestOnCompletedAt_Plan15_4()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var created = await client.CreateEquipmentAsync(scenario.Camera("CAM-001", EquipmentStatus.Malfunction), Cancellation);

        var response = await client.PostJsonAsync(
            $"{Equipment}/{created.Id}/maintenances/{created.OpenMaintenances.Single().Id}/completion",
            new CompleteMaintenanceRequest { Result = MaintenanceResult.Success, CompletedAt = DateTimeOffset.UtcNow.AddHours(1) },
            Cancellation);

        var errors = await ProblemAssert.IsValidationAsync(response, Cancellation);
        Assert.Equal(["completedAt"], errors.Keys);
        Assert.Equal(["A conclusão não pode estar no futuro."], errors["completedAt"]);
    }

    [Fact]
    public async Task Complete_WithoutResult_IsBadRequestOnResult()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var created = await client.CreateEquipmentAsync(scenario.Camera("CAM-001", EquipmentStatus.Malfunction), Cancellation);

        var response = await client.PostJsonAsync(
            $"{Equipment}/{created.Id}/maintenances/{created.OpenMaintenances.Single().Id}/completion",
            new CompleteMaintenanceRequest { CompletedAt = DateTimeOffset.UtcNow },
            Cancellation);

        var errors = await ProblemAssert.IsValidationAsync(response, Cancellation);
        Assert.Equal(["result"], errors.Keys);
        Assert.Equal(["Informe se a manutenção teve sucesso."], errors["result"]);
    }

    [Fact]
    public async Task Complete_UnknownMaintenanceOrEquipment_IsNotFound_ByManager_IsForbidden()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var admin = Client(UserRoles.Administrator);
        var created = await admin.CreateEquipmentAsync(scenario.Camera("CAM-001", EquipmentStatus.Malfunction), Cancellation);
        var other = await admin.CreateEquipmentAsync(scenario.Camera("CAM-002", EquipmentStatus.Malfunction), Cancellation);
        using var manager = Client(UserRoles.Manager);
        var request = new CompleteMaintenanceRequest { Result = MaintenanceResult.Success, CompletedAt = DateTimeOffset.UtcNow };

        await ProblemAssert.IsNotFoundAsync(
            await admin.PostJsonAsync($"{Equipment}/{created.Id}/maintenances/{Guid.NewGuid()}/completion", request, Cancellation),
            "Manutenção não encontrada.",
            Cancellation);
        await ProblemAssert.IsNotFoundAsync(
            await admin.PostJsonAsync(
                $"{Equipment}/{created.Id}/maintenances/{other.OpenMaintenances.Single().Id}/completion", request, Cancellation),
            "Manutenção não encontrada.",
            Cancellation);
        await ProblemAssert.IsNotFoundAsync(
            await admin.PostJsonAsync(
                $"{Equipment}/{Guid.NewGuid()}/maintenances/{created.OpenMaintenances.Single().Id}/completion", request, Cancellation),
            "Equipamento não encontrado.",
            Cancellation);
        await ProblemAssert.IsForbiddenAsync(
            await manager.PostJsonAsync(
                $"{Equipment}/{created.Id}/maintenances/{created.OpenMaintenances.Single().Id}/completion", request, Cancellation),
            Cancellation);
    }

    private HttpClient Client(string role) => Fixture.CreateUserClient(Fixture.Tokens.For(role));

    private async Task<EquipmentDetails> CreateCameraAsync()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        return await client.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);
    }

    private static async Task<MaintenanceData[]> ListAsync(HttpClient client, Guid id, string query = "")
    {
        var response = await client.GetAsync($"{Equipment}/{id}/maintenances{query}", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<MaintenanceData[]>(Cancellation);
    }

    private static async Task<MaintenanceCompletionData> CompleteAsync(
        HttpClient client, Guid id, Guid maintenanceId, MaintenanceResult result, string? notes)
    {
        var response = await client.PostJsonAsync(
            $"{Equipment}/{id}/maintenances/{maintenanceId}/completion",
            new CompleteMaintenanceRequest { Result = result, CompletedAt = DateTimeOffset.UtcNow, Notes = notes },
            Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<MaintenanceCompletionData>(Cancellation);
    }
}
