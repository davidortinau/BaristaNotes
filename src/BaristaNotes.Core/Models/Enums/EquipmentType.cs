namespace BaristaNotes.Core.Models.Enums;

public enum EquipmentType
{
    Machine = 1,
    Grinder = 2,
    Tamper = 3,
    PuckScreen = 4,
    PourOverDripper = 5,
    MokaPot = 6,
    DripMachine = 7,
    Aeropress = 8,
    FrenchPress = 9,
    Other = 99
}

public static class EquipmentTypeExtensions
{
    /// <summary>
    /// Returns the brew methods that this equipment type is compatible with,
    /// so the drink-logging flow can filter equipment by the chosen method.
    /// Grinder/Tamper/PuckScreen/Other are accessories and match all methods.
    /// </summary>
    public static IReadOnlyList<BrewMethod> CompatibleMethods(this EquipmentType type) => type switch
    {
        EquipmentType.Machine => new[] { BrewMethod.Espresso },
        EquipmentType.PourOverDripper => new[] { BrewMethod.PourOver, BrewMethod.V60 },
        EquipmentType.MokaPot => new[] { BrewMethod.Moka },
        EquipmentType.DripMachine => new[] { BrewMethod.Drip },
        EquipmentType.Aeropress => new[] { BrewMethod.Aeropress },
        EquipmentType.FrenchPress => new[] { BrewMethod.FrenchPress },
        // Accessories and Other apply to every method
        _ => BrewMethodExtensions.All
    };
}
