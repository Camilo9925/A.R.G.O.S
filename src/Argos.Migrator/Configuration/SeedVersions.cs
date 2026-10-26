using Argos.Application.Seeding.Inventory;
using Argos.Application.Seeding.Operations;

namespace Argos.Migrator.Configuration;

/// <summary>
/// Versões da carga de dados registradas no histórico do seed (plan 7.4).
/// </summary>
internal static class SeedVersions
{
    /// <summary>Fase 0: só o esqueleto, sem catálogo nem histórico simulado.</summary>
    public const string Foundation = "foundation";

    /// <summary>Fase 2: município, zonas, bairros, CEPs, órgãos, bases e tipos (D-56).</summary>
    public const string InventoryReference = InventorySeed.ReferenceVersion;

    /// <summary>Fase 2: equipamentos, situações e manutenções (D-56).</summary>
    public const string InventoryCatalog = InventorySeed.CatalogVersion;

    /// <summary>Fase 3: agentes e vínculos com motos e viaturas (D-56).</summary>
    public const string InventoryAgents = InventorySeed.AgentsVersion;

    /// <summary>Fase 3: inativações de demonstração (RN-35; D-56).</summary>
    public const string InventoryInactivations = InventorySeed.InactivationsVersion;

    /// <summary>Fase 4: escala de referência simulada (D-94).</summary>
    public const string OperationsDutyRoster = OperationsSeed.DutyRosterVersion;

    /// <summary>Fase 4: alertas dos equipamentos em Mau funcionamento ou Furtado (D-78).</summary>
    public const string OperationsAlerts = OperationsSeed.AlertsVersion;
}
