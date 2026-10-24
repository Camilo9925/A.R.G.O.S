using Argos.Application.Common.Abstractions;

namespace Argos.Application.Seeding.Inventory;

/// <summary>Versões da carga do Inventário e o que elas têm em comum (plan 15.11.4; D-56).</summary>
public static class InventorySeed
{
    /// <summary>Município, zonas, bairros, CEPs, órgãos, bases e tipos.</summary>
    public const string ReferenceVersion = "inventory-reference-v1";

    /// <summary>Equipamentos, situações e manutenções.</summary>
    public const string CatalogVersion = "inventory-catalog-v1";

    /// <summary>Fase 3: agentes e vínculos com motos e viaturas.</summary>
    public const string AgentsVersion = "inventory-agents-v1";

    /// <summary>Fase 3: inativações de demonstração de equipamentos e agentes (RN-35), depois dos agentes.</summary>
    public const string InactivationsVersion = "inventory-inactivations-v1";

    /// <summary>
    /// Autor dos registros de negócio da carga, para que o histórico mostre que são simulados (Art. IX.2; D-56). A
    /// auditoria técnica continua com o autor do processo (<c>sistema</c>, D-43).
    /// </summary>
    public const string Author = "simulação";

    /// <summary>
    /// Diz se a versão deve ser aplicada: não, se já foi com a mesma semente; sim, se nunca foi. Aplicada com outra
    /// semente, a carga para com uma mensagem clara, em vez de misturar dois catálogos ou falhar na unicidade.
    /// </summary>
    internal static async Task<bool> ShouldApplyAsync(
        ISeedLedger ledger, string version, int seed, CancellationToken cancellationToken)
    {
        var appliedSeed = await ledger.FindAppliedSeedAsync(version, cancellationToken);

        if (appliedSeed is null)
        {
            return true;
        }

        return appliedSeed == seed
            ? false
            : throw new InvalidOperationException(
                $"A carga {version} já foi aplicada com a semente {appliedSeed}. Para usar a semente {seed}, "
                + "recomece o banco (docker compose down -v).");
    }
}
