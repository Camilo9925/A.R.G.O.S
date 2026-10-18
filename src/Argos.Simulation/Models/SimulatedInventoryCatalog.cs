namespace Argos.Simulation.Models;

/// <summary>
/// Catálogo inicial gerado (plan 15.11.2, 15.11.3; D-56): órgãos, bases e equipamentos, com os fatos de negócio
/// relativos à data-âncora (D-03). A carga (Aplicação, pelo Migrator) aplica a âncora e cria tudo pelas operações do
/// Domínio, na ordem: cadastro, manutenção periódica e, por fim, a mudança de situação.
/// </summary>
public sealed record SimulatedInventoryCatalog(
    IReadOnlyList<SimulatedOrganization> Organizations,
    IReadOnlyList<SimulatedBase> Bases,
    IReadOnlyList<SimulatedEquipment> Equipment);

public sealed record SimulatedOrganization(string Acronym, string Name);

/// <summary>Localização sobre um logradouro de Prometeia, com o CEP e o bairro do trecho e a referência da RN-03.</summary>
public sealed record SimulatedLocation(
    string ZipCode, string Street, string Neighborhood, string Reference, double Latitude, double Longitude);

public sealed record SimulatedBase(string Name, string OrganizationAcronym, SimulatedLocation Location);

/// <summary>Situações do equipamento, com os mesmos nomes do enum do Domínio (RN-04).</summary>
public enum SimulatedEquipmentStatus
{
    NotInstalled,
    Operational,
    Malfunction,
    UnderMaintenance,
    Deactivated,
    Stolen,
}

/// <summary>
/// Um equipamento do catálogo. As características usam os tipos aceitos pelo Domínio: <see cref="string"/>,
/// <see cref="long"/>, <see cref="decimal"/>, <see cref="bool"/> e lista de <see cref="string"/> (Múltipla escolha).
/// </summary>
/// <param name="TypeKey">Chave do tipo no catálogo inicial (plan 15.5), como <c>traffic-radar</c>.</param>
/// <param name="Location">Só para fixos.</param>
/// <param name="BaseName">Nome da base do órgão; só para móveis.</param>
/// <param name="RegisteredBeforeAnchor">Quanto antes da âncora o equipamento foi cadastrado (cerca de 6 meses).</param>
/// <param name="PeriodicMaintenance">Manutenção periódica aberta no cadastro, quando o tipo tem.</param>
/// <param name="StatusChange">Mudança de situação nos últimos dias, quando a situação final não é a inicial.</param>
public sealed record SimulatedEquipment(
    string TypeKey,
    string SerialNumber,
    string Brand,
    string Model,
    string OrganizationAcronym,
    SimulatedLocation? Location,
    string? BaseName,
    IReadOnlyDictionary<string, object> Attributes,
    TimeSpan RegisteredBeforeAnchor,
    SimulatedEquipmentStatus InitialStatus,
    SimulatedPeriodicMaintenance? PeriodicMaintenance,
    SimulatedStatusChange? StatusChange)
{
    public SimulatedEquipmentStatus FinalStatus => StatusChange?.Target ?? InitialStatus;
}

/// <summary>
/// Manutenção periódica aberta no cadastro (RN-08), com a data prevista em dias a partir da data da âncora no fuso do
/// município: negativa para as vencidas, de 0 a 7 para as próximas (RN-10).
/// </summary>
public sealed record SimulatedPeriodicMaintenance(int DueInDays, int PeriodicityDays, string Description);

/// <summary>Mudança de situação feita nos últimos dias (RN-05), com a justificativa quando a RN-06 a exige.</summary>
public sealed record SimulatedStatusChange(SimulatedEquipmentStatus Target, TimeSpan BeforeAnchor, string? Justification);
