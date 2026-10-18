using Argos.Application.Seeding.Inventory;

namespace Argos.IntegrationTests.Infrastructure;

/// <summary>
/// Entrada pequena da carga <c>inventory-reference-v1</c> para os testes: município quadrado, uma zona, dois bairros (um
/// com acento, para a busca), um CEP, um órgão e uma base no Centro. Os dez tipos vêm do catálogo do Domínio.
/// </summary>
internal static class InventorySeedData
{
    public const string Saint = "São Jorge";
    public const string Downtown = "Centro";
    public const string Organization = "DMT";
    public const string Base = "Base Central";

    /// <summary>Âncora fixa dos testes (D-03).</summary>
    public static readonly DateTimeOffset Anchor = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly SeedPoint[] Square =
    [
        new(-22.20, -49.70), new(-22.20, -49.60), new(-22.30, -49.60), new(-22.30, -49.70), new(-22.20, -49.70),
    ];

    public static InventoryReferenceSeed Reference(IReadOnlyList<NeighborhoodSeed>? neighborhoods = null) =>
        new(
            new MunicipalitySeed("Prometeia", "SP", "America/Sao_Paulo", Square, new SeedPoint(-22.25, -49.65)),
            [new RegionSeed("Zona Única", Square)],
            neighborhoods ??
            [
                new NeighborhoodSeed(Saint, "Zona Única", new SeedPoint(-22.24, -49.64)),
                new NeighborhoodSeed(Downtown, "Zona Única", new SeedPoint(-22.26, -49.66)),
            ],
            [new ZipCodeSeed("16985001", "Rua das Flores", Saint, new SeedPoint(-22.241, -49.641))],
            [new OrganizationSeed(Organization, "Departamento Municipal de Trânsito")],
            [
                new OperationalBaseSeed(
                    Base, Organization, new LocationSeed("16985002", "Avenida Brasil", Downtown, "número 100", -22.26, -49.66)),
            ]);
}
