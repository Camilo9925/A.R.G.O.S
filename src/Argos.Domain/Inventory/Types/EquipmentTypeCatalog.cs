namespace Argos.Domain.Inventory.Types;

/// <summary>
/// Os dez tipos do catálogo inicial (spec 10; plan 15.5), definidos em código e criados pela carga (D-56). As chaves
/// são estáveis e em inglês (Art. XIII.1); rótulos e opções em pt-BR. Cada tipo passa pela validação de definição de
/// <see cref="EquipmentType.CreateBuiltIn"/>.
/// </summary>
public static class EquipmentTypeCatalog
{
    public const string TrafficRadar = "traffic-radar";
    public const string SurveillanceCamera = "surveillance-camera";
    public const string TruckScale = "truck-scale";
    public const string AutomatedGate = "automated-gate";
    public const string ParkingKiosk = "parking-kiosk";
    public const string ParkingEnforcementVehicle = "parking-enforcement-vehicle";
    public const string PassengerTransportVehicle = "passenger-transport-vehicle";
    public const string ElectricScooter = "electric-scooter";
    public const string TrafficAgentMotorcycle = "traffic-agent-motorcycle";
    public const string PatrolVehicle = "patrol-vehicle";

    /// <summary>Chave da modalidade de velocidade do radar, que torna a velocidade máxima obrigatória (US-02).</summary>
    public const string SpeedModality = "speed";

    /// <summary>Cria os dez tipos, na ordem de plan 15.5. Cada chamada devolve instâncias novas.</summary>
    public static IReadOnlyList<EquipmentType> CreateAll() =>
    [
        Radar(),
        Camera(),
        Scale(),
        Gate(),
        Kiosk(),
        EnforcementVehicle(),
        TransportVehicle(),
        Scooter(),
        Motorcycle(),
        Patrol(),
    ];

    private static EquipmentType Radar() => EquipmentType.CreateBuiltIn(
        TrafficRadar,
        "Radar de fiscalização",
        EquipmentCategory.Fixed,
        EquipmentIcons.Radar,
        crewAssignable: false,
        [
            Option(1, "modality", "Modalidade", required: true,
                (SpeedModality, "Velocidade"),
                ("red-light", "Avanço de semáforo"),
                ("forbidden-turn", "Conversão proibida"),
                ("forbidden-u-turn", "Retorno proibido"),
                ("crosswalk-stop", "Parada sobre faixa"),
                ("other", "Outros")),
            EquipmentTypeAttribute.Create(
                "maxSpeedKmh", "Velocidade máxima", AttributeDataType.Integer, 2,
                requiredWhen: new AttributeCondition("modality", SpeedModality), unit: "km/h", min: 10, max: 150),
            Integer(3, "monitoredLanes", "Faixas monitoradas", required: true, 1, 8),
            Text(4, "roadDirection", "Sentido da via", required: false),
        ]);

    private static EquipmentType Camera() => EquipmentType.CreateBuiltIn(
        SurveillanceCamera,
        "Câmera de videomonitoramento",
        EquipmentCategory.Fixed,
        EquipmentIcons.Camera,
        crewAssignable: false,
        [
            Option(1, "resolution", "Resolução", required: true,
                ("hd-720p", "HD 720p"),
                ("full-hd-1080p", "Full HD 1080p"),
                ("qhd-2k", "2K"),
                ("uhd-4k", "4K")),
            Boolean(2, "nightVision", "Visão noturna"),
            Option(3, "colorMode", "Modo de cor", required: true,
                ("color", "Colorida"),
                ("black-and-white", "Preto e branco")),
            Boolean(4, "panTiltZoom", "Giro e zoom (PTZ)"),
            Integer(5, "fieldOfViewDegrees", "Campo de visão", required: false, 1, 360, unit: "°"),
        ]);

    private static EquipmentType Scale() => EquipmentType.CreateBuiltIn(
        TruckScale,
        "Balança de caminhão",
        EquipmentCategory.Fixed,
        EquipmentIcons.Scale,
        crewAssignable: false,
        [
            EquipmentTypeAttribute.Create(
                "maxCapacityTonnes", "Capacidade máxima", AttributeDataType.Decimal, 1,
                isRequired: true, unit: "t", min: 1, max: 200),
            Option(2, "weighingMode", "Modo de pesagem", required: true,
                ("static", "Estática"),
                ("in-motion", "Em movimento")),
        ]);

    private static EquipmentType Gate() => EquipmentType.CreateBuiltIn(
        AutomatedGate,
        "Cancela automatizada",
        EquipmentCategory.Fixed,
        EquipmentIcons.Gate,
        crewAssignable: false,
        [
            Text(1, "controlledPlace", "Local controlado", required: true),
            Option(2, "direction", "Sentido", required: true,
                ("entry", "Entrada"),
                ("exit", "Saída"),
                ("both", "Ambos")),
            Option(3, "activationMethod", "Forma de acionamento", required: true,
                ("ticket", "Tíquete"),
                ("tag", "TAG"),
                ("plate-reading", "Leitura de placa"),
                ("remote-control", "Controle remoto"),
                ("manual", "Manual")),
        ]);

