using Argos.Application.Common.Exceptions;
using Argos.Application.Inventory.Timeline;
using Argos.Contracts.Common;
using Argos.Contracts.Inventory;
using Argos.Domain.Inventory;
using Argos.UnitTests.Domain.Inventory;
using DomainAgentStatus = Argos.Domain.Inventory.Agents.AgentStatus;
using DomainEndReason = Argos.Domain.Inventory.Agents.AssignmentEndReason;
using DomainEquipmentStatus = Argos.Domain.Inventory.Equipments.EquipmentStatus;
using DomainMaintenanceKind = Argos.Domain.Inventory.Equipments.MaintenanceKind;
using DomainMaintenanceOrigin = Argos.Domain.Inventory.Equipments.MaintenanceOrigin;
using DomainMaintenanceResult = Argos.Domain.Inventory.Equipments.MaintenanceResult;
using DomainStatusChangeOrigin = Argos.Domain.Inventory.Equipments.StatusChangeOrigin;

namespace Argos.UnitTests.Application.Inventory;

/// <summary>
/// Textos da linha do tempo de equipamento e de agente (US-08, RF-011, RF-018; Art. V.1, XIII.2; D-49; plan 15.6).
/// </summary>
public sealed class TimelineServiceTests
{
    private static readonly DateTimeOffset At = InventoryTestData.Now;

    private readonly InventoryServices _services = new();
    private readonly Guid _equipmentId;
    private readonly Guid _agentId;

