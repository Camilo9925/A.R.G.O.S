using Argos.Simulation.Geography;
using Argos.Simulation.Models;

namespace Argos.Simulation.Generators;

/// <summary>
/// Gera o catálogo inicial do Inventário (plan 15.11.2, 15.11.3; D-56): função pura de (semente, geografia de
/// Prometeia). Mesma semente, mesmo catálogo (Art. IX.3). Os equipamentos fixos caem sobre a geometria dos
/// logradouros, com o CEP e o bairro do trecho (Art. IX.4); marcas, modelos, números de série, placas e
/// permissionários são fictícios (Art. VIII.5).
/// </summary>
public static class InventoryCatalogGenerator
{
    public const string Smm = "SMM";
    public const string Gcm = "GCM";
    public const string Pm = "PM";
    public const string Rotapro = "ROTAPRO";
    public const string Agora = "ÁGORA";

    public const string CentralDeMobilidade = "Central de Mobilidade";
    public const string TerminalUrbano = "Terminal Urbano";
    public const string SedeGcm = "Sede";
    public const string InspetoriaNorte = "Inspetoria Norte";
    public const string InspetoriaSul = "Inspetoria Sul";
    public const string PrimeiraCompanhia = "1ª Companhia";
    public const string SegundaCompanhia = "2ª Companhia";
    public const string Garagem = "Garagem";
    public const string Patio = "Pátio";

    /// <summary>Distância máxima ao contorno do município para uma balança ficar "perto do contorno" (plan 15.11.3).</summary>
    public const double ScaleMaxDistanceToBoundaryMeters = 800;

    /// <summary>Raio em torno do centro de bairro que define um "centro de bairro comercial" para os totens.</summary>
    public const double NeighborhoodCenterRadiusMeters = 400;

    public const string Centro = "Centro";

    public static SimulatedInventoryCatalog Generate(int seed, PrometeiaGeography geography)
    {
        ArgumentNullException.ThrowIfNull(geography);
        return new Run(seed, geography).Generate();
    }

    /// <summary>Uma geração: o estado mutável (sorteio e identificadores já usados) fica só aqui dentro.</summary>
    private sealed class Run(int seed, PrometeiaGeography geography)
    {
        private const int CatalogSize = 300;
        private const double OverdueShare = 0.05;
        private const double UpcomingShare = 0.08;

        private static readonly (SimulatedEquipmentStatus Status, int Count)[] StatusCounts =
        [
            (SimulatedEquipmentStatus.Deactivated, 9),
            (SimulatedEquipmentStatus.Stolen, 3),
            (SimulatedEquipmentStatus.NotInstalled, 12),
            (SimulatedEquipmentStatus.Malfunction, 15),
            (SimulatedEquipmentStatus.UnderMaintenance, 9),
        ];

        private readonly Random _random = new(seed);
        private readonly HashSet<string> _serials = [];
        private readonly HashSet<string> _plates = [];
        private readonly List<PrometeiaStreet> _mainStreets =
            geography.Streets.Where(street => street.Class != StreetClass.Local).ToList();

        private readonly List<Draft> _drafts = [];

        public SimulatedInventoryCatalog Generate()
        {
            var organizations = Organizations();
            var bases = Bases();

            AddRadars();
            AddCameras();
            AddScales();
            AddGates();
            AddKiosks();
            AddMobile();

            if (_drafts.Count != CatalogSize)
            {
                throw new InvalidOperationException($"O catálogo deveria ter {CatalogSize} equipamentos.");
            }

            AssignStatuses();
            return new SimulatedInventoryCatalog(organizations, bases, _drafts.Select(Build).ToArray());
        }

        private static SimulatedOrganization[] Organizations() =>
        [
            new(Smm, "Secretaria Municipal de Mobilidade"),
            new(Gcm, "Guarda Civil Municipal"),
            new(Pm, "Polícia Militar"),
            new(Rotapro, "Rotativa Prometeia S.A."),
            new(Agora, "Ágora Micromobilidade"),
        ];

        private SimulatedBase[] Bases()
        {
            var otherRegions = geography.Regions.Select(region => region.Name).Where(name => name != Centro).ToArray();

            return
            [
                new(CentralDeMobilidade, Smm, LocationIn(Centro)),
                new(TerminalUrbano, Smm, LocationIn(Centro)),
                new(SedeGcm, Gcm, LocationIn(Centro)),
                new(InspetoriaNorte, Gcm, LocationIn("Zona Norte")),
                new(InspetoriaSul, Gcm, LocationIn("Zona Sul")),
                new(PrimeiraCompanhia, Pm, LocationIn("Zona Leste")),
                new(SegundaCompanhia, Pm, LocationIn("Zona Oeste")),
                new(Garagem, Rotapro, LocationIn(Pick(otherRegions))),
                new(Patio, Agora, LocationIn(Pick(otherRegions))),
            ];
        }

