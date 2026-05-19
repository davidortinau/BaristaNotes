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
        // V60: pour-over-specific subtype, similar shape to PourOver.
        BrewMethod.V60 => new BrewMethodProfile(
            Method: method,
            DoseMin: 10, DoseMax: 40, DoseDefault: 18, DoseStep: 0.5m,
            OutputMin: 100, OutputMax: 600, OutputDefault: 300, OutputStep: 5,
            TimeMin: 60, TimeMax: 600, TimeDefault: 180, TimeStep: 5),
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
        // Turkish (ibrik/cezve) — tiny dose, ~3-minute simmer.
        BrewMethod.Turkish => new BrewMethodProfile(
            Method: method,
            DoseMin: 5, DoseMax: 20, DoseDefault: 7, DoseStep: 0.5m,
            OutputMin: 25, OutputMax: 150, OutputDefault: 70, OutputStep: 5,
            TimeMin: 60, TimeMax: 300, TimeDefault: 180, TimeStep: 5),
        // Siphon — full immersion with vacuum draw, 1-3min brew.
        BrewMethod.Siphon => new BrewMethodProfile(
            Method: method,
            DoseMin: 15, DoseMax: 60, DoseDefault: 25, DoseStep: 0.5m,
            OutputMin: 200, OutputMax: 1000, OutputDefault: 400, OutputStep: 5,
            TimeMin: 60, TimeMax: 600, TimeDefault: 240, TimeStep: 5),
        // Cupping — SCA-style 8.25g per 150ml cup, 4-min steep.
        BrewMethod.Cupping => new BrewMethodProfile(
            Method: method,
            DoseMin: 8, DoseMax: 15, DoseDefault: 10, DoseStep: 0.1m,
            OutputMin: 150, OutputMax: 300, OutputDefault: 180, OutputStep: 5,
            TimeMin: 180, TimeMax: 600, TimeDefault: 240, TimeStep: 5),
        // Cold Brew — large batch, 12–24h steep (timing in seconds).
        BrewMethod.ColdBrew => new BrewMethodProfile(
            Method: method,
            DoseMin: 50, DoseMax: 300, DoseDefault: 100, DoseStep: 5,
            OutputMin: 500, OutputMax: 3000, OutputDefault: 1000, OutputStep: 10,
            TimeMin: 14400, TimeMax: 86400, TimeDefault: 43200, TimeStep: 1800),
        // Cold Drip — slow-drip ice tower, 4–12h drip.
        BrewMethod.ColdDrip => new BrewMethodProfile(
            Method: method,
            DoseMin: 30, DoseMax: 150, DoseDefault: 60, DoseStep: 1,
            OutputMin: 200, OutputMax: 1500, OutputDefault: 500, OutputStep: 10,
            TimeMin: 7200, TimeMax: 43200, TimeDefault: 21600, TimeStep: 600),
        // Steep & Release — immersion-dripper hybrid (Clever, Hario Switch, etc.).
        BrewMethod.SteepAndRelease => new BrewMethodProfile(
            Method: method,
            DoseMin: 10, DoseMax: 60, DoseDefault: 18, DoseStep: 0.5m,
            OutputMin: 100, OutputMax: 800, OutputDefault: 300, OutputStep: 5,
            TimeMin: 60, TimeMax: 600, TimeDefault: 240, TimeStep: 5),
        _ => new BrewMethodProfile(
            Method: method,
            DoseMin: 5, DoseMax: 30, DoseDefault: 18, DoseStep: 0.1m,
            OutputMin: 10, OutputMax: 100, OutputDefault: 36, OutputStep: 0.5m,
            TimeMin: 10, TimeMax: 60, TimeDefault: 28, TimeStep: 1)
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
    public static GrindMicronRangeSpec GrindMicronRange(this BrewMethod method) => method switch
    {
        BrewMethod.Turkish        => new(Min: 50,  Max: 225,  Step: 5,  Default: 130),
        BrewMethod.Espresso       => new(Min: 175, Max: 380,  Step: 5,  Default: 270),
        BrewMethod.Moka           => new(Min: 350, Max: 650,  Step: 10, Default: 500),
        BrewMethod.V60            => new(Min: 400, Max: 700,  Step: 10, Default: 550),
        BrewMethod.PourOver       => new(Min: 400, Max: 925,  Step: 25, Default: 660),
        BrewMethod.Aeropress      => new(Min: 300, Max: 960,  Step: 25, Default: 600),
        BrewMethod.Siphon         => new(Min: 360, Max: 800,  Step: 25, Default: 580),
        BrewMethod.Drip           => new(Min: 290, Max: 900,  Step: 25, Default: 600),
        BrewMethod.Cupping        => new(Min: 450, Max: 850,  Step: 25, Default: 650),
        BrewMethod.SteepAndRelease=> new(Min: 450, Max: 825,  Step: 25, Default: 640),
        BrewMethod.FrenchPress    => new(Min: 690, Max: 1300, Step: 25, Default: 1000),
        BrewMethod.ColdBrew       => new(Min: 825, Max: 1300, Step: 25, Default: 1100),
        BrewMethod.ColdDrip       => new(Min: 825, Max: 1300, Step: 25, Default: 1100),
        _                         => new(Min: 200, Max: 1300, Step: 25, Default: 600),
    };
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
