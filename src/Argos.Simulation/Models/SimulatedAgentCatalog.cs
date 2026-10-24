namespace Argos.Simulation.Models;

/// <summary>
/// Agentes, vínculos e inativações de demonstração gerados (Fase 3; plan 15.11.3; D-56), com os instantes relativos à
/// data-âncora (D-03). A carga cria tudo pelas operações do Domínio: <c>inventory-agents-v1</c> (agentes e vínculos) e
/// <c>inventory-inactivations-v1</c> (inativações), nessa ordem.
/// </summary>
public sealed record SimulatedAgentCatalog(
    IReadOnlyList<SimulatedAgent> Agents,
    IReadOnlyList<SimulatedAssignment> Assignments,
    IReadOnlyList<SimulatedEquipmentInactivation> EquipmentInactivations,
    IReadOnlyList<SimulatedAgentInactivation> AgentInactivations);

/// <summary>Funções do agente, com os mesmos nomes do enum do Domínio (RF-014).</summary>
public enum SimulatedAgentRole
{
    TrafficAgent,
    MunicipalGuard,
    MilitaryPolice,
}

/// <summary>Um agente fictício (Art. VIII.5; RN-25: só os dados mínimos), sempre Fora de serviço no cadastro.</summary>
/// <param name="BaseName">Base de atuação, do órgão do agente.</param>
/// <param name="RegisteredBeforeAnchor">Quanto antes da âncora o agente foi cadastrado (cerca de 6 meses).</param>
public sealed record SimulatedAgent(
    string FullName,
    string DisplayName,
    string RegistrationNumber,
    SimulatedAgentRole Role,
    string OrganizationAcronym,
    string? Rank,
    string BaseName,
    TimeSpan RegisteredBeforeAnchor);

/// <summary>Vínculo de um agente com uma moto ou viatura do catálogo (US-07), pela marca e número de série (RN-01).</summary>
public sealed record SimulatedAssignment(
    string AgentOrganizationAcronym,
    string AgentRegistrationNumber,
    string EquipmentBrand,
    string EquipmentSerialNumber,
    TimeSpan StartedBeforeAnchor);

/// <summary>Inativação de um equipamento Desativado ou Furtado, nunca moto nem viatura (RN-35, D-62).</summary>
/// <param name="BeforeAnchor">Depois da mudança para Desativado ou Furtado.</param>
public sealed record SimulatedEquipmentInactivation(string Brand, string SerialNumber, string Reason, TimeSpan BeforeAnchor);

/// <summary>Agente sem vínculo que passa a Afastado e, depois, é inativado (RN-12, RN-35).</summary>
public sealed record SimulatedAgentInactivation(
    string OrganizationAcronym,
    string RegistrationNumber,
    string Reason,
    TimeSpan OnLeaveBeforeAnchor,
    TimeSpan InactivatedBeforeAnchor);
