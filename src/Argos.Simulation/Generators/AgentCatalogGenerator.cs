using Argos.Simulation.Models;

namespace Argos.Simulation.Generators;

/// <summary>
/// Gera os agentes, os vínculos e as inativações de demonstração da Fase 3 (plan 15.11.3; D-56, D-62): função pura de
/// (semente, catálogo de equipamentos gerado pela mesma semente). Mesma semente, mesmo resultado (Art. IX.3). Nomes,
/// nomes de guerra, matrículas e postos são fictícios (Art. VIII.5) e só os dados mínimos da operação existem (RN-25).
/// <list type="bullet">
/// <item>80 agentes, todos Fora de serviço no cadastro: 30 agentes de trânsito (SMM), 28 guardas municipais (GCM) e
/// 22 policiais militares (PM).</item>
/// <item>48 vínculos: cada uma das 12 motos com 1 agente de trânsito e cada uma das 18 viaturas (10 da GCM e 8 da PM)
/// com 2 agentes do órgão, todos da base do equipamento.</item>
/// <item>Inativações (RN-35): 3 equipamentos Desativados ou Furtados, nunca moto nem viatura, e 2 agentes sem vínculo,
/// que passam a Afastado antes de inativados.</item>
/// </list>
/// </summary>
public static class AgentCatalogGenerator
{
    public const int TrafficAgents = 30;
    public const int MunicipalGuards = 28;
    public const int MilitaryPolice = 22;
    public const int AgentsPerPatrol = 2;
    public const int EquipmentInactivations = 3;
    public const int AgentInactivations = 2;

    public const string MotorcycleType = "traffic-agent-motorcycle";
    public const string PatrolType = "patrol-vehicle";

    public static SimulatedAgentCatalog Generate(int seed, SimulatedInventoryCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return new Run(seed, catalog).Generate();
    }

    /// <summary>Uma geração: o estado mutável (sorteio e identificadores já usados) fica só aqui dentro.</summary>
    private sealed class Run(int seed, SimulatedInventoryCatalog catalog)
    {
        private static readonly string[] FirstNames =
        [
            "Adriana", "Alexandre", "Beatriz", "Caio", "Camila", "Diego", "Elaine", "Fábio", "Fernanda", "Gustavo",
            "Helena", "Igor", "Juliana", "Leandro", "Letícia", "Lucas", "Mariana", "Mateus", "Nádia", "Otávio",
            "Paula", "Renato", "Roberta", "Sérgio", "Tatiane", "Vinícius", "Viviane", "Yara",
        ];

        private static readonly string[] Surnames =
        [
            "Albuquerque", "Andrade", "Azevedo", "Batista", "Brandão", "Campos", "Castro", "Correia", "Duarte", "Farias",
            "Fonseca", "Freitas", "Garcia", "Leite", "Lopes", "Macedo", "Medeiros", "Moraes", "Moreira", "Nunes",
            "Pacheco", "Peixoto", "Pinheiro", "Prado", "Rezende", "Rocha", "Sampaio", "Siqueira", "Tavares", "Vasconcelos",
        ];

        private static readonly string[] GuardRanks =
            ["Guarda de 3ª classe", "Guarda de 2ª classe", "Guarda de 1ª classe", "Guarda de classe distinta", "Subinspetor"];

        private static readonly string[] PoliceRanks =
            ["Soldado", "Soldado", "Cabo", "3º Sargento", "2º Sargento", "1º Sargento", "Subtenente"];

        private readonly Random _random = new(seed);
        private readonly HashSet<string> _fullNames = [];
        private readonly HashSet<(string, string)> _displayNames = [];
        private readonly HashSet<(string, string)> _registrations = [];
        private readonly List<SimulatedAgent> _agents = [];
        private readonly List<SimulatedAssignment> _assignments = [];
        private readonly List<SimulatedAgent> _unassigned = [];

        public SimulatedAgentCatalog Generate()
        {
            var motorcycles = catalog.Equipment.Where(equipment => equipment.TypeKey == MotorcycleType).ToArray();
            var patrols = catalog.Equipment.Where(equipment => equipment.TypeKey == PatrolType).ToArray();

            AddOrganization(
                InventoryCatalogGenerator.Smm,
                SimulatedAgentRole.TrafficAgent,
                TrafficAgents,
                motorcycles,
                agentsPerVehicle: 1,
                [InventoryCatalogGenerator.CentralDeMobilidade, InventoryCatalogGenerator.TerminalUrbano]);
            AddOrganization(
                InventoryCatalogGenerator.Gcm,
                SimulatedAgentRole.MunicipalGuard,
                MunicipalGuards,
                patrols.Where(patrol => patrol.OrganizationAcronym == InventoryCatalogGenerator.Gcm).ToArray(),
                AgentsPerPatrol,
                [InventoryCatalogGenerator.SedeGcm, InventoryCatalogGenerator.InspetoriaNorte, InventoryCatalogGenerator.InspetoriaSul]);
            AddOrganization(
                InventoryCatalogGenerator.Pm,
                SimulatedAgentRole.MilitaryPolice,
                MilitaryPolice,
                patrols.Where(patrol => patrol.OrganizationAcronym == InventoryCatalogGenerator.Pm).ToArray(),
                AgentsPerPatrol,
                [InventoryCatalogGenerator.PrimeiraCompanhia, InventoryCatalogGenerator.SegundaCompanhia]);

            return new SimulatedAgentCatalog(_agents, _assignments, InactivateEquipment(), InactivateAgents());
        }

