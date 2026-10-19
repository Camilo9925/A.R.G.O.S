using System.Net;
using Argos.Contracts.Access;
using Argos.Contracts.Common;
using Argos.Contracts.Inventory;
using Argos.IntegrationTests.Infrastructure;
using static Argos.IntegrationTests.Infrastructure.InventoryHttp;

namespace Argos.IntegrationTests.Inventory;

/// <summary>
/// Endpoints de equipamentos (T067; US-01, US-02, US-04; plan 15.14, 15.16): cadastro, detalhes, edição, situações
/// iniciais, mudança de situação e histórico de situação. Caminho feliz de cada rota (Art. XII.3) e os erros com o campo
/// exato, o <c>existingEquipmentId</c> ou o motivo no <c>detail</c> (Art. XII.4). Pesquisa e necessidade de manutenção
/// ficam em <see cref="EquipmentSearchEndpointsTests"/>; manutenções, em <see cref="MaintenanceEndpointsTests"/>.
/// </summary>
public sealed class EquipmentEndpointsTests(ApiFixture fixture) : IntegrationTestBase(fixture)
{
    private const string InitialStatusesRoute = "api/inventory/equipment/initial-statuses";
    private const string NotFound = "Equipamento não encontrado.";
    private const string Justification = "Ocorrência registrada pela guarda.";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // Situações iniciais (D-66)

    [Fact]
    public async Task InitialStatuses_ListsTheDomainInitialStatusesInOrder_D66()
    {
        using var client = Client(UserRoles.Administrator);

        var response = await client.GetAsync(InitialStatusesRoute, Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var statuses = await response.ReadAsync<string[]>(Cancellation);
        Assert.Equal(["NotInstalled", "Operational", "Malfunction", "UnderMaintenance"], statuses);
    }

    [Theory]
    [InlineData(UserRoles.Operator)]
    [InlineData(UserRoles.Manager)]
    public async Task InitialStatuses_WithoutInventoryManage_IsForbidden_D58(string role)
    {
        using var client = Client(role);

        var response = await client.GetAsync(InitialStatusesRoute, Cancellation);

        await ProblemAssert.IsForbiddenAsync(response, Cancellation);
    }

    // Cadastro (US-01, US-02)

    [Fact]
    public async Task Create_FixedCamera_Returns201WithLocation_AndTheInitialHistoryRecord_US01()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);

        var response = await client.PostJsonAsync(Equipment, scenario.Camera("CAM-001"), Cancellation);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<EquipmentDetails>(Cancellation);
        var details = await (await client.GetAsync(response.Headers.Location, Cancellation)).ReadAsync<EquipmentDetails>(Cancellation);
        Assert.Equal(created.Id, details.Id);
        Assert.Equal(("surveillance-camera", EquipmentCategory.Fixed, "CAM-001", "Ômega Vision", "OV-200"),
            (details.TypeKey, details.Category, details.SerialNumber, details.Brand, details.Model));
        Assert.Equal((scenario.OrganizationId, InventorySeedData.Organization), (details.OrganizationId, details.OrganizationAcronym));
        Assert.Equal(("Rua das Flores", "número 181", "16985-001", "Prometeia", "SP"),
            (details.Location!.Street, details.Location.Reference, details.Location.ZipCode, details.Location.City,
                details.Location.State));
        Assert.Equal((scenario.HesperiaId, InventorySeedData.Hesperia, scenario.RegionId),
            (details.NeighborhoodId, details.NeighborhoodName, details.RegionId));
        Assert.Null(details.BaseId);
        Assert.Equal(("full-hd-1080p", "Full HD 1080p"), Attribute(details, "resolution"));
        Assert.Equal(("true", "Sim"), Attribute(details, "nightVision"));
        Assert.Equal((EquipmentStatus.Operational, MaintenanceNeed.None, true), (details.Status, details.MaintenanceNeed, details.IsActive));
        Assert.Empty(details.OpenMaintenances);
        Assert.Equal(
            [(EquipmentStatus.Malfunction, false), (EquipmentStatus.UnderMaintenance, false),
                (EquipmentStatus.Deactivated, true), (EquipmentStatus.Stolen, true)],
            details.AllowedStatusChanges.Select(option => (option.Status, option.RequiresJustification)));
        Assert.Equal("admin", details.CreatedBy);

