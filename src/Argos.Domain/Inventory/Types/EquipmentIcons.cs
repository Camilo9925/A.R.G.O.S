namespace Argos.Domain.Inventory.Types;

/// <summary>
/// Catálogo fixo de ícones dos tipos de equipamento (plan 15.5), espelho de <c>EquipmentIcons</c> de
/// <c>Argos.Contracts</c>, que o Domínio não referencia (Art. III.2). Os itens são os mesmos, com o mesmo nome e o
/// mesmo valor, garantido por teste unitário.
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