    public TimelineServiceTests()
    {
        var radar = EquipmentTestData.NewRadar();
        _services.Inventory.Equipment.Add(radar);
        _equipmentId = radar.Id;

        var agent = AgentTestData.NewAgent(organization: _services.Organization, operationalBase: _services.Base);
        _services.Inventory.Agents.Add(agent);
        _agentId = agent.Id;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Situação (US-04, US-08, RN-12)

    [Fact]
    public async Task EquipmentStatusChange_TitleIsFromArrowToWithJustification_US08()
    {
        var entry = await EquipmentEntryAsync(Fact(TimelineEntryKind.StatusChanged) with
        {
            EquipmentStatus = new(DomainEquipmentStatus.Operational, DomainEquipmentStatus.Malfunction, DomainStatusChangeOrigin.Manual, null),
        });

        Assert.Equal("Operacional → Mau funcionamento", entry.Title);
        Assert.Null(entry.Description);
        Assert.Empty(entry.Details);

        var justified = await EquipmentEntryAsync(Fact(TimelineEntryKind.StatusChanged) with
        {
            EquipmentStatus = new(DomainEquipmentStatus.Operational, DomainEquipmentStatus.Stolen, DomainStatusChangeOrigin.Manual, "Furtado na madrugada."),
        });

        Assert.Equal("Operacional → Furtado", justified.Title);
        Assert.Equal([new TimelineDetailData("Justificativa", "Furtado na madrugada.")], justified.Details);
    }

    [Fact]
    public async Task EquipmentStatusChangeFromMaintenance_SaysItCameFromTheCompletion_RN11()
    {
        var entry = await EquipmentEntryAsync(Fact(TimelineEntryKind.StatusChanged) with
        {
            EquipmentStatus = new(
                DomainEquipmentStatus.Malfunction, DomainEquipmentStatus.Operational, DomainStatusChangeOrigin.MaintenanceCompletion, null),
        });

        Assert.Equal(("Mau funcionamento → Operacional", "Resultado da conclusão de uma manutenção."), (entry.Title, entry.Description));
    }

    [Fact]
    public async Task EquipmentRegistered_ShowsTheInitialStatus_US01()
    {
        var entry = await EquipmentEntryAsync(Fact(TimelineEntryKind.Registered) with
        {
            EquipmentStatus = new(null, DomainEquipmentStatus.NotInstalled, DomainStatusChangeOrigin.Registration, null),
        });

        Assert.Equal(("Equipamento cadastrado", "Situação inicial: Ainda não instalado."), (entry.Title, entry.Description));
    }

    [Fact]
    public async Task AgentRegisteredAndStatusChange_UseTheAgentStatusLabels_RN12()
    {
        _services.Timeline.Facts.AddRange(
        [
            Fact(TimelineEntryKind.StatusChanged) with { AgentStatus = new(DomainAgentStatus.OffDuty, DomainAgentStatus.OnDuty) },
            Fact(TimelineEntryKind.Registered) with { AgentStatus = new(null, DomainAgentStatus.OffDuty) },
        ]);

        var page = await _services.TimelineService().ForAgentAsync(_agentId, new PageQuery(), Ct);

        Assert.Equal(
            [("Fora de serviço → Em serviço", null), ("Agente cadastrado", "Situação inicial: Fora de serviço.")],
            page.Items.Select(entry => (entry.Title, entry.Description)));
    }

    // Manutenções (US-05, RN-07 a RN-11, RN-35)

    [Theory]
    [InlineData(DomainMaintenanceKind.Periodic, DomainMaintenanceResult.Success, "Manutenção periódica concluída com sucesso")]
    [InlineData(DomainMaintenanceKind.Immediate, DomainMaintenanceResult.Failure, "Manutenção imediata concluída sem sucesso")]
    [InlineData(DomainMaintenanceKind.Scheduled, DomainMaintenanceResult.Success, "Manutenção agendada concluída com sucesso")]
    public async Task MaintenanceCompleted_TitleHasKindAndResult_US05(
        DomainMaintenanceKind kind, DomainMaintenanceResult result, string title)
    {
        var entry = await EquipmentEntryAsync(Fact(TimelineEntryKind.MaintenanceCompleted) with
        {
            Maintenance = Maintenance(kind) with { Result = result, CompletionNotes = "Sensor substituído." },
        });

        Assert.Equal(title, entry.Title);
        Assert.Equal([new TimelineDetailData("Observações", "Sensor substituído.")], entry.Details);
    }

    [Fact]
    public async Task MaintenanceOpened_PeriodicRescheduling_ShowsDueDateAndPeriodicity_RN09()
    {
        var entry = await EquipmentEntryAsync(Fact(TimelineEntryKind.MaintenanceOpened) with
        {
            Maintenance = Maintenance(DomainMaintenanceKind.Periodic) with
            {
                Origin = DomainMaintenanceOrigin.PeriodicRescheduling,
                DueDate = new DateOnly(2027, 4, 16),
                PeriodicityDays = 180,
                Description = "Aferição semestral",
            },
        });

        Assert.Equal(
            ("Manutenção periódica aberta", "Programada na conclusão da manutenção periódica anterior."),
            (entry.Title, entry.Description));
        Assert.Equal(
            [
                new TimelineDetailData("Data prevista", "16/04/2027"),
                new TimelineDetailData("Periodicidade", "180 dias"),
                new TimelineDetailData("Descrição", "Aferição semestral"),
            ],
            entry.Details);
    }

    [Fact]
    public async Task MaintenanceOpened_Automatic_SaysItWasOpenedByTheMalfunction_RN07()
    {
        var entry = await EquipmentEntryAsync(Fact(TimelineEntryKind.MaintenanceOpened) with
        {
            Maintenance = Maintenance(DomainMaintenanceKind.Immediate) with { Origin = DomainMaintenanceOrigin.AutomaticOnMalfunction },
        });

        Assert.Equal(
            ("Manutenção imediata aberta", "Aberta automaticamente pelo mau funcionamento."),
            (entry.Title, entry.Description));
        Assert.Empty(entry.Details);
    }

    [Fact]
    public async Task MaintenanceCancelled_ShowsTheCancellationReason_RN35()
    {
        var entry = await EquipmentEntryAsync(Fact(TimelineEntryKind.MaintenanceCancelled) with
        {
            Maintenance = Maintenance(DomainMaintenanceKind.Scheduled) with
            {
                CancellationReason = "Cancelada automaticamente: equipamento inativado.",
            },
        });

        Assert.Equal(
            ("Manutenção agendada cancelada", "Cancelada automaticamente: equipamento inativado."),
            (entry.Title, entry.Description));
    }

    // Cadastro (D-49, RN-35)

    [Fact]
    public async Task Inactivated_TitleAndReason_RN35()
    {
        var entry = await EquipmentEntryAsync(Fact(TimelineEntryKind.Inactivated) with
        {
            Registration = new([], "Fim do contrato de concessão."),
        });

        Assert.Equal("Cadastro inativado", entry.Title);
        Assert.Equal([new TimelineDetailData("Motivo", "Fim do contrato de concessão.")], entry.Details);

        var withoutReason = await EquipmentEntryAsync(Fact(TimelineEntryKind.Inactivated) with { Registration = new([], null) });
        Assert.Empty(withoutReason.Details);
    }

    [Fact]
    public async Task RegistrationUpdated_ListsEachFieldWithBeforeAndAfter_D49()
    {
        var entry = await EquipmentEntryAsync(Fact(TimelineEntryKind.RegistrationUpdated) with
        {
            Registration = new(
                [
                    new FieldChange("organizationId", "Órgão responsável", "Secretaria Municipal de Mobilidade", "Guarda Civil Municipal"),
                    new FieldChange("attributes.monitoredLanes", "Faixas monitoradas", null, "2"),
                ],
                null),
        });

        Assert.Equal(("Cadastro alterado", "2 campos alterados."), (entry.Title, entry.Description));
        Assert.Equal(
            [
                new TimelineDetailData("Órgão responsável", "Secretaria Municipal de Mobilidade → Guarda Civil Municipal"),
                new TimelineDetailData("Faixas monitoradas", "(vazio) → 2"),
            ],
            entry.Details);
    }

    // Vínculos (US-07, RF-018)

    [Fact]
    public async Task Crew_OnTheEquipmentTimeline_DescribesTheAgent_US07()
    {
        _services.Timeline.Facts.AddRange(
        [
            Fact(TimelineEntryKind.CrewUnassigned) with { Crew = Crew(DomainEndReason.EquipmentInactivated) },
            Fact(TimelineEntryKind.CrewAssigned) with { Crew = Crew(null) },
        ]);

        var page = await _services.TimelineService().ForEquipmentAsync(_equipmentId, new PageQuery(), Ct);

        Assert.Equal(
            [("Agente desvinculado", "Falcão · matrícula GCM-0042"), ("Agente vinculado", "Falcão · matrícula GCM-0042")],
            page.Items.Select(entry => (entry.Title, entry.Description)));
        Assert.Equal([new TimelineDetailData("Motivo", "Equipamento inativado")], page.Items[0].Details);
        Assert.Empty(page.Items[1].Details);
    }

    [Theory]
    [InlineData(DomainEndReason.Reassigned, "Agente vinculado a outro equipamento")]
    [InlineData(DomainEndReason.Ended, "Encerrado manualmente")]
    [InlineData(DomainEndReason.AgentInactivated, "Agente inativado")]
    public async Task Crew_OnTheAgentTimeline_DescribesTheEquipmentAndWhyItEnded_US07(DomainEndReason reason, string label)
    {
        _services.Timeline.Facts.AddRange(
        [
            Fact(TimelineEntryKind.CrewUnassigned) with { Crew = Crew(reason) },
            Fact(TimelineEntryKind.CrewAssigned) with { Crew = Crew(null) },
        ]);

        var page = await _services.TimelineService().ForAgentAsync(_agentId, new PageQuery(), Ct);

        Assert.Equal(
            [("Vínculo encerrado", "Viatura · 9BWZZZ377VT004251"), ("Vinculado a equipamento", "Viatura · 9BWZZZ377VT004251")],
            page.Items.Select(entry => (entry.Title, entry.Description)));
        Assert.Equal([new TimelineDetailData("Motivo", label)], page.Items[0].Details);
    }

    // Consulta (US-08, Art. V.1)

    [Fact]
    public async Task Entries_KeepTheOrderInstantAuthorAndPagingOfTheQuery_US08()
    {
        _services.Timeline.Facts.AddRange(
        [
            Fact(TimelineEntryKind.Inactivated, At.AddDays(2), "admin") with { Registration = new([], null) },
            Fact(TimelineEntryKind.StatusChanged, At.AddDays(1), "PROMETHEUS") with
            {
                EquipmentStatus = new(DomainEquipmentStatus.Operational, DomainEquipmentStatus.Deactivated, DomainStatusChangeOrigin.Manual, "Fim."),
            },
            Fact(TimelineEntryKind.Registered, At, "simulação") with
            {
                EquipmentStatus = new(null, DomainEquipmentStatus.Operational, DomainStatusChangeOrigin.Registration, null),
            },
        ]);

        var page = await _services.TimelineService().ForEquipmentAsync(_equipmentId, new PageQuery { Page = 1, PageSize = 2 }, Ct);

        Assert.Equal(
            [
                (TimelineEntryKind.Inactivated, At.AddDays(2), "admin"),
                (TimelineEntryKind.StatusChanged, At.AddDays(1), "PROMETHEUS"),
            ],
            page.Items.Select(entry => (entry.Kind, entry.OccurredAt, entry.Author)));
        Assert.Equal((1, 2, 3), (page.Page, page.PageSize, page.TotalCount));
        Assert.Equal(("equipment", _equipmentId, 1, 2), _services.Timeline.LastRequest);
    }

    [Fact]
    public async Task UnknownEquipmentOrAgent_IsNotFound()
    {
        var equipment = await Assert.ThrowsAsync<NotFoundException>(
            () => _services.TimelineService().ForEquipmentAsync(Guid.NewGuid(), new PageQuery(), Ct));
        var agent = await Assert.ThrowsAsync<NotFoundException>(
            () => _services.TimelineService().ForAgentAsync(Guid.NewGuid(), new PageQuery(), Ct));

        Assert.Equal(("Equipamento não encontrado.", "Agente não encontrado."), (equipment.Message, agent.Message));
        Assert.Null(_services.Timeline.LastRequest);
    }

    [Fact]
    public async Task PageOutOfRange_IsValidationErrorOnPageAndPageSize()
    {
        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => _services.TimelineService()
            .ForAgentAsync(_agentId, new PageQuery { Page = 0, PageSize = 101 }, Ct));

        Assert.Equal(["page", "pageSize"], exception.Errors.Keys.Order(StringComparer.Ordinal));
    }

    private static TimelineFact Fact(TimelineEntryKind kind, DateTimeOffset? at = null, string author = "admin") =>
        new(kind, at ?? At, author);

    private static TimelineMaintenance Maintenance(DomainMaintenanceKind kind) =>
        new(kind, DomainMaintenanceOrigin.Manual, null, null, null, null, null, null);

    private TimelineCrew Crew(DomainEndReason? reason) =>
        new(_agentId, "Falcão", "GCM-0042", _equipmentId, "Viatura", "9BWZZZ377VT004251", reason);

    private async Task<TimelineEntry> EquipmentEntryAsync(TimelineFact fact)
    {
        _services.Timeline.Facts.Clear();
        _services.Timeline.Facts.Add(fact);
        var page = await _services.TimelineService().ForEquipmentAsync(_equipmentId, new PageQuery(), Ct);
        return Assert.Single(page.Items);
    }
}