        var history = await HistoryAsync(client, created.Id);
        var initial = Assert.Single(history.Items);
        Assert.Equal((null, EquipmentStatus.Operational, StatusChangeOrigin.Registration, "admin"),
            (initial.FromStatus, initial.ToStatus, initial.Origin, initial.Author));
    }

    [Fact]
    public async Task Create_MobileMotorcycle_UsesTheBaseAndItsNeighborhood_RF007()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);

        var details = await client.CreateEquipmentAsync(scenario.Motorcycle("MOTO-001"), Cancellation);

        Assert.Equal((EquipmentCategory.Mobile, scenario.BaseId, InventorySeedData.Base),
            (details.Category, details.BaseId, details.BaseName));
        Assert.Null(details.Location);
        Assert.Equal((scenario.DowntownId, InventorySeedData.Downtown), (details.NeighborhoodId, details.NeighborhoodName));
    }

    [Fact]
    public async Task Create_InMalfunction_OpensTheImmediateMaintenance_RN07()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);

        var details = await client.CreateEquipmentAsync(scenario.Camera("CAM-001", EquipmentStatus.Malfunction), Cancellation);

        var immediate = Assert.Single(details.OpenMaintenances);
        Assert.Equal((MaintenanceKind.Immediate, MaintenanceOrigin.AutomaticOnMalfunction), (immediate.Kind, immediate.Origin));
        Assert.Equal(MaintenanceNeed.Immediate, details.MaintenanceNeed);
    }

    [Fact]
    public async Task Create_WithInitialPeriodicMaintenance_OpensIt_RF002()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        var today = await Fixture.TodayAsync(Cancellation);
        using var client = Client(UserRoles.Administrator);

        var details = await client.CreateEquipmentAsync(
            scenario.Camera("CAM-001", initialMaintenance: InventoryScenario.Periodic(today.AddDays(20), 180)), Cancellation);

        var periodic = Assert.Single(details.OpenMaintenances);
        Assert.Equal((MaintenanceKind.Periodic, today.AddDays(20), 180, MaintenanceNeed.Scheduled),
            (periodic.Kind, periodic.DueDate, periodic.PeriodicityDays, periodic.Need));
    }

    [Fact]
    public async Task Create_RadarWithSpeedModalityWithoutMaxSpeed_IsBadRequestOnlyOnThatAttribute_US02()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);

        var response = await client.PostJsonAsync(Equipment, scenario.Radar("RAD-001", "speed", null), Cancellation);

        var errors = await ProblemAssert.IsValidationAsync(response, Cancellation);
        Assert.Equal(["attributes.maxSpeedKmh"], errors.Keys);
        Assert.Equal(["Obrigatório quando modalidade é Velocidade."], errors["attributes.maxSpeedKmh"]);
    }

    [Fact]
    public async Task Create_RadarWithOtherModalityWithoutMaxSpeed_IsAccepted_US02()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);

        var radar = await client.CreateEquipmentAsync(scenario.Radar("RAD-001", "red-light", null), Cancellation);
        var speed = await client.CreateEquipmentAsync(scenario.Radar("RAD-002", "speed", "60"), Cancellation);

        Assert.DoesNotContain(radar.Attributes, attribute => attribute.Key == "maxSpeedKmh");
        Assert.Equal(("60", "60 km/h"), Attribute(speed, "maxSpeedKmh"));
    }

    [Fact]
    public async Task Create_SeveralInvalidAttributes_ReturnsEveryFieldAtOnce_D46()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var request = scenario.Camera("CAM-001");
        request.Attributes = new() { ["resolution"] = "8k", ["colorMode"] = "color", ["nightVision"] = "sim", ["unknown"] = "x" };

        var response = await client.PostJsonAsync(Equipment, request, Cancellation);

        var errors = await ProblemAssert.IsValidationAsync(response, Cancellation);
        Assert.Equal(
            ["attributes.nightVision", "attributes.panTiltZoom", "attributes.resolution", "attributes.unknown"],
            errors.Keys.Order());
        Assert.Equal(["Campo obrigatório."], errors["attributes.panTiltZoom"]);
        Assert.Equal(["Característica desconhecida para este tipo de equipamento."], errors["attributes.unknown"]);
    }

    [Fact]
    public async Task Create_WithRequiredFieldsEmpty_IsBadRequestOnEachField_US01()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var request = scenario.Camera(string.Empty);
        request.Brand = string.Empty;
        request.Model = "  ";
        request.OrganizationId = null;

        var response = await client.PostJsonAsync(Equipment, request, Cancellation);

        var errors = await ProblemAssert.IsValidationAsync(response, Cancellation);
        Assert.Equal(["brand", "model", "organizationId", "serialNumber"], errors.Keys.Order());
        Assert.Equal(["Informe o número de série."], errors["serialNumber"]);
        Assert.Equal(["Escolha o órgão responsável."], errors["organizationId"]);
    }

    [Fact]
    public async Task Create_FixedWithoutLocation_IsBadRequestOnLocation_RF003()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var request = scenario.Camera("CAM-001");
        request.Location = null;

        var response = await client.PostJsonAsync(Equipment, request, Cancellation);

        var errors = await ProblemAssert.IsValidationAsync(response, Cancellation);
        Assert.Equal(["location"], errors.Keys);
        Assert.Equal(["Informe a localização do equipamento fixo."], errors["location"]);
    }

    [Fact]
    public async Task Create_PointOutsideTheMunicipality_IsBadRequestOnLocationCoordinates_RN02()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var request = scenario.Camera("CAM-001");
        request.Location = InventoryScenario.Location(scenario.HesperiaId, "Rua das Flores", "S/N", -22.241, -49.75);

        var response = await client.PostJsonAsync(Equipment, request, Cancellation);

        var errors = await ProblemAssert.IsValidationAsync(response, Cancellation);
        Assert.Equal(["location.latitude", "location.longitude"], errors.Keys.Order());
        Assert.Equal(["O ponto está fora do município."], errors["location.latitude"]);
        Assert.Equal(["O ponto está fora do município."], errors["location.longitude"]);
    }

    [Fact]
    public async Task Create_WithStatusNotAllowedAtRegistration_IsBadRequestOnStatus_D66()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);

        var response = await client.PostJsonAsync(Equipment, scenario.Camera("CAM-001", EquipmentStatus.Stolen), Cancellation);

        var errors = await ProblemAssert.IsValidationAsync(response, Cancellation);
        Assert.Equal(["status"], errors.Keys);
        Assert.Equal(
            ["Situação inicial permitida: Ainda não instalado, Operacional, Mau funcionamento ou Em manutenção."],
            errors["status"]);
    }

    [Fact]
    public async Task Create_SameBrandAndSerialIgnoringCaseAccentsAndSpaces_IsConflictPointingToTheExisting_RN01()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var existing = await client.CreateEquipmentAsync(scenario.Camera("ABC-123"), Cancellation);
        var duplicate = scenario.Camera(" abc-123 ", downtown: true);
        duplicate.Brand = "OMEGA  VISION";

        var response = await client.PostJsonAsync(Equipment, duplicate, Cancellation);

        var problem = await ProblemAssert.IsConflictAsync(
            response, "Já existe um equipamento com essa marca e esse número de série.", Cancellation);
        Assert.Equal(existing.Id, problem.GetProperty("existingEquipmentId").GetGuid());
    }

    [Fact]
    public async Task Create_SameSerialWithOtherBrand_OrSameBrandWithOtherSerial_IsAccepted_RN01()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        await client.CreateEquipmentAsync(scenario.Camera("ABC-123"), Cancellation);
        var otherBrand = scenario.Camera("ABC-123");
        otherBrand.Brand = "Vigilis";

        await client.CreateEquipmentAsync(otherBrand, Cancellation);
        await client.CreateEquipmentAsync(scenario.Camera("ABC-124"), Cancellation);
    }

    [Fact]
    public async Task Create_UnknownReferencesInTheBody_AreNotFound_Plan15_12()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);

        var unknownType = scenario.Camera("CAM-001");
        unknownType.TypeId = Guid.NewGuid();
        await ProblemAssert.IsNotFoundAsync(
            await client.PostJsonAsync(Equipment, unknownType, Cancellation), "Tipo de equipamento não encontrado.", Cancellation);

        var unknownOrganization = scenario.Camera("CAM-001");
        unknownOrganization.OrganizationId = Guid.NewGuid();
        await ProblemAssert.IsNotFoundAsync(
            await client.PostJsonAsync(Equipment, unknownOrganization, Cancellation), "Órgão não encontrado.", Cancellation);

        var unknownBase = scenario.Motorcycle("MOTO-001");
        unknownBase.BaseId = Guid.NewGuid();
        await ProblemAssert.IsNotFoundAsync(
            await client.PostJsonAsync(Equipment, unknownBase, Cancellation), "Base operacional não encontrada.", Cancellation);
    }

    [Fact]
    public async Task Create_ByManager_IsForbidden_D58()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Manager);

        var response = await client.PostJsonAsync(Equipment, scenario.Camera("CAM-001"), Cancellation);

        await ProblemAssert.IsForbiddenAsync(response, Cancellation);
    }

    // Detalhes

    [Fact]
    public async Task Details_ForManager_HasNoAllowedStatusChanges_D59()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var admin = Client(UserRoles.Administrator);
        var created = await admin.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);
        using var manager = Client(UserRoles.Manager);

        var details = await manager.GetEquipmentAsync(created.Id, Cancellation);

        Assert.Equal(created.Id, details.Id);
        Assert.Empty(details.AllowedStatusChanges);
    }

    [Fact]
    public async Task Details_ByOperator_IsForbidden_D58()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var admin = Client(UserRoles.Administrator);
        var created = await admin.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);
        using var operatorClient = Client(UserRoles.Operator);

        await ProblemAssert.IsForbiddenAsync(await operatorClient.GetAsync($"{Equipment}/{created.Id}", Cancellation), Cancellation);
    }

    [Fact]
    public async Task Details_Unknown_IsNotFound()
    {
        using var client = Client(UserRoles.Administrator);

        var response = await client.GetAsync($"{Equipment}/{Guid.NewGuid()}", Cancellation);

        await ProblemAssert.IsNotFoundAsync(response, NotFound, Cancellation);
    }

    // Edição (D-60)

    [Fact]
    public async Task Update_ChangesTheRegistration_AndTheVersion_US01()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var created = await client.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);

        var response = await client.PutJsonAsync($"{Equipment}/{created.Id}", Update(scenario, created, "OV-300"), Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadAsync<EquipmentDetails>(Cancellation);
        Assert.Equal(("OV-300", scenario.DowntownId, "Avenida Brasil"),
            (updated.Model, updated.NeighborhoodId, updated.Location!.Street));
        Assert.Equal(("qhd-2k", "2K"), Attribute(updated, "resolution"));
        Assert.NotEqual(created.Version, updated.Version);
        Assert.Equal("admin", updated.UpdatedBy);
        var reloaded = await client.GetEquipmentAsync(created.Id, Cancellation);
        Assert.Equal((updated.Model, updated.Version, updated.NeighborhoodId), (reloaded.Model, reloaded.Version, reloaded.NeighborhoodId));
    }

    [Fact]
    public async Task Update_WithStaleVersion_IsConflict_D60()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var created = await client.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);
        var first = await client.PutJsonAsync($"{Equipment}/{created.Id}", Update(scenario, created, "OV-300"), Cancellation);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var stale = await client.PutJsonAsync($"{Equipment}/{created.Id}", Update(scenario, created, "OV-400"), Cancellation);

        await ProblemAssert.IsConflictAsync(
            stale,
            "O equipamento foi alterado por outra pessoa depois que você o abriu. Recarregue os dados e tente de novo.",
            Cancellation);
        Assert.Equal("OV-300", (await client.GetEquipmentAsync(created.Id, Cancellation)).Model);
    }

    [Fact]
    public async Task Update_WithoutVersion_IsBadRequestOnVersion_D60()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var created = await client.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);
        var request = Update(scenario, created, "OV-300");
        request.Version = null;

        var response = await client.PutJsonAsync($"{Equipment}/{created.Id}", request, Cancellation);

        var errors = await ProblemAssert.IsValidationAsync(response, Cancellation);
        Assert.Equal(["version"], errors.Keys);
        Assert.Equal(["Informe a versão do cadastro lida."], errors["version"]);
    }

    [Fact]
    public async Task Update_ToTheBrandAndSerialOfAnother_IsConflictPointingToIt_RN01()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var other = await client.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);
        var created = await client.CreateEquipmentAsync(scenario.Camera("CAM-002"), Cancellation);
        var request = Update(scenario, created, "OV-200");
        request.SerialNumber = "cam-001";

        var response = await client.PutJsonAsync($"{Equipment}/{created.Id}", request, Cancellation);

        var problem = await ProblemAssert.IsConflictAsync(
            response, "Já existe um equipamento com essa marca e esse número de série.", Cancellation);
        Assert.Equal(other.Id, problem.GetProperty("existingEquipmentId").GetGuid());
    }

    [Fact]
    public async Task Update_UnknownOrByManager_IsNotFoundOrForbidden()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var admin = Client(UserRoles.Administrator);
        var created = await admin.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);
        using var manager = Client(UserRoles.Manager);

        await ProblemAssert.IsNotFoundAsync(
            await admin.PutJsonAsync($"{Equipment}/{Guid.NewGuid()}", Update(scenario, created, "OV-300"), Cancellation),
            NotFound,
            Cancellation);
        await ProblemAssert.IsForbiddenAsync(
            await manager.PutJsonAsync($"{Equipment}/{created.Id}", Update(scenario, created, "OV-300"), Cancellation),
            Cancellation);
    }

    // Situação (US-04)

    [Fact]
    public async Task ChangeStatus_OperationalToMalfunction_RecordsHistoryAndOpensTheImmediate_RN07()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var created = await client.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);
        var before = DateTimeOffset.UtcNow;

        var details = await client.ChangeStatusAsync(created.Id, EquipmentStatus.Malfunction, null, Cancellation);

        Assert.Equal((EquipmentStatus.Malfunction, MaintenanceNeed.Immediate), (details.Status, details.MaintenanceNeed));
        var immediate = Assert.Single(details.OpenMaintenances);
        Assert.Equal(
            (MaintenanceKind.Immediate, MaintenanceOrigin.AutomaticOnMalfunction,
                "Aberta automaticamente: equipamento em mau funcionamento."),
            (immediate.Kind, immediate.Origin, immediate.Description));

        var history = await HistoryAsync(Client(UserRoles.Manager), created.Id);
        Assert.Equal(2, history.TotalCount);
        var change = history.Items[0];
        Assert.Equal((EquipmentStatus.Operational, EquipmentStatus.Malfunction, StatusChangeOrigin.Manual, "admin"),
            (change.FromStatus, change.ToStatus, change.Origin, change.Author));
        Assert.InRange(change.OccurredAt, before.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.Equal(StatusChangeOrigin.Registration, history.Items[1].Origin);
    }

    [Theory]
    [InlineData(EquipmentStatus.Stolen)]
    [InlineData(EquipmentStatus.Deactivated)]
    public async Task ChangeStatus_ToStolenOrDeactivatedWithoutJustification_IsBadRequestOnJustification_RN06(
        EquipmentStatus target)
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var created = await client.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);

        var response = await client.PostJsonAsync(
            $"{Equipment}/{created.Id}/status-changes", new ChangeEquipmentStatusRequest { Status = target }, Cancellation);

        var errors = await ProblemAssert.IsValidationAsync(response, Cancellation);
        Assert.Equal(["justification"], errors.Keys);
        Assert.Equal(["Informe a justificativa, com 3 a 500 caracteres."], errors["justification"]);
        Assert.Equal(EquipmentStatus.Operational, (await client.GetEquipmentAsync(created.Id, Cancellation)).Status);
    }

    [Fact]
    public async Task ChangeStatus_ToStolenWithJustification_CancelsTheOpenMaintenances_RN11()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var created = await client.CreateEquipmentAsync(scenario.Camera("CAM-001", EquipmentStatus.Malfunction), Cancellation);

        var details = await client.ChangeStatusAsync(created.Id, EquipmentStatus.Stolen, Justification, Cancellation);

        Assert.Equal((EquipmentStatus.Stolen, MaintenanceNeed.None), (details.Status, details.MaintenanceNeed));
        Assert.Empty(details.OpenMaintenances);
        var history = await HistoryAsync(client, created.Id);
        Assert.Equal(Justification, history.Items[0].Justification);
        var maintenances = await (await client.GetAsync($"{Equipment}/{created.Id}/maintenances", Cancellation))
            .ReadAsync<MaintenanceData[]>(Cancellation);
        var cancelled = Assert.Single(maintenances);
        Assert.Equal((MaintenanceState.Cancelled, "Cancelada automaticamente: equipamento furtado.", "admin"),
            (cancelled.State, cancelled.CancellationReason, cancelled.CancelledBy));
    }

    [Fact]
    public async Task ChangeStatus_TransitionNotAllowed_IsConflictWithTheReason_RN05()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var created = await client.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);
        await client.ChangeStatusAsync(created.Id, EquipmentStatus.Stolen, Justification, Cancellation);

        var response = await client.PostJsonAsync(
            $"{Equipment}/{created.Id}/status-changes",
            new ChangeEquipmentStatusRequest { Status = EquipmentStatus.Operational },
            Cancellation);

        await ProblemAssert.IsBusinessRuleAsync(response, "Não é permitido passar de Furtado para Operacional.", Cancellation);
        Assert.Equal(EquipmentStatus.Stolen, (await client.GetEquipmentAsync(created.Id, Cancellation)).Status);
    }

    [Fact]
    public async Task ChangeStatus_ToTheCurrentStatus_IsConflictWithTheReason_Plan15_3()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var created = await client.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);

        var response = await client.PostJsonAsync(
            $"{Equipment}/{created.Id}/status-changes",
            new ChangeEquipmentStatusRequest { Status = EquipmentStatus.Operational },
            Cancellation);

        await ProblemAssert.IsBusinessRuleAsync(response, "O equipamento já está nessa situação.", Cancellation);
    }

    [Fact]
    public async Task Reactivation_ByOperatorIsForbiddenWithTheReason_ByAdministratorNeedsJustification_RN05_D47()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var admin = Client(UserRoles.Administrator);
        var created = await admin.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);
        var deactivated = await admin.ChangeStatusAsync(created.Id, EquipmentStatus.Deactivated, Justification, Cancellation);
        Assert.Equal([(EquipmentStatus.Operational, true)],
            deactivated.AllowedStatusChanges.Select(option => (option.Status, option.RequiresJustification)));
        using var operatorClient = Client(UserRoles.Operator);
        var reactivation = new ChangeEquipmentStatusRequest { Status = EquipmentStatus.Operational, Justification = Justification };

        var byOperator = await operatorClient.PostJsonAsync($"{Equipment}/{created.Id}/status-changes", reactivation, Cancellation);
        await ProblemAssert.IsForbiddenAsync(
            byOperator, "Somente o Administrador pode reativar um equipamento desativado.", Cancellation);

        var withoutJustification = await admin.PostJsonAsync(
            $"{Equipment}/{created.Id}/status-changes",
            new ChangeEquipmentStatusRequest { Status = EquipmentStatus.Operational },
            Cancellation);
        Assert.Equal(["justification"], (await ProblemAssert.IsValidationAsync(withoutJustification, Cancellation)).Keys);

        var reactivated = await admin.ChangeStatusAsync(created.Id, EquipmentStatus.Operational, Justification, Cancellation);
        Assert.Equal(EquipmentStatus.Operational, reactivated.Status);
    }

    [Fact]
    public async Task ChangeStatus_ByOperator_IsAllowed_D58()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var admin = Client(UserRoles.Administrator);
        var created = await admin.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);
        using var operatorClient = Client(UserRoles.Operator);

        var details = await operatorClient.ChangeStatusAsync(created.Id, EquipmentStatus.UnderMaintenance, null, Cancellation);

        Assert.Equal(EquipmentStatus.UnderMaintenance, details.Status);
        Assert.Equal("operador", (await HistoryAsync(admin, created.Id)).Items[0].Author);
    }

    [Fact]
    public async Task ChangeStatus_WithoutStatus_IsBadRequestOnStatus()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var created = await client.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);

        var response = await client.PostJsonAsync(
            $"{Equipment}/{created.Id}/status-changes", new ChangeEquipmentStatusRequest(), Cancellation);

        var errors = await ProblemAssert.IsValidationAsync(response, Cancellation);
        Assert.Equal(["status"], errors.Keys);
        Assert.Equal(["Escolha a nova situação."], errors["status"]);
    }

    [Fact]
    public async Task ChangeStatus_UnknownOrByManager_IsNotFoundOrForbidden()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var admin = Client(UserRoles.Administrator);
        var created = await admin.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);
        using var manager = Client(UserRoles.Manager);
        var request = new ChangeEquipmentStatusRequest { Status = EquipmentStatus.Malfunction };

        await ProblemAssert.IsNotFoundAsync(
            await admin.PostJsonAsync($"{Equipment}/{Guid.NewGuid()}/status-changes", request, Cancellation), NotFound, Cancellation);
        await ProblemAssert.IsForbiddenAsync(
            await manager.PostJsonAsync($"{Equipment}/{created.Id}/status-changes", request, Cancellation), Cancellation);
    }

    // Histórico de situação

    [Fact]
    public async Task StatusHistory_IsPagedFromNewestToOldest_US04()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var client = Client(UserRoles.Administrator);
        var created = await client.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);
        await client.ChangeStatusAsync(created.Id, EquipmentStatus.Malfunction, null, Cancellation);
        await client.ChangeStatusAsync(created.Id, EquipmentStatus.UnderMaintenance, null, Cancellation);

        var firstPage = await HistoryAsync(client, created.Id, "?page=1&pageSize=2");
        var secondPage = await HistoryAsync(client, created.Id, "?page=2&pageSize=2");

        Assert.Equal((3, 1, 2), (firstPage.TotalCount, firstPage.Page, firstPage.PageSize));
        Assert.Equal([EquipmentStatus.UnderMaintenance, EquipmentStatus.Malfunction], firstPage.Items.Select(item => item.ToStatus));
        Assert.Equal([EquipmentStatus.Operational], secondPage.Items.Select(item => item.ToStatus));
    }

    [Fact]
    public async Task StatusHistory_InvalidPageUnknownOrWithoutHistoryView_AreRefused()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var admin = Client(UserRoles.Administrator);
        var created = await admin.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);

        var invalid = await admin.GetAsync($"{Equipment}/{created.Id}/status-changes?pageSize=101", Cancellation);
        var errors = await ProblemAssert.IsValidationAsync(invalid, Cancellation);
        Assert.Equal(["pageSize"], errors.Keys);

        await ProblemAssert.IsNotFoundAsync(
            await admin.GetAsync($"{Equipment}/{Guid.NewGuid()}/status-changes", Cancellation), NotFound, Cancellation);

        using var anonymous = Fixture.Factory.CreateClient();
        await ProblemAssert.IsUnauthenticatedAsync(
            await anonymous.GetAsync($"{Equipment}/{created.Id}/status-changes", Cancellation), Cancellation);
    }

    [Fact]
    public async Task StatusHistory_ByOperator_IsAllowed_RN26()
    {
        var scenario = await InventoryScenario.CreateAsync(Fixture, Cancellation);
        using var admin = Client(UserRoles.Administrator);
        var created = await admin.CreateEquipmentAsync(scenario.Camera("CAM-001"), Cancellation);

        var history = await HistoryAsync(Client(UserRoles.Operator), created.Id);

        Assert.Equal(1, history.TotalCount);
    }

    private HttpClient Client(string role) => Fixture.CreateUserClient(Fixture.Tokens.For(role));

    private static async Task<PagedResult<StatusChangeData>> HistoryAsync(HttpClient client, Guid id, string query = "")
    {
        var response = await client.GetAsync($"{Equipment}/{id}/status-changes{query}", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<PagedResult<StatusChangeData>>(Cancellation);
    }

    private static (string Value, string DisplayValue) Attribute(EquipmentDetails details, string key)
    {
        var attribute = details.Attributes.Single(item => item.Key == key);
        return (attribute.Value, attribute.DisplayValue);
    }

    private static UpdateEquipmentRequest Update(InventoryScenario scenario, EquipmentDetails current, string model) =>
        new()
        {
            SerialNumber = current.SerialNumber,
            Brand = current.Brand,
            Model = model,
            OrganizationId = scenario.OrganizationId,
            Location = scenario.DowntownLocation(),
            Attributes = InventoryScenario.CameraAttributes("qhd-2k"),
            Version = current.Version,
        };
}