        // Radares: vias principais de todas as zonas, revezando as zonas; ~70% na modalidade velocidade.
        private void AddRadars()
        {
            const int Count = 36;
            var regions = geography.Regions.Select(region => region.Name).ToArray();
            var speed = Indexes(Count, (int)Math.Round(Count * 0.7));

            for (var i = 0; i < Count; i++)
            {
                var street = Pick(StreetsIn(regions[i % regions.Length], mainOnly: true));
                var isMajor = street.Class == StreetClass.Major;
                var attributes = new Dictionary<string, object>
                {
                    ["modality"] = speed.Contains(i)
                        ? "speed"
                        : Pick(["red-light", "forbidden-turn", "forbidden-u-turn", "crosswalk-stop", "other"]),
                    ["monitoredLanes"] = (long)(isMajor ? _random.Next(2, 5) : _random.Next(1, 3)),
                };

                if (speed.Contains(i))
                {
                    attributes["maxSpeedKmh"] = (long)(isMajor ? Pick([60, 70, 80]) : Pick([40, 50, 60]));
                }

                if (_random.NextDouble() < 0.5)
                {
                    attributes["roadDirection"] = Pick(["bairro → centro", "centro → bairro", "sentido único"]);
                }

                var (brand, model) = Pick([("Vigilis", "VX-300"), ("Vigilis", "VX-500"), ("Tecnovia", "TR-80")]);
                AddFixed("traffic-radar", brand, model, Smm, LocationOn(street), attributes, 365, "Aferição anual do radar");
            }
        }

        // Câmeras: ~40% no Centro e o restante em vias principais das demais zonas; 60 da GCM e 30 da SMM.
        private void AddCameras()
        {
            const int Count = 90;
            var gcm = Indexes(Count, 60);
            var centro = StreetsIn(Centro, mainOnly: false);
            var outside = _mainStreets.Where(street => geography.RegionOf(street) != Centro).ToList();

            for (var i = 0; i < Count; i++)
            {
                var street = i < 36 ? Pick(centro) : Pick(outside);
                var ptz = _random.NextDouble() < 0.3;
                var attributes = new Dictionary<string, object>
                {
                    ["resolution"] = Pick(["hd-720p", "full-hd-1080p", "full-hd-1080p", "full-hd-1080p", "qhd-2k", "uhd-4k"]),
                    ["nightVision"] = _random.NextDouble() < 0.7,
                    ["colorMode"] = _random.NextDouble() < 0.85 ? "color" : "black-and-white",
                    ["panTiltZoom"] = ptz,
                };

                if (_random.NextDouble() < 0.6)
                {
                    attributes["fieldOfViewDegrees"] = ptz ? 360L : (long)Pick([60, 75, 90, 110, 120]);
                }

                var (brand, model) = Pick([("Orbis", "OC-1080"), ("Orbis", "OC-4K"), ("Panóptica", "PX-20")]);
                AddFixed(
                    "surveillance-camera", brand, model, gcm.Contains(i) ? Gcm : Smm, LocationOn(street), attributes,
                    180, "Revisão preventiva semestral da câmera");
            }
        }

        // Balanças: em vias major ou secondary perto do contorno (entradas da cidade), uma por logradouro.
        private void AddScales()
        {
            const int Count = 6;
            var candidates = Shuffle(_mainStreets
                    .SelectMany(street => street.Geometry.SelectMany(trecho => trecho.Select(point => (Street: street, Point: point))))
                    .Where(candidate =>
                        GeoMath.Contains(geography.Municipality.Boundary, candidate.Point)
                        && GeoMath.DistanceToRingMeters(geography.Municipality.Boundary, candidate.Point)
                            <= ScaleMaxDistanceToBoundaryMeters)
                    .ToList())
                .DistinctBy(candidate => candidate.Street.Name)
                .Take(Count)
                .ToArray();

            if (candidates.Length < Count)
            {
                throw new InvalidOperationException("Não há vias principais suficientes perto do contorno para as balanças.");
            }

            foreach (var (street, point) in candidates)
            {
                var attributes = new Dictionary<string, object>
                {
                    ["maxCapacityTonnes"] = 30m + (0.5m * _random.Next(0, 101)),
                    ["weighingMode"] = Pick(["static", "in-motion"]),
                };

                AddFixed(
                    "truck-scale", "Pondera", Pick(["PD-60", "PD-80"]), Smm, LocationAt(street, point), attributes,
                    365, "Aferição anual da balança");
            }
        }

