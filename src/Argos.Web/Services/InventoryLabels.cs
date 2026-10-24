using Argos.Contracts.Inventory;
using MudBlazor;

namespace Argos.Web.Services;

/// <summary>
/// Textos em pt-BR dos enums do Inventário que a interface mostra (Art. X.1, XIII). Só rótulos: as regras ficam na API.
/// Situação e necessidade de manutenção têm cor e ícone em <c>StatusTokens</c> e <c>MaintenanceNeedTokens</c>.
/// </summary>
public static class InventoryLabels
{
    public static string For(EquipmentCategory category) => category switch
    {
        EquipmentCategory.Fixed => "Fixo",
        EquipmentCategory.Mobile => "Móvel",
        _ => category.ToString(),
    };

    public static string For(MaintenanceKind kind) => kind switch
    {
        MaintenanceKind.Immediate => "Imediata",
        MaintenanceKind.Scheduled => "Agendada",
        MaintenanceKind.Periodic => "Periódica",
        _ => kind.ToString(),
    };

    public static string For(MaintenanceState state) => state switch
    {
        MaintenanceState.Open => "Aberta",
        MaintenanceState.Completed => "Concluída",
        MaintenanceState.Cancelled => "Cancelada",
        _ => state.ToString(),
    };

    public static string For(MaintenanceResult result) => result switch
    {
        MaintenanceResult.Success => "Com sucesso",
        MaintenanceResult.Failure => "Sem sucesso",
        _ => result.ToString(),
    };

    public static string For(MaintenanceOrigin origin) => origin switch
    {
        MaintenanceOrigin.Manual => "Registrada manualmente",
        MaintenanceOrigin.AutomaticOnMalfunction => "Aberta automaticamente (mau funcionamento)",
        MaintenanceOrigin.PeriodicRescheduling => "Reprogramação da periódica",
        _ => origin.ToString(),
    };

    public static string For(StatusChangeOrigin origin) => origin switch
    {
        StatusChangeOrigin.Registration => "Cadastro",
        StatusChangeOrigin.Manual => "Alteração manual",
        StatusChangeOrigin.MaintenanceCompletion => "Conclusão de manutenção",
        _ => origin.ToString(),
    };

    public static string For(TimelineEntryKind kind) => kind switch
    {
        TimelineEntryKind.Registered => "Cadastro",
        TimelineEntryKind.StatusChanged => "Mudança de situação",
        TimelineEntryKind.MaintenanceOpened => "Manutenção aberta",
        TimelineEntryKind.MaintenanceCompleted => "Manutenção concluída",
        TimelineEntryKind.MaintenanceCancelled => "Manutenção cancelada",
        TimelineEntryKind.RegistrationUpdated => "Alteração de cadastro",
        TimelineEntryKind.Inactivated => "Inativação",
        TimelineEntryKind.CrewAssigned => "Vínculo iniciado",
        TimelineEntryKind.CrewUnassigned => "Vínculo encerrado",
        _ => kind.ToString(),
    };

    /// <summary>Ícone e cor do tipo de entrada da linha do tempo: o tipo nunca aparece só pela cor (Art. X.2).</summary>
    public static (string Icon, Color Color) AppearanceFor(TimelineEntryKind kind) => kind switch
    {
        TimelineEntryKind.Registered => (Icons.Material.Filled.AddCircle, Color.Success),
        TimelineEntryKind.StatusChanged => (Icons.Material.Filled.SwapHoriz, Color.Info),
        TimelineEntryKind.MaintenanceOpened => (Icons.Material.Filled.Build, Color.Warning),
        TimelineEntryKind.MaintenanceCompleted => (Icons.Material.Filled.TaskAlt, Color.Success),
        TimelineEntryKind.MaintenanceCancelled => (Icons.Material.Filled.EventBusy, Color.Default),
        TimelineEntryKind.RegistrationUpdated => (Icons.Material.Filled.Edit, Color.Primary),
        TimelineEntryKind.Inactivated => (Icons.Material.Outlined.DoNotDisturbOn, Color.Secondary),
        TimelineEntryKind.CrewAssigned => (Icons.Material.Filled.Link, Color.Tertiary),
        TimelineEntryKind.CrewUnassigned => (Icons.Material.Filled.LinkOff, Color.Default),
        _ => (Icons.Material.Filled.Circle, Color.Default),
    };

    public static string For(AgentRole role) => role switch
    {
        AgentRole.TrafficAgent => "Agente de trânsito",
        AgentRole.MunicipalGuard => "Guarda municipal",
        AgentRole.MilitaryPolice => "Policial militar",
        _ => role.ToString(),
    };

    public static string For(AgentStatus status) => Theme.AgentStatusTokens.For(status).Label;

    /// <summary>Filtro de cadastros ativos e inativos das listas (US-09).</summary>
    public static string For(Argos.Contracts.Common.ActivityFilter activity) => activity switch
    {
        Argos.Contracts.Common.ActivityFilter.Active => "Ativos",
        Argos.Contracts.Common.ActivityFilter.Inactive => "Inativos",
        Argos.Contracts.Common.ActivityFilter.All => "Ativos e inativos",
        _ => activity.ToString(),
    };

    public static string For(EquipmentStatus status) => Theme.StatusTokens.For(status).Label;

    public static string For(MaintenanceNeed need) => Theme.MaintenanceNeedTokens.For(need).Label;

    /// <summary>Ícone do tipo de equipamento pela chave do catálogo (<see cref="EquipmentIcons"/>).</summary>
    public static string IconFor(string iconKey) => iconKey switch
    {
        EquipmentIcons.Radar => Icons.Material.Filled.Speed,
        EquipmentIcons.Camera => Icons.Material.Filled.Videocam,
        EquipmentIcons.Scale => Icons.Material.Filled.Scale,
        EquipmentIcons.Gate => Icons.Material.Filled.Toll,
        EquipmentIcons.Kiosk => Icons.Material.Filled.LocalParking,
        EquipmentIcons.AutonomousVehicle => Icons.Material.Filled.DirectionsCar,
        EquipmentIcons.Taxi => Icons.Material.Filled.LocalTaxi,
        EquipmentIcons.Scooter => Icons.Material.Filled.ElectricScooter,
        EquipmentIcons.Motorcycle => Icons.Material.Filled.TwoWheeler,
        EquipmentIcons.PatrolCar => Icons.Material.Filled.LocalPolice,
        EquipmentIcons.Sensor => Icons.Material.Filled.Sensors,
        EquipmentIcons.Water => Icons.Material.Filled.WaterDrop,
        EquipmentIcons.Light => Icons.Material.Filled.Lightbulb,
        EquipmentIcons.Sign => Icons.Material.Filled.Signpost,
        _ => Icons.Material.Filled.Devices,
    };
}
