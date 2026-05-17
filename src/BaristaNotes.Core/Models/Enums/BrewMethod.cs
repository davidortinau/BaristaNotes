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
    /// Short 2–3 character label suitable for compact chip displays.
    /// </summary>
    public static string ShortName(this BrewMethod method) => method switch
    {
        BrewMethod.Espresso => "Esp",
        BrewMethod.PourOver => "Pour",
        BrewMethod.Moka => "Moka",
        BrewMethod.Drip => "Drip",
        BrewMethod.Aeropress => "Aero",
        BrewMethod.FrenchPress => "Press",
        _ => method.ToString()
    };

    /// <summary>
    /// UI profile (sensible dose / output / time min-max-default per method)
    /// driving the adaptive gauges + sliders on the drink-logging page. These
    /// ranges are aligned with <c>ShotService.ValidateCreateShot</c> so the UI
    /// can never show a value the backend would reject, while still offering a
    /// sane default + step for each brew method.
    /// </summary>
    public static BrewMethodProfile Profile(this BrewMethod method) => method switch
    {
        // Espresso: 5–30g in, 10–100g out, 10–60s. Defaults are typical 18g → 36g / 28s.
        BrewMethod.Espresso => new BrewMethodProfile(
            Method: method,
            DoseMin: 5, DoseMax: 30, DoseDefault: 18, DoseStep: 0.1m,
            OutputMin: 10, OutputMax: 100, OutputDefault: 36, OutputStep: 0.5m,
            TimeMin: 10, TimeMax: 60, TimeDefault: 28, TimeStep: 1),
        // Pour Over: V60/Chemex range — 10–60g in, 100–800g out, 1–15 minutes.
        BrewMethod.PourOver => new BrewMethodProfile(
            Method: method,
            DoseMin: 10, DoseMax: 60, DoseDefault: 20, DoseStep: 0.5m,
            OutputMin: 100, OutputMax: 800, OutputDefault: 320, OutputStep: 5,
            TimeMin: 60, TimeMax: 900, TimeDefault: 210, TimeStep: 5),
        // Moka pot — small stovetop. 5–50g in, 20–400g out, 30s–10min.
        BrewMethod.Moka => new BrewMethodProfile(
            Method: method,
            DoseMin: 5, DoseMax: 50, DoseDefault: 18, DoseStep: 0.5m,
            OutputMin: 20, OutputMax: 400, OutputDefault: 80, OutputStep: 5,
            TimeMin: 30, TimeMax: 600, TimeDefault: 180, TimeStep: 5),
        // Drip / batch brewer — large dose + yield.
        BrewMethod.Drip => new BrewMethodProfile(
            Method: method,
            DoseMin: 10, DoseMax: 120, DoseDefault: 30, DoseStep: 1,
            OutputMin: 100, OutputMax: 1500, OutputDefault: 500, OutputStep: 10,
            TimeMin: 60, TimeMax: 1200, TimeDefault: 300, TimeStep: 10),
        // Aeropress — small-batch immersion. 5–40g in, 50–400g out, 30s–10min.
        BrewMethod.Aeropress => new BrewMethodProfile(
            Method: method,
            DoseMin: 5, DoseMax: 40, DoseDefault: 15, DoseStep: 0.5m,
            OutputMin: 50, OutputMax: 400, OutputDefault: 200, OutputStep: 5,
            TimeMin: 30, TimeMax: 600, TimeDefault: 90, TimeStep: 5),
        // French Press — steep time dominates. 10–100g in, 100–1200g out.
        BrewMethod.FrenchPress => new BrewMethodProfile(
            Method: method,
            DoseMin: 10, DoseMax: 100, DoseDefault: 30, DoseStep: 0.5m,
            OutputMin: 100, OutputMax: 1200, OutputDefault: 500, OutputStep: 10,
            TimeMin: 60, TimeMax: 900, TimeDefault: 240, TimeStep: 5),
        _ => new BrewMethodProfile(
            Method: method,
            DoseMin: 5, DoseMax: 30, DoseDefault: 18, DoseStep: 0.1m,
            OutputMin: 10, OutputMax: 100, OutputDefault: 36, OutputStep: 0.5m,
            TimeMin: 10, TimeMax: 60, TimeDefault: 28, TimeStep: 1)
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