        /// <summary>
        /// Os agentes de um órgão: primeiro os que tripulam cada veículo do órgão, na base dele; depois os demais,
        /// revezando as bases do órgão.
        /// </summary>
        private void AddOrganization(
            string acronym,
            SimulatedAgentRole role,
            int count,
            IReadOnlyList<SimulatedEquipment> vehicles,
            int agentsPerVehicle,
            string[] bases)
        {
            if (vehicles.Count * agentsPerVehicle > count)
            {
                throw new InvalidOperationException($"O órgão {acronym} não tem agentes suficientes para os seus veículos.");
            }

            foreach (var vehicle in vehicles)
            {
                for (var i = 0; i < agentsPerVehicle; i++)
                {
                    var agent = NewAgent(acronym, role, vehicle.BaseName!);
                    _assignments.Add(new SimulatedAssignment(
                        acronym,
                        agent.RegistrationNumber,
                        vehicle.Brand,
                        vehicle.SerialNumber,
                        TimeSpan.FromMinutes(_random.Next(20 * 24 * 60, 60 * 24 * 60))));
                }
            }

            for (var i = 0; _agents.Count(agent => agent.OrganizationAcronym == acronym) < count; i++)
            {
                _unassigned.Add(NewAgent(acronym, role, bases[i % bases.Length]));
            }
        }

        private SimulatedAgent NewAgent(string acronym, SimulatedAgentRole role, string baseName)
        {
            var (first, surname) = UniqueName();
            var displayName = _displayNames.Add((acronym, surname)) ? surname : $"{first} {surname}";
            _displayNames.Add((acronym, displayName));

            var agent = new SimulatedAgent(
                $"{first} {surname}",
                displayName,
                Registration(acronym),
                role,
                acronym,
                role switch
                {
                    SimulatedAgentRole.MunicipalGuard => Pick(GuardRanks),
                    SimulatedAgentRole.MilitaryPolice => Pick(PoliceRanks),
                    _ => null,
                },
                baseName,
                TimeSpan.FromDays(180) + TimeSpan.FromMinutes(_random.Next(-20 * 24 * 60, 20 * 24 * 60)));
            _agents.Add(agent);
            return agent;
        }

        /// <summary>Nome fictício com prenome e dois sobrenomes de listas comuns, sem repetição (Art. VIII.5).</summary>
        private (string First, string Surname) UniqueName()
        {
            while (true)
            {
                var first = Pick(FirstNames);
                var middle = Pick(Surnames);
                var surname = Pick(Surnames);
                if (middle != surname && _fullNames.Add($"{first} {middle} {surname}"))
                {
                    return ($"{first} {middle}", surname);
                }
            }
        }

        /// <summary>Matrícula fictícia no formato do órgão, única no órgão (US-06), com os caracteres da RF-014.</summary>
        private string Registration(string acronym)
        {
            while (true)
            {
                var registration = acronym switch
                {
                    InventoryCatalogGenerator.Smm => $"AT-{_random.Next(10000, 100000)}",
                    InventoryCatalogGenerator.Gcm => $"GCM-{_random.Next(1000, 10000)}",
                    _ => $"{_random.Next(100000, 1000000)}-{_random.Next(10)}",
                };

                if (_registrations.Add((acronym, registration)))
                {
                    return registration;
                }
            }
        }

        /// <summary>
        /// 3 equipamentos Desativados ou Furtados (RN-35), nunca moto nem viatura (D-62), inativados depois da mudança de
        /// situação.
        /// </summary>
        private SimulatedEquipmentInactivation[] InactivateEquipment()
        {
            var candidates = Shuffle(catalog.Equipment
                .Where(equipment => equipment.FinalStatus is SimulatedEquipmentStatus.Deactivated or SimulatedEquipmentStatus.Stolen
                    && equipment.TypeKey is not (MotorcycleType or PatrolType)
                    && equipment.StatusChange is not null)
                .ToList());

            if (candidates.Count < EquipmentInactivations)
            {
                throw new InvalidOperationException("O catálogo não tem equipamentos Desativados ou Furtados suficientes.");
            }

            return candidates
                .Take(EquipmentInactivations)
                .Select(equipment => new SimulatedEquipmentInactivation(
                    equipment.Brand,
                    equipment.SerialNumber,
                    equipment.FinalStatus == SimulatedEquipmentStatus.Stolen
                        ? "Furto sem recuperação do equipamento."
                        : Pick(["Equipamento retirado de operação.", "Baixa patrimonial após avaliação técnica."]),
                    TimeSpan.FromMinutes(_random.Next(1, (int)equipment.StatusChange!.BeforeAnchor.TotalMinutes))))
                .ToArray();
        }

        /// <summary>2 agentes sem vínculo, que passam a Afastado e depois são inativados (RN-12, RN-35).</summary>
        private SimulatedAgentInactivation[] InactivateAgents() =>
            Shuffle(_unassigned.ToList())
                .Take(AgentInactivations)
                .Select(agent => new SimulatedAgentInactivation(
                    agent.OrganizationAcronym,
                    agent.RegistrationNumber,
                    Pick(["Desligamento do órgão.", "Transferência para outro município."]),
                    TimeSpan.FromMinutes(_random.Next(5 * 24 * 60, 15 * 24 * 60)),
                    TimeSpan.FromMinutes(_random.Next(24 * 60, 4 * 24 * 60))))
                .ToArray();

        private T Pick<T>(IReadOnlyList<T> items) => items[_random.Next(items.Count)];

        /// <summary>Embaralhamento de Fisher–Yates com o sorteio da semente.</summary>
        private List<T> Shuffle<T>(List<T> items)
        {
            for (var i = items.Count - 1; i > 0; i--)
            {
                var j = _random.Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }

            return items;
        }
    }
}