        // Cancelas: metade no Centro e metade nos acessos de equipamentos públicos das demais zonas; 12 SMM e 12 ROTAPRO.
        private void AddGates()
        {
            const int Count = 24;
            var centro = StreetsIn(Centro, mainOnly: false);
            var outside = geography.Streets.Where(street => geography.RegionOf(street) != Centro).ToList();
            string[] places =
            [
                "Estacionamento do Paço Municipal",
                "Garagem do Terminal Urbano",
                "Estacionamento do Mercado Municipal",
                "Estacionamento do Ginásio de Esportes",
                "Estacionamento da Biblioteca Municipal",
                "Estacionamento do Centro de Saúde",
                "Estacionamento da Rodoviária",
                "Pátio da Garagem Municipal",
                "Estacionamento do Teatro Municipal",
                "Estacionamento do Parque Ecológico",
            ];

            for (var i = 0; i < Count; i++)
            {
                var attributes = new Dictionary<string, object>
                {
                    ["controlledPlace"] = Pick(places),
                    ["direction"] = Pick(["entry", "exit", "both", "both"]),
                    ["activationMethod"] = Pick(["ticket", "tag", "plate-reading", "remote-control", "manual"]),
                };

                var (brand, model) = Pick([("Portalis", "PC-200"), ("Tecnovia", "TG-10")]);
                AddFixed(
                    "automated-gate", brand, model, i % 2 == 0 ? Smm : Rotapro,
                    LocationOn(Pick(i < Count / 2 ? centro : outside)), attributes, 180, "Revisão preventiva da cancela");
            }
        }

        // Totens: ~60% no Centro e o restante perto dos centros de bairro (zona azul), todos da ROTAPRO.
        private void AddKiosks()
        {
            const int Count = 44;
            var centro = StreetsIn(Centro, mainOnly: false);
            var neighborhoodCenters = geography.Streets
                .Where(street => geography.RegionOf(street) != Centro
                    && GeoMath.DistanceMeters(street.ReferencePoint, geography.Neighborhood(street.Neighborhood).Center)
                        <= NeighborhoodCenterRadiusMeters)
                .ToList();

            for (var i = 0; i < Count; i++)
            {
                var street = i < 26 || neighborhoodCenters.Count == 0 ? Pick(centro) : Pick(neighborhoodCenters);
                var methods = Pick<string[]>(
                [
                    ["debit", "credit", "pix"],
                    ["cash", "debit", "credit", "pix"],
                    ["credit", "pix"],
                    ["cash", "pix"],
                ]);
                var attributes = new Dictionary<string, object>
                {
                    ["paymentMethods"] = methods,
                    ["issuesReceipt"] = _random.NextDouble() < 0.8,
                };

                if (_random.NextDouble() < 0.7)
                {
                    attributes["coverageArea"] = $"Zona azul — {street.Neighborhood}";
                }

                var (brand, model) = Pick([("Argentum", "AT-5"), ("Numisa", "NK-2")]);
                AddFixed("parking-kiosk", brand, model, Rotapro, LocationOn(street), attributes, 90, "Revisão trimestral do totem");
            }
        }

