using Argos.Contracts.Inventory;
using Argos.Contracts.Operations;
using Argos.Contracts.Realtime;
using Argos.Domain.Inventory.Types;
using Argos.UnitTests.Application.Inventory;
using ContractAgentRole = Argos.Contracts.Inventory.AgentRole;
using ContractAgentStatus = Argos.Contracts.Inventory.AgentStatus;
using ContractStatus = Argos.Contracts.Inventory.EquipmentStatus;

namespace Argos.UnitTests.Application.Operations;

/// <summary>
/// D-80 nos casos de uso do Inventário que mudam o que a Central mostra (plan 16.8): cadastro, edição e inativação de
/// agente, vínculo e encerramento, inativação e edição de equipamento. Cada um publica um só <c>OperationsChanged</c>,
/// para todos, com o tipo esperado e depois da gravação (as gravações feitas até a publicação são todas as do caso).
/// </summary>
public sealed class InventoryNotificationsTests
{
    private readonly InventoryServices _services = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string> Writes() =>
    [
        "agent-create",
        "agent-update",
        "agent-inactivate",
        "assignment-start",
        "assignment-end",
        "equipment-inactivate",
        "equipment-update",
    ];

    [Theory]
    [MemberData(nameof(Writes))]
    public async Task InventoryWrite_PublishesOperationsChangedAfterTheSave_D80(string write)
    {
        var act = await ArrangeAsync(write);
        _services.Notifier.Messages.Clear();
        var savesBefore = _services.UnitOfWork.SaveCalls;

        await act();

        var message = Assert.Single(_services.Notifier.Messages);
        Assert.Equal(savesBefore + 1, _services.UnitOfWork.SaveCalls);
        Assert.Equal(
            ((string?)null, OperationsHubContract.Messages.OperationsChanged, _services.UnitOfWork.SaveCalls),
            (message.Permission, message.Name, message.SavesBefore));
        Assert.Equal(OperationsChangeKind.Registration, ((OperationsChanged)message.Message).Kind);
    }

    /// <summary>Prepara o cenário do caso e devolve a escrita a conferir.</summary>
    private async Task<Func<Task>> ArrangeAsync(string write)
    {
        switch (write)
        {
            case "agent-create":
                return () => _services.AgentService().CreateAsync(AgentRequest(), Ct);

            case "agent-update":
            {
                var agent = await _services.AgentService().CreateAsync(AgentRequest(), Ct);
                return () => _services.AgentService().UpdateAsync(agent.Id, new UpdateAgentRequest
                {
                    FullName = agent.FullName,
                    DisplayName = "Gavião",
                    RegistrationNumber = agent.RegistrationNumber,
                    Role = agent.Role,
                    OrganizationId = agent.OrganizationId,
                    BaseId = agent.BaseId,
                    Version = agent.Version,
                }, Ct);
            }

            case "agent-inactivate":
            {
                var agent = await _services.AgentService().CreateAsync(AgentRequest(), Ct);
                await _services.AgentStatusService().ChangeAsync(
                    agent.Id, new ChangeAgentStatusRequest { Status = ContractAgentStatus.OnLeave }, Ct);
                return () => _services.AgentService().InactivateAsync(agent.Id, new InactivationRequest(), Ct);
            }

            case "assignment-start":
            {
                var agent = await _services.AgentService().CreateAsync(AgentRequest(), Ct);
                var patrol = await _services.EquipmentService().CreateAsync(PatrolRequest(), Ct);
                return () => _services.AssignmentService().AssignAsync(
                    agent.Id, new AssignEquipmentRequest { EquipmentId = patrol.Id }, Ct);
            }

            case "assignment-end":
            {
                var agent = await _services.AgentService().CreateAsync(AgentRequest(), Ct);
                var patrol = await _services.EquipmentService().CreateAsync(PatrolRequest(), Ct);
                var assignment = await _services.AssignmentService().AssignAsync(
                    agent.Id, new AssignEquipmentRequest { EquipmentId = patrol.Id }, Ct);
                return () => _services.AssignmentService().EndAsync(agent.Id, assignment.Id, Ct);
            }

            case "equipment-inactivate":
            {
                var patrol = await _services.EquipmentService().CreateAsync(
                    PatrolRequest() with { Status = ContractStatus.NotInstalled }, Ct);
                return () => _services.EquipmentInactivationService().InactivateAsync(patrol.Id, new InactivationRequest(), Ct);
            }

            case "equipment-update":
            {
                var request = PatrolRequest();
                var patrol = await _services.EquipmentService().CreateAsync(request, Ct);
                return () => _services.EquipmentService().UpdateAsync(patrol.Id, new UpdateEquipmentRequest
                {
                    SerialNumber = request.SerialNumber,
                    Brand = request.Brand,
                    Model = "Patrulha 4x4 Plus",
                    OrganizationId = request.OrganizationId,
                    BaseId = request.BaseId,
                    Attributes = request.Attributes,
                    Version = patrol.Version,
                }, Ct);
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(write), write, "Caso desconhecido.");
        }
    }

    private CreateAgentRequest AgentRequest() => new()
    {
        FullName = "Agente Fictício de Teste",
        DisplayName = "Carcará",
        RegistrationNumber = "SMM-0101",
        Role = ContractAgentRole.TrafficAgent,
        OrganizationId = _services.Organization.Id,
        BaseId = _services.Base.Id,
    };

    private CreateEquipmentRequest PatrolRequest() => new()
    {
        TypeId = _services.Type(EquipmentTypeCatalog.PatrolVehicle).Id,
        SerialNumber = "9BWZZZ377VT004251",
        Brand = "Orbis",
        Model = "Patrulha 4x4",
        OrganizationId = _services.Organization.Id,
        BaseId = _services.Base.Id,
        Attributes = new() { ["licensePlate"] = "ABC1D23", ["prefix"] = "SMM-01", ["vehicleKind"] = "patrol-car" },
        Status = ContractStatus.Operational,
    };
}
