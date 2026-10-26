namespace Argos.Domain.Operations.Alerts;

/// <summary>Causa de um alerta (RN-18, parcial na entrega: só as duas mudanças de situação; D-77).</summary>
public enum AlertCause
{
    /// <summary>Equipamento marcado como Furtado.</summary>
    EquipmentStolen,

    /// <summary>Equipamento em Mau funcionamento.</summary>
    EquipmentMalfunction,
}

/// <summary>
/// Severidade de um alerta (RN-18), da mais grave para a menos grave: a ordem dos itens é a ordem da lista (RF-035).
/// A escala é a inteira da RN-18; a entrega só usa as duas primeiras.
/// </summary>
public enum AlertSeverity
{
    Critical,
    High,
    Medium,
    Low,
}

/// <summary>Estado de um alerta (RN-20): Aberto → Reconhecido → Resolvido; o não crítico pode ir de Aberto a Resolvido.</summary>
public enum AlertState
{
    /// <summary>Aberto e ainda não reconhecido (o que pisca na Central).</summary>
    Open,

    Acknowledged,

    Resolved,
}

/// <summary>Ação que um alerta aceita no estado atual (RN-20; D-59).</summary>
public enum AlertAction
{
    Acknowledge,
    Resolve,
}