    private static EquipmentType Kiosk() => EquipmentType.CreateBuiltIn(
        ParkingKiosk,
        "Totem de autoatendimento",
        EquipmentCategory.Fixed,
        EquipmentIcons.Kiosk,
        crewAssignable: false,
        [
            EquipmentTypeAttribute.Create(
                "paymentMethods", "Formas de pagamento", AttributeDataType.MultiOption, 1, isRequired: true,
                options: Options(("cash", "Dinheiro"), ("debit", "Débito"), ("credit", "Crédito"), ("pix", "Pix"))),
            Boolean(2, "issuesReceipt", "Emite comprovante"),
            Text(3, "coverageArea", "Área de cobertura", required: false),
        ]);

    private static EquipmentType EnforcementVehicle() => EquipmentType.CreateBuiltIn(
        ParkingEnforcementVehicle,
        "Veículo autônomo de fiscalização de estacionamento",
        EquipmentCategory.Mobile,
        EquipmentIcons.AutonomousVehicle,
        crewAssignable: false,
        [
            Text(1, "operationArea", "Área de operação", required: true),
            EquipmentTypeAttribute.Create(
                "autonomyHours", "Autonomia", AttributeDataType.Decimal, 2,
                isRequired: true, unit: "h", min: 0.5m, max: 48),
            Text(3, "identification", "Identificação", required: true),
        ]);

    private static EquipmentType TransportVehicle() => EquipmentType.CreateBuiltIn(
        PassengerTransportVehicle,
        "Veículo de transporte",
        EquipmentCategory.Mobile,
        EquipmentIcons.Taxi,
        crewAssignable: false,
        [
            Option(1, "modality", "Modalidade", required: true,
                ("taxi", "Táxi"),
                ("motorcycle-taxi", "Mototáxi"),
                ("school", "Escolar")),
            Text(2, "licensePlate", "Placa", required: true, maxLength: 8),
            Integer(3, "passengerCapacity", "Capacidade de passageiros", required: true, 1, 60),
            Text(4, "permitHolder", "Permissionário", required: true),
        ]);

    private static EquipmentType Scooter() => EquipmentType.CreateBuiltIn(
        ElectricScooter,
        "Patinete elétrico",
        EquipmentCategory.Mobile,
        EquipmentIcons.Scooter,
        crewAssignable: false,
        [
            Option(1, "modality", "Modalidade", required: true,
                ("public", "Público"),
                ("shared", "Compartilhado")),
            Text(2, "operatorCompany", "Empresa operadora", required: true),
            Integer(3, "batteryLevelPercent", "Nível de bateria", required: false, 0, 100, unit: "%"),
        ]);

    private static EquipmentType Motorcycle() => EquipmentType.CreateBuiltIn(
        TrafficAgentMotorcycle,
        "Moto de agente de trânsito",
        EquipmentCategory.Mobile,
        EquipmentIcons.Motorcycle,
        crewAssignable: true,
        [
            Text(1, "licensePlate", "Placa", required: true, maxLength: 8),
            Text(2, "prefix", "Prefixo", required: true, maxLength: 20),
        ]);

    private static EquipmentType Patrol() => EquipmentType.CreateBuiltIn(
        PatrolVehicle,
        "Viatura",
        EquipmentCategory.Mobile,
        EquipmentIcons.PatrolCar,
        crewAssignable: true,
        [
            Text(1, "licensePlate", "Placa", required: true, maxLength: 8),
            Text(2, "prefix", "Prefixo", required: true, maxLength: 20),
            Option(3, "vehicleKind", "Tipo de viatura", required: true,
                ("patrol-car", "Carro de patrulha"),
                ("pickup", "Caminhonete"),
                ("van", "Van/base móvel"),
                ("motorcycle", "Motocicleta")),
        ]);

    private static EquipmentTypeAttribute Text(int order, string key, string label, bool required, int? maxLength = null) =>
        EquipmentTypeAttribute.Create(key, label, AttributeDataType.Text, order, isRequired: required, maxLength: maxLength);

    private static EquipmentTypeAttribute Integer(
        int order, string key, string label, bool required, int min, int max, string? unit = null) =>
        EquipmentTypeAttribute.Create(
            key, label, AttributeDataType.Integer, order, isRequired: required, unit: unit, min: min, max: max);

    private static EquipmentTypeAttribute Boolean(int order, string key, string label) =>
        EquipmentTypeAttribute.Create(key, label, AttributeDataType.Boolean, order, isRequired: true);

    private static EquipmentTypeAttribute Option(
        int order, string key, string label, bool required, params (string Key, string Label)[] options) =>
        EquipmentTypeAttribute.Create(
            key, label, AttributeDataType.Option, order, isRequired: required, options: Options(options));

    private static AttributeOption[] Options(params (string Key, string Label)[] options) =>
        options.Select(option => new AttributeOption(option.Key, option.Label)).ToArray();
}
