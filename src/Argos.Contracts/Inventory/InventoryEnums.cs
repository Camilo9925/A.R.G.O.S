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
