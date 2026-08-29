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
    FrenchPress = 6,
    Turkish = 7,
    V60 = 8,
    Siphon = 9,
    Cupping = 10,
    ColdBrew = 11,
    ColdDrip = 12,
    SteepAndRelease = 13,
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
        BrewMethod.Turkish => "Turkish",
        BrewMethod.V60 => "V60",
        BrewMethod.Siphon => "Siphon",
        BrewMethod.Cupping => "Cupping",
        BrewMethod.ColdBrew => "Cold Brew",
        BrewMethod.ColdDrip => "Cold Drip",
        BrewMethod.SteepAndRelease => "Steep & Release",
        _ => method.ToString()
    };

    /// <summary>
    /// Short 2–5 character label suitable for compact chip displays.
    /// </summary>
    public static string ShortName(this BrewMethod method) => method switch
    {
        BrewMethod.Espresso => "Esp",
        BrewMethod.PourOver => "Pour",
        BrewMethod.Moka => "Moka",
        BrewMethod.Drip => "Drip",
        BrewMethod.Aeropress => "Aero",
        BrewMethod.FrenchPress => "Press",
        BrewMethod.Turkish => "Trk",
        BrewMethod.V60 => "V60",
        BrewMethod.Siphon => "Siph",
        BrewMethod.Cupping => "Cup",
        BrewMethod.ColdBrew => "CldB",
        BrewMethod.ColdDrip => "CldD",
        BrewMethod.SteepAndRelease => "Steep",
        _ => method.ToString()
    };

    /// <summary>
    /// Recommended Dose, Yield, and Time ranges for the logging UI.
    /// Hard save limits are stored with these values in
    /// <see cref="BrewMethodValueRangeCatalog"/>.
    /// </summary>
    public static BrewMethodProfile Profile(this BrewMethod method)
        => BrewMethodValueRangeCatalog.GetProfile(method);

    /// <summary>
    /// Drink types that are reasonable for a given brew method. Espresso is the
    /// only method with a meaningful menu (milk drinks, lungo/ristretto, etc.);
    /// every other method is effectively a single drink (Pour Over, French Press,
    /// Cold Brew…) so the picker collapses to one option.
    /// The first entry is treated as the default when switching methods.
    /// </summary>
    public static IReadOnlyList<string> DrinkTypesFor(this BrewMethod method) => method switch
    {
        BrewMethod.Espresso       => new[] { "Espresso", "Ristretto", "Lungo", "Americano", "Macchiato", "Cortado", "Flat White", "Cappuccino", "Latte", "Mocha" },
        BrewMethod.V60            => new[] { "Pour Over" },
        BrewMethod.PourOver       => new[] { "Pour Over" },
        BrewMethod.Drip           => new[] { "Drip" },
        BrewMethod.Aeropress      => new[] { "Aeropress" },
        BrewMethod.FrenchPress    => new[] { "French Press" },
        BrewMethod.Moka           => new[] { "Moka" },
        BrewMethod.Turkish        => new[] { "Turkish" },
        BrewMethod.Siphon         => new[] { "Siphon" },
        BrewMethod.Cupping        => new[] { "Cupping" },
        BrewMethod.ColdBrew       => new[] { "Cold Brew" },
        BrewMethod.ColdDrip       => new[] { "Cold Drip" },
        BrewMethod.SteepAndRelease => new[] { "Steep & Release" },
        _ => new[] { method.DisplayName() }
    };

    /// <summary>
    /// All brew methods in canonical display order (fine → coarse, hot → cold).
    /// </summary>
    public static IReadOnlyList<BrewMethod> All { get; } = new[]
    {
        BrewMethod.Turkish,
        BrewMethod.Espresso,
        BrewMethod.Moka,
        BrewMethod.V60,
        BrewMethod.PourOver,
        BrewMethod.Aeropress,
        BrewMethod.Siphon,
        BrewMethod.Drip,
        BrewMethod.Cupping,
        BrewMethod.SteepAndRelease,
        BrewMethod.FrenchPress,
        BrewMethod.ColdBrew,
        BrewMethod.ColdDrip,
    };

    /// <summary>
    /// Grind size range (in microns) for the picker UI, including step granularity
    /// and a sensible default. Sourced from the Turin DF64V grind chart (Honest
    /// Coffee Guide), cross-referenced against published Coffee Locator / JayArr
    /// general-grind-size charts where they materially diverge.
    ///
    /// Known divergences from broader web consensus:
    /// - Moka top end (650µm) is looser than the typical 300–500µm guidance —
    ///   we honour the DF64V chart for grinder consistency.
    /// - Cupping range (450–850µm) is finer than the SCA standard 800–1100µm —
    ///   we honour the DF64V chart; revisit if cupping users complain.
    /// </summary>
    public static GrindMicronRangeSpec GrindMicronRange(this BrewMethod method)
        => BrewMethodValueRangeCatalog.GetGrindSpec(method);
}

/// <summary>
/// Grind-picker range in microns: Min/Max bounds, Step granularity, and a
/// Default starting value for fresh picker opens. See
/// <see cref="BrewMethodExtensions.GrindMicronRange"/>.
/// </summary>
public record GrindMicronRangeSpec(int Min, int Max, int Step, int Default);

/// <summary>
/// Adaptive UI ranges and defaults for a <see cref="BrewMethod"/>. Returned by
/// <see cref="BrewMethodExtensions.Profile"/> and consumed by the drink-logging
/// page so that sliders, gauges, and number fields re-scale to the selected
/// method (e.g. espresso dose 5–30g vs. drip dose 10–120g).
/// </summary>
public record BrewMethodProfile(
    BrewMethod Method,
    decimal DoseMin, decimal DoseMax, decimal DoseDefault, decimal DoseStep,
    decimal OutputMin, decimal OutputMax, decimal OutputDefault, decimal OutputStep,
    int TimeMin, int TimeMax, int TimeDefault, int TimeStep)
{
    /// <summary>Clamp a dose value into this method's allowed range.</summary>
    public decimal ClampDose(decimal value) => Math.Max(DoseMin, Math.Min(DoseMax, value));

    /// <summary>Clamp an output value into this method's allowed range.</summary>
    public decimal ClampOutput(decimal value) => Math.Max(OutputMin, Math.Min(OutputMax, value));

    /// <summary>Clamp a time-seconds value into this method's allowed range.</summary>
    public decimal ClampTime(decimal value)
        => Math.Max(TimeMin, Math.Min(TimeMax, value));
}
