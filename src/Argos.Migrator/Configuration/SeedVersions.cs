using Argos.Application.Seeding.Inventory;

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
}
