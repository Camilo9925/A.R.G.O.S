using Argos.Domain.Inventory.Equipments;

namespace Argos.Application.Seeding.Inventory;

// Entrada da carga do Inventário (plan 15.11.4; D-56): records próprios da Aplicação, montados pelo Migrator a partir
// dos dados de Prometeia e do catálogo gerado pela Simulação, que a Aplicação não referencia. As referências entre os
// cadastros são pelos nomes (região, bairro, sigla do órgão, base), únicos na carga.

/// <summary>Coordenada em graus decimais.</summary>
public sealed record SeedPoint(double Latitude, double Longitude);

/// <summary>Versão <c>inventory-reference-v1</c>: geografia, CEPs, órgãos e bases. Os tipos vêm do catálogo do Domínio.</summary>
public sealed record InventoryReferenceSeed(
    MunicipalitySeed Municipality,
    IReadOnlyList<RegionSeed> Regions,
    IReadOnlyList<NeighborhoodSeed> Neighborhoods,
    IReadOnlyList<ZipCodeSeed> ZipCodes,
    IReadOnlyList<OrganizationSeed> Organizations,
    IReadOnlyList<OperationalBaseSeed> Bases);

/// <param name="Boundary">Anel fechado do contorno (o primeiro vértice repetido no fim), como exige o Domínio.</param>
public sealed record MunicipalitySeed(
    string Name, string State, string TimeZoneId, IReadOnlyList<SeedPoint> Boundary, SeedPoint Center);

/// <param name="Boundary">Anel fechado do contorno da zona.</param>
public sealed record RegionSeed(string Name, IReadOnlyList<SeedPoint> Boundary);

public sealed record NeighborhoodSeed(string Name, string RegionName, SeedPoint Center);

/// <summary>Um CEP: um logradouro dentro de um bairro, com o ponto de referência do trecho (D-52).</summary>
public sealed record ZipCodeSeed(string ZipCode, string Street, string NeighborhoodName, SeedPoint ReferencePoint);

public sealed record OrganizationSeed(string Acronym, string Name);

/// <summary>Localização sobre um logradouro, com a referência no formato da RN-03.</summary>
public sealed record LocationSeed(
    string ZipCode, string Street, string NeighborhoodName, string Reference, double Latitude, double Longitude);

public sealed record OperationalBaseSeed(string Name, string OrganizationAcronym, LocationSeed Location);

/// <summary>Versão <c>inventory-catalog-v1</c>: os equipamentos, com situações e manutenções.</summary>
public sealed record EquipmentCatalogSeed(IReadOnlyList<EquipmentSeed> Equipment);

/// <summary>
/// Um equipamento do catálogo, com os fatos de negócio relativos à data-âncora (D-03, D-56). A carga o cria pelas
/// mesmas operações do Domínio dos casos de uso, nesta ordem: cadastro, manutenção periódica e mudança de situação.
/// </summary>
/// <param name="TypeKey">Chave do tipo no catálogo inicial, como <c>traffic-radar</c>.</param>
/// <param name="Location">Só para fixos.</param>
/// <param name="BaseName">Nome da base do órgão do equipamento; só para móveis.</param>
/// <param name="Attributes">Valores já tipados (texto, <see cref="long"/>, <see cref="decimal"/>, <see cref="bool"/>, lista de texto).</param>
/// <param name="RegisteredBeforeAnchor">Quanto antes da âncora o equipamento foi cadastrado.</param>
public sealed record EquipmentSeed(
    string TypeKey,
    string SerialNumber,
    string Brand,
    string Model,
    string OrganizationAcronym,
    LocationSeed? Location,
    string? BaseName,
    IReadOnlyDictionary<string, object?> Attributes,
    TimeSpan RegisteredBeforeAnchor,
    EquipmentStatus InitialStatus,
    PeriodicMaintenanceSeed? PeriodicMaintenance,
    StatusChangeSeed? StatusChange);

/// <summary>
/// Manutenção periódica aberta no cadastro (RN-08), com a data prevista em dias a partir da data da âncora no fuso do
/// município: negativa para as vencidas (RN-10).
/// </summary>
public sealed record PeriodicMaintenanceSeed(int DueInDays, int PeriodicityDays, string Description);

/// <summary>Mudança de situação feita antes da âncora (RN-05), com a justificativa quando a RN-06 a exige.</summary>
public sealed record StatusChangeSeed(EquipmentStatus Target, TimeSpan BeforeAnchor, string? Justification);

/// <summary>Resultado de uma versão da carga: aplicada agora, com as quantidades, ou já aplicada antes.</summary>
public sealed record SeedResult(string Version, bool AlreadyApplied, IReadOnlyDictionary<string, int> Counts)
{
    public static SeedResult Skipped(string version) => new(version, true, new Dictionary<string, int>());
}