        private void AddMobile()
        {
            for (var i = 1; i <= 4; i++)
            {
                AddMobile(
                    "parking-enforcement-vehicle", "Autovia", "AF-1", Rotapro, Garagem,
                    new Dictionary<string, object>
                    {
                        ["operationArea"] = i <= 2 ? "Zona azul do Centro" : "Zona azul dos centros de bairro",
                        ["autonomyHours"] = 6m + (0.5m * _random.Next(0, 13)),
                        ["identification"] = $"AV-{i:00}",
                    },
                    90, "Revisão trimestral do veículo autônomo");
            }

            foreach (var (modality, count, capacity) in new[] { ("taxi", 18, 4), ("motorcycle-taxi", 10, 1), ("school", 8, 0) })
            {
                for (var i = 0; i < count; i++)
                {
                    var (brand, model) = modality switch
                    {
                        "taxi" => ("Rodaris", "Sedã R4"),
                        "motorcycle-taxi" => ("Motéris", "MT 160"),
                        _ => ("Rodaris", "Escolar 20"),
                    };

                    AddMobile(
                        "passenger-transport-vehicle", brand, model, Smm, TerminalUrbano,
                        new Dictionary<string, object>
                        {
                            ["modality"] = modality,
                            ["licensePlate"] = Plate(),
                            ["passengerCapacity"] = (long)(capacity > 0 ? capacity : _random.Next(12, 21)),
                            ["permitHolder"] = PersonName(),
                        },
                        null, null);
                }
            }

            for (var i = 0; i < 30; i++)
            {
                var shared = i < 24;
                var attributes = new Dictionary<string, object>
                {
                    ["modality"] = shared ? "shared" : "public",
                    ["operatorCompany"] = shared ? "Ágora Micromobilidade" : "Secretaria Municipal de Mobilidade",
                };

                if (_random.NextDouble() < 0.8)
                {
                    attributes["batteryLevelPercent"] = (long)_random.Next(20, 101);
                }

                AddMobile(
                    "electric-scooter", "Voltis", "V2", shared ? Agora : Smm, shared ? Patio : CentralDeMobilidade,
                    attributes, 90, "Revisão trimestral do patinete");
            }

            for (var i = 1; i <= 12; i++)
            {
                AddMobile(
                    "traffic-agent-motorcycle", "Motéris", "MT 300 Trânsito", Smm, CentralDeMobilidade,
                    new Dictionary<string, object> { ["licensePlate"] = Plate(), ["prefix"] = $"SMM-M{i:00}" },
                    180, "Revisão semestral da moto");
            }

            string[] gcmBases = [SedeGcm, InspetoriaNorte, InspetoriaSul];
            string[] pmBases = [PrimeiraCompanhia, SegundaCompanhia];

            for (var i = 0; i < 18; i++)
            {
                var isGcm = i < 10;
                var number = isGcm ? i + 1 : i - 9;
                var kind = Pick(["patrol-car", "patrol-car", "patrol-car", "pickup", "van", "motorcycle"]);
                var model = kind switch
                {
                    "pickup" => "Picape 4x4",
                    "van" => "Van Base",
                    "motorcycle" => "Moto Patrulha 300",
                    _ => "Patrulha 2.0",
                };

                AddMobile(
                    "patrol-vehicle", kind == "motorcycle" ? "Motéris" : "Rodaris", model, isGcm ? Gcm : Pm,
                    isGcm ? gcmBases[i % gcmBases.Length] : pmBases[i % pmBases.Length],
                    new Dictionary<string, object>
                    {
                        ["licensePlate"] = Plate(),
                        ["prefix"] = $"{(isGcm ? Gcm : Pm)}-V{number:00}",
                        ["vehicleKind"] = kind,
                    },
                    180, "Revisão semestral da viatura");
            }
        }

        private void AddFixed(
            string typeKey,
            string brand,
            string model,
            string organization,
            SimulatedLocation location,
            Dictionary<string, object> attributes,
            int periodicityDays,
            string periodicDescription) =>
            _drafts.Add(new Draft(
                typeKey, brand, model, Serial(brand), organization, location, null, attributes, IsFixed: true,
                IsCrew: false, periodicityDays, periodicDescription));

        private void AddMobile(
            string typeKey,
            string brand,
            string model,
            string organization,
            string baseName,
            Dictionary<string, object> attributes,
            int? periodicityDays,
            string? periodicDescription) =>
            _drafts.Add(new Draft(
                typeKey, brand, model, Serial(brand), organization, null, baseName, attributes, IsFixed: false,
                IsCrew: typeKey is "traffic-agent-motorcycle" or "patrol-vehicle", periodicityDays, periodicDescription));

