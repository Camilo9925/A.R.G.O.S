namespace Argos.Contracts.Inventory;

/// <summary>
/// Catálogo fixo de ícones dos tipos de equipamento (plan 15.5): as chaves do catálogo inicial e as dos tipos
/// personalizados (US-10). A Web traduz cada chave para um ícone; a Camada 2 usará as mesmas chaves no mapa.
/// Os itens são exatamente os de <c>EquipmentIcons</c> do Domínio, garantido por teste unitário.
/// </summary>
public static class EquipmentIcons
{
    public const string Radar = "radar";
    public const string Camera = "camera";
    public const string Scale = "scale";
    public const string Gate = "gate";
    public const string Kiosk = "kiosk";
    public const string AutonomousVehicle = "autonomous-vehicle";
    public const string Taxi = "taxi";
    public const string Scooter = "scooter";
    public const string Motorcycle = "motorcycle";
    public const string PatrolCar = "patrol-car";
    public const string Sensor = "sensor";
    public const string Water = "water";
    public const string Light = "light";
    public const string Sign = "sign";
    public const string Generic = "generic";

    public static IReadOnlyList<string> All { get; } =
    [
        Radar,
        Camera,
        Scale,
        Gate,
        Kiosk,
        AutonomousVehicle,
        Taxi,
        Scooter,
        Motorcycle,
        PatrolCar,
        Sensor,
        Water,
        Light,
        Sign,
        Generic,
    ];
}
