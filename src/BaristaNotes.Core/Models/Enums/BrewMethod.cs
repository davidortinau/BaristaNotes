namespace BaristaNotes.Core.Models.Enums;

/// <summary>
/// A brewing method for preparing coffee. Each <see cref="Recipe"/> and
/// (eventually) each logged drink is associated with exactly one brew method.
/// </summary>
public enum BrewMethod
{
    Espresso = 1,
    PourOver = 2,
    Moka = 3,
    Drip = 4,
    Aeropress = 5,
    FrenchPress = 6
}

/// <summary>
/// Helpers for <see cref="BrewMethod"/> display + equipment compatibility.
/// </summary>
public static class BrewMethodExtensions
{
    public static string DisplayName(this BrewMethod method) => method switch
    {
        BrewMethod.Espresso => "Espresso",
        BrewMethod.PourOver => "Pour Over",
        BrewMethod.Moka => "Moka",
        BrewMethod.Drip => "Drip",
        BrewMethod.Aeropress => "Aeropress",
        BrewMethod.FrenchPress => "French Press",
        _ => method.ToString()
    };

    /// <summary>
    /// All brew methods in canonical display order.
    /// </summary>
    public static IReadOnlyList<BrewMethod> All { get; } = new[]
    {
        BrewMethod.Espresso,
        BrewMethod.PourOver,
        BrewMethod.Moka,
        BrewMethod.Drip,
        BrewMethod.Aeropress,
        BrewMethod.FrenchPress
    };
}