        /// <summary>
        /// Distribui as situações finais com as contagens de plan 15.11.3 (~84% Operacional, ~5% Mau funcionamento,
        /// ~3% Em manutenção, ~4% Ainda não instalado, ~3% Desativado e ~1% Furtado). Motos e viaturas nunca ficam
        /// Desativadas nem Furtadas (D-62), e só fixos ficam Ainda não instalados.
        /// </summary>
        private void AssignStatuses()
        {
            var order = Shuffle(Enumerable.Range(0, _drafts.Count).ToList());

            foreach (var (status, count) in StatusCounts)
            {
                var chosen = order
                    .Where(index => _drafts[index].FinalStatus is null && IsEligible(_drafts[index], status))
                    .Take(count)
                    .ToArray();

                foreach (var index in chosen)
                {
                    _drafts[index].FinalStatus = status;
                }
            }

            foreach (var draft in _drafts.Where(draft => draft.FinalStatus is null))
            {
                draft.FinalStatus = SimulatedEquipmentStatus.Operational;
            }

            // Próximas datas das periódicas dos operacionais: ~5% vencidas e ~8% próximas (RN-10).
            var operationalPeriodic = Shuffle(_drafts
                .Where(draft => draft is { FinalStatus: SimulatedEquipmentStatus.Operational, PeriodicityDays: not null })
                .ToList());
            var overdue = (int)Math.Round(operationalPeriodic.Count * OverdueShare);
            var upcoming = (int)Math.Round(operationalPeriodic.Count * UpcomingShare);

            for (var i = 0; i < operationalPeriodic.Count; i++)
            {
                operationalPeriodic[i].Due = i < overdue ? DueKind.Overdue : i < overdue + upcoming ? DueKind.Upcoming : DueKind.Scheduled;
            }
        }

        private static bool IsEligible(Draft draft, SimulatedEquipmentStatus status) => status switch
        {
            SimulatedEquipmentStatus.Deactivated or SimulatedEquipmentStatus.Stolen => !draft.IsCrew,
            SimulatedEquipmentStatus.NotInstalled => draft.IsFixed,
            _ => true,
        };

        private SimulatedEquipment Build(Draft draft)
        {
            var final = draft.FinalStatus!.Value;
            var registeredBefore = TimeSpan.FromDays(180) + TimeSpan.FromMinutes(_random.Next(-20 * 24 * 60, 20 * 24 * 60));
            var initial = final == SimulatedEquipmentStatus.NotInstalled
                ? SimulatedEquipmentStatus.NotInstalled
                : SimulatedEquipmentStatus.Operational;

            SimulatedPeriodicMaintenance? periodic = null;
            if (draft.PeriodicityDays is { } periodicity && final != SimulatedEquipmentStatus.NotInstalled)
            {
                var dueInDays = draft.Due switch
                {
                    DueKind.Overdue => -_random.Next(1, 31),
                    DueKind.Upcoming => _random.Next(0, 8),
                    _ => _random.Next(8, periodicity + 1),
                };
                periodic = new SimulatedPeriodicMaintenance(dueInDays, periodicity, draft.PeriodicDescription!);
            }

            SimulatedStatusChange? change = null;
            if (final != initial)
            {
                var before = TimeSpan.FromMinutes(_random.Next(60, 10 * 24 * 60));
                var justification = final switch
                {
                    SimulatedEquipmentStatus.Deactivated => Pick(
                    [
                        "Equipamento substituído por um modelo novo.",
                        "Fim do contrato de manutenção.",
                        "Dano sem reparo economicamente viável.",
                    ]),
                    SimulatedEquipmentStatus.Stolen => Pick(
                    [
                        "Furto registrado em boletim de ocorrência.",
                        "Equipamento levado durante a madrugada.",
                    ]),
                    _ => null,
                };
                change = new SimulatedStatusChange(final, before, justification);
            }

            return new SimulatedEquipment(
                draft.TypeKey,
                draft.SerialNumber,
                draft.Brand,
                draft.Model,
                draft.Organization,
                draft.Location,
                draft.BaseName,
                draft.Attributes,
                registeredBefore,
                initial,
                periodic,
                change);
        }

        private List<PrometeiaStreet> StreetsIn(string region, bool mainOnly)
        {
            var streets = (mainOnly ? _mainStreets : geography.Streets)
                .Where(street => geography.RegionOf(street) == region)
                .ToList();

            // Uma zona sem via principal usa as demais vias dela.
            return streets.Count > 0 || !mainOnly ? streets : StreetsIn(region, mainOnly: false);
        }

        private SimulatedLocation LocationIn(string region) => LocationOn(Pick(StreetsIn(region, mainOnly: false)));

        /// <summary>Ponto sobre um trecho do logradouro, dentro do município (RN-02); sem sucesso, o ponto de referência.</summary>
        private SimulatedLocation LocationOn(PrometeiaStreet street)
        {
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var point = GeoMath.PointAlong(Pick(street.Geometry), 0.1 + (0.8 * _random.NextDouble()));
                if (GeoMath.Contains(geography.Municipality.Boundary, point))
                {
                    return LocationAt(street, point);
                }
            }

