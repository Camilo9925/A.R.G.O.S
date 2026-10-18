namespace Argos.Domain.Inventory.Equipments;

/// <summary>Situação de um equipamento (RN-04).</summary>
public enum EquipmentStatus
{
    /// <summary>Ainda não instalado.</summary>
    NotInstalled,

    /// <summary>Operacional.</summary>
    Operational,

    /// <summary>Mau funcionamento.</summary>
    Malfunction,

    /// <summary>Em manutenção.</summary>
    UnderMaintenance,

    /// <summary>Desativado.</summary>
    Deactivated,

    /// <summary>Furtado.</summary>
    Stolen,
}

/// <summary>De onde veio uma mudança de situação (plan 15.2.7). A rodada da Telemetria acrescentará <c>Telemetry</c>.</summary>
public enum StatusChangeOrigin
{
    /// <summary>Registro inicial, no cadastro (US-01).</summary>
    Registration,

    /// <summary>Mudança feita por uma pessoa (US-04).</summary>
    Manual,

    /// <summary>Consequência da conclusão de uma manutenção (RN-11).</summary>
    MaintenanceCompletion,
}

/// <summary>Uma situação de destino oferecida a partir da atual, já filtrada pela permissão (D-59).</summary>
public sealed record StatusChangeOption(EquipmentStatus Target, bool RequiresJustification);

/// <summary>Rótulos em pt-BR das situações, usados nas mensagens do Domínio (Art. XIII.2).</summary>
public static class EquipmentStatusLabels
{
    public static string For(EquipmentStatus status) => status switch
    {
        EquipmentStatus.NotInstalled => "Ainda não instalado",
        EquipmentStatus.Operational => "Operacional",
        EquipmentStatus.Malfunction => "Mau funcionamento",
        EquipmentStatus.UnderMaintenance => "Em manutenção",
        EquipmentStatus.Deactivated => "Desativado",
        EquipmentStatus.Stolen => "Furtado",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Situação desconhecida."),
    };
}
