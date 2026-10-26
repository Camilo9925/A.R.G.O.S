namespace Argos.Contracts.Operations;

// Espelhos dos enums do Domínio da Telemetria e da Central (D-61; plan 16.10): os mesmos nomes de itens, que trafegam
// como texto. Paridade garantida por teste unitário. PositionSource e OperationsChangeKind existem só no contrato.

/// <summary>Tipo do recurso de uma posição, de um estado atual ou de um alerta (plan 16.2).</summary>
public enum ResourceKind
{
    Equipment,
    Agent,
}

/// <summary>Causa de um alerta (RN-18, parcial; D-77).</summary>
public enum AlertCause
{
    /// <summary>Equipamento furtado.</summary>
    EquipmentStolen,

    /// <summary>Equipamento em mau funcionamento.</summary>
    EquipmentMalfunction,
}

/// <summary>Severidade de um alerta (RN-18), da mais grave para a menos grave.</summary>
public enum AlertSeverity
{
    Critical,
    High,
    Medium,
    Low,
}

/// <summary>Estado de um alerta (RN-20).</summary>
public enum AlertState
{
    /// <summary>Aberto e ainda não reconhecido.</summary>
    Open,

    Acknowledged,

    Resolved,
}

/// <summary>Ação que o usuário pode fazer no alerta agora (RN-20; D-59).</summary>
public enum AlertAction
{
    Acknowledge,
    Resolve,
}

/// <summary>Turno da escala de referência simulada (D-75, D-94), também usado pela Preditiva na Fase 5.</summary>
public enum DutyShift
{
    /// <summary>06h às 14h.</summary>
    Morning,

    /// <summary>14h às 22h.</summary>
    Afternoon,

    /// <summary>22h às 06h.</summary>
    Night,
}

/// <summary>De onde vem a posição de um marcador de equipamento (plan 16.7). Só do contrato.</summary>
public enum PositionSource
{
    /// <summary>Localização cadastral do equipamento fixo.</summary>
    Installed,

    /// <summary>Posição atual recebida pela telemetria.</summary>
    Live,

    /// <summary>Posição da base do móvel, sem posição atual (ou oculta pela RN-27, A-38).</summary>
    Base,
}

/// <summary>O que mudou na Central, no aviso <c>OperationsChanged</c> (plan 16.11). Só do contrato.</summary>
public enum OperationsChangeKind
{
    EquipmentStatus,
    AgentStatus,
    Alert,
    Registration,
    Maintenance,
}
