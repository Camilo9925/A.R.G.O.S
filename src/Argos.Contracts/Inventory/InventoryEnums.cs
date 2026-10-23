namespace Argos.Contracts.Inventory;

// Espelhos dos enums do Domínio (D-61; plan 15.14): os mesmos nomes de itens, que trafegam como texto (plan 4.1) e
// são convertidos por nome. Um teste unitário garante a paridade nos dois sentidos.

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

/// <summary>Categoria do tipo de equipamento: fixo (com localização) ou móvel (com base), RF-007.</summary>
public enum EquipmentCategory
{
    Fixed,
    Mobile,
}

/// <summary>De onde veio uma mudança de situação (plan 15.2.7).</summary>
public enum StatusChangeOrigin
{
    /// <summary>Registro inicial, no cadastro (US-01).</summary>
    Registration,

    /// <summary>Mudança feita por uma pessoa (US-04).</summary>
    Manual,

    /// <summary>Consequência da conclusão de uma manutenção (RN-11).</summary>
    MaintenanceCompletion,
}

/// <summary>Tipo de manutenção (RN-08).</summary>
public enum MaintenanceKind
{
    Immediate,
    Scheduled,
    Periodic,
}

/// <summary>Estado de uma manutenção (RN-08).</summary>
public enum MaintenanceState
{
    Open,
    Completed,
    Cancelled,
}

/// <summary>Resultado da conclusão de uma manutenção (US-05, RN-11).</summary>
public enum MaintenanceResult
{
    Success,
    Failure,
}

/// <summary>De onde veio a manutenção (plan 15.2.8).</summary>
public enum MaintenanceOrigin
{
    /// <summary>Registrada por uma pessoa ou como manutenção inicial do cadastro.</summary>
    Manual,

    /// <summary>Aberta pela entrada em Mau funcionamento (RN-07) ou pela conclusão sem sucesso (RN-11).</summary>
    AutomaticOnMalfunction,

    /// <summary>Próxima periódica, programada na conclusão da anterior (RN-09).</summary>
    PeriodicRescheduling,
}

/// <summary>Necessidade de manutenção (RF-002, RN-10; D-54), da menos para a mais grave.</summary>
public enum MaintenanceNeed
{
    None,
    Scheduled,
    Upcoming,
    Overdue,
    Immediate,
}

/// <summary>Tipo de dado de uma característica (plan 15.2.5).</summary>
public enum AttributeDataType
{
    Text,
    Integer,
    Decimal,
    Boolean,
    Option,
    MultiOption,
}

/// <summary>Função do agente (RF-014).</summary>
public enum AgentRole
{
    /// <summary>Agente de trânsito.</summary>
    TrafficAgent,

    /// <summary>Guarda municipal.</summary>
    MunicipalGuard,

    /// <summary>Policial militar.</summary>
    MilitaryPolice,
}

/// <summary>Situação de um agente (RN-12). A inicial é Fora de serviço (US-06).</summary>
public enum AgentStatus
{
    /// <summary>Em serviço.</summary>
    OnDuty,

    /// <summary>Em atendimento.</summary>
    Responding,

    /// <summary>Em pausa.</summary>
    OnBreak,

    /// <summary>Fora de serviço.</summary>
    OffDuty,

    /// <summary>Afastado.</summary>
    OnLeave,
}

/// <summary>Por que um vínculo entre agente e equipamento móvel foi encerrado (plan 15.2.10).</summary>
public enum AssignmentEndReason
{
    /// <summary>O agente foi vinculado a outro equipamento (US-07).</summary>
    Reassigned,

    /// <summary>Encerrado por uma pessoa (RF-016).</summary>
    Ended,

    /// <summary>O agente foi inativado (RN-35).</summary>
    AgentInactivated,

    /// <summary>O equipamento foi inativado (RN-35).</summary>
    EquipmentInactivated,
}

/// <summary>
/// Tipo de uma entrada da linha do tempo (US-08; plan 15.6). Não espelha um enum do Domínio: a linha do tempo é uma
/// projeção das fontes do histórico, montada pela consulta e pela Aplicação. A Central (Camada 2) acrescentará os de
/// alerta.
/// </summary>
public enum TimelineEntryKind
{
    /// <summary>Cadastro, com a situação inicial (US-01, US-06).</summary>
    Registered,

    /// <summary>Mudança de situação (RN-05, RN-12).</summary>
    StatusChanged,

    /// <summary>Manutenção aberta (RN-07, RN-08, RN-09).</summary>
    MaintenanceOpened,

    /// <summary>Manutenção concluída, com sucesso ou sem sucesso (US-05).</summary>
    MaintenanceCompleted,

    /// <summary>Manutenção cancelada automaticamente (RN-11, RN-35).</summary>
    MaintenanceCancelled,

    /// <summary>Alteração de cadastro, com os campos alterados (D-49).</summary>
    RegistrationUpdated,

    /// <summary>Inativação do cadastro (RN-35).</summary>
    Inactivated,

    /// <summary>Início de um vínculo entre agente e equipamento móvel (US-07).</summary>
    CrewAssigned,

    /// <summary>Encerramento de um vínculo (US-07, RN-35).</summary>
    CrewUnassigned,
}