            return LocationAt(street, street.ReferencePoint);
        }

        private SimulatedLocation LocationAt(PrometeiaStreet street, GeoCoordinate point) =>
            new(
                street.ZipCode,
                street.Name,
                street.Neighborhood,
                Reference(),
                Math.Round(point.Latitude, 6),
                Math.Round(point.Longitude, 6));

        /// <summary>Referência no formato da RN-03: "número 181", "oposto ao número 181" ou "S/N".</summary>
        private string Reference()
        {
            var roll = _random.NextDouble();
            var number = _random.Next(10, 3000);
            return roll < 0.7 ? $"número {number}" : roll < 0.9 ? $"oposto ao número {number}" : "S/N";
        }

        /// <summary>Número de série no formato da marca, único por marca (RN-01).</summary>
        private string Serial(string brand)
        {
            while (true)
            {
                var serial = brand switch
                {
                    "Vigilis" => $"VG-{_random.Next(2019, 2026)}-{_random.Next(0, 100000):00000}",
                    "Tecnovia" => $"TV{_random.Next(10000000, 100000000)}",
                    "Orbis" => $"OB-{Alphanumeric(6)}",
                    "Panóptica" => $"PN{_random.Next(1000000, 10000000)}",
                    "Pondera" => $"PD-{_random.Next(1000, 10000)}-{_random.Next(1000, 10000)}",
                    "Portalis" => $"PT{_random.Next(100000, 1000000)}",
                    "Argentum" => $"AR-{_random.Next(100000, 1000000)}",
                    "Numisa" => $"NM{_random.Next(10000000, 100000000)}",
                    "Autovia" => $"AU-{Alphanumeric(6)}",
                    _ => $"9{Alphanumeric(16)}",
                };

                if (_serials.Add($"{brand}|{serial}"))
                {
                    return serial;
                }
            }
        }

        /// <summary>Placa no padrão Mercosul (LLLNLNN), única no catálogo.</summary>
        private string Plate()
        {
            while (true)
            {
                var plate = $"{Letter()}{Letter()}{Letter()}{_random.Next(10)}{Letter()}{_random.Next(10)}{_random.Next(10)}";
                if (_plates.Add(plate))
                {
                    return plate;
                }
            }
        }

        /// <summary>Nome fictício de permissionário, de listas de prenomes e sobrenomes comuns (Art. VIII.5).</summary>
        private string PersonName()
        {
            string[] first =
            [
                "Ana", "Bruno", "Carla", "Daniel", "Eduarda", "Felipe", "Gabriela", "Henrique", "Isabela", "João",
                "Larissa", "Marcos", "Natália", "Otávio", "Patrícia", "Rafael", "Sílvia", "Thiago", "Vanessa", "Wagner",
            ];
            string[] last =
            [
                "Almeida", "Barbosa", "Cardoso", "Dias", "Esteves", "Ferreira", "Gomes", "Lima", "Martins", "Nogueira",
                "Oliveira", "Pereira", "Queiroz", "Ribeiro", "Santos", "Teixeira", "Vieira", "Xavier",
            ];

            return $"{Pick(first)} {Pick(last)} {Pick(last)}";
        }

        private char Letter() => (char)('A' + _random.Next(26));

        private string Alphanumeric(int length)
        {
            const string Chars = "ABCDEFGHJKLMNPRSTUVWXYZ0123456789";
            return new string(Enumerable.Range(0, length).Select(_ => Chars[_random.Next(Chars.Length)]).ToArray());
        }

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

        /// <summary><paramref name="count"/> posições distintas sorteadas entre 0 e <paramref name="total"/> − 1.</summary>
        private HashSet<int> Indexes(int total, int count) =>
            Shuffle(Enumerable.Range(0, total).ToList()).Take(count).ToHashSet();

        private enum DueKind
        {
            Scheduled,
            Upcoming,
            Overdue,
        }

        private sealed record Draft(
            string TypeKey,
            string Brand,
            string Model,
            string SerialNumber,
            string Organization,
            SimulatedLocation? Location,
            string? BaseName,
            Dictionary<string, object> Attributes,
            bool IsFixed,
            bool IsCrew,
            int? PeriodicityDays,
            string? PeriodicDescription)
        {
            public SimulatedEquipmentStatus? FinalStatus { get; set; }

            public DueKind Due { get; set; }
        }
    }
}
