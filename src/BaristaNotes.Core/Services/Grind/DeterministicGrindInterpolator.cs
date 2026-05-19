using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BaristaNotes.Core.Services.Grind;

/// <summary>A single calibration anchor for a grinder: micron ↔ native setting.</summary>
public sealed record GrindAnchor(
    [property: JsonPropertyName("micron")] decimal Micron,
    [property: JsonPropertyName("setting")] decimal Setting,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("updatedAt")] DateTime? UpdatedAt = null);

/// <summary>
/// Deterministic micron-to-setting interpolator. Given a set of calibration
/// anchors for a grinder and a target micron value, returns a suggested
/// setting and a plausible min/max range via linear interpolation between
/// the two nearest anchors (clamped at the extremes).
///
/// Ships with seed anchors for the Turin DF64 so the user sees a sensible
/// first-pass translation before any AI call runs.
/// </summary>
public static class DeterministicGrindInterpolator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Anchor curve for the Turin DF64 family (Gen 1, Gen 2, DF64v, DF64E,
    /// MiiCoffee, G-IOTA — all share the 0–90 stepless dial). Derived from the
    /// Honest Coffee Guide DF64V brew-method chart (Dec 2025): for each method
    /// we take the dial-range midpoint and the micron-range midpoint, then
    /// keep the points that yield a strictly monotonic dial-by-micron curve.
    /// Sanity-checked: 550µm sits between V60 (500,41) and Aeropress (600,50)
    /// and interpolates to ~46, inside the chart's V60 dial range (30–59).
    /// </summary>
    public static readonly IReadOnlyList<GrindAnchor> DF64SeedAnchors = new[]
    {
        new GrindAnchor(50m,   0m,  "df64v-chart"),  // Turkish floor
        new GrindAnchor(140m,  6m,  "df64v-chart"),  // Turkish midpoint
        new GrindAnchor(280m,  18m, "df64v-chart"),  // Espresso midpoint
        new GrindAnchor(500m,  41m, "df64v-chart"),  // Moka top / V60 lower
        new GrindAnchor(600m,  50m, "df64v-chart"),  // Aeropress / Filter cluster
        new GrindAnchor(700m,  60m, "df64v-chart"),  // V60 / Pour-over upper
        new GrindAnchor(1000m, 75m, "df64v-chart"),  // French press mid
        new GrindAnchor(1300m, 90m, "df64v-chart"),  // Cold brew / drip ceiling
    };

    public static IReadOnlyList<GrindAnchor> ParseAnchors(string? anchorsJson)
    {
        if (string.IsNullOrWhiteSpace(anchorsJson)) return Array.Empty<GrindAnchor>();
        try
        {
            var anchors = JsonSerializer.Deserialize<List<GrindAnchor>>(anchorsJson, JsonOptions);
            return anchors ?? (IReadOnlyList<GrindAnchor>)Array.Empty<GrindAnchor>();
        }
        catch (JsonException)
        {
            return Array.Empty<GrindAnchor>();
        }
    }

    public static string SerializeAnchors(IEnumerable<GrindAnchor> anchors) =>
        JsonSerializer.Serialize(anchors, JsonOptions);

    /// <summary>
    /// Interpolate a target micron value against a set of anchors. Returns null
    /// if fewer than 2 anchors are available.
    /// </summary>
    public static InterpolationResult? Interpolate(
        IReadOnlyList<GrindAnchor> anchors,
        decimal targetMicron,
        (decimal Min, decimal Max)? targetMicronRange = null,
        decimal? minSetting = null,
        decimal? maxSetting = null)
    {
        if (anchors.Count < 2) return null;

        var sorted = anchors.OrderBy(a => a.Micron).ToList();
        var suggested = InterpolateOne(sorted, targetMicron);

        decimal min, max;
        if (targetMicronRange.HasValue)
        {
            var low = InterpolateOne(sorted, targetMicronRange.Value.Min);
            var high = InterpolateOne(sorted, targetMicronRange.Value.Max);
            min = Math.Min(low, high);
            max = Math.Max(low, high);
        }
        else
        {
            // Build ±10% micron window around target for a sensible default range.
            var lowMicron = targetMicron * 0.9m;
            var highMicron = targetMicron * 1.1m;
            var low = InterpolateOne(sorted, lowMicron);
            var high = InterpolateOne(sorted, highMicron);
            min = Math.Min(low, high);
            max = Math.Max(low, high);
        }

        if (minSetting.HasValue)
        {
            min = Math.Max(min, minSetting.Value);
            suggested = Math.Max(suggested, minSetting.Value);
        }
        if (maxSetting.HasValue)
        {
            max = Math.Min(max, maxSetting.Value);
            suggested = Math.Min(suggested, maxSetting.Value);
        }

        return new InterpolationResult(
            Min: Math.Round(min, 2),
            Max: Math.Round(max, 2),
            Suggested: Math.Round(suggested, 2),
            AnchorsUsed: sorted.Count);
    }

    private static decimal InterpolateOne(IReadOnlyList<GrindAnchor> sorted, decimal micron)
    {
        if (micron <= sorted[0].Micron) return sorted[0].Setting;
        if (micron >= sorted[^1].Micron) return sorted[^1].Setting;

        for (var i = 0; i < sorted.Count - 1; i++)
        {
            var a = sorted[i];
            var b = sorted[i + 1];
            if (micron >= a.Micron && micron <= b.Micron)
            {
                var span = b.Micron - a.Micron;
                if (span == 0) return a.Setting;
                var t = (micron - a.Micron) / span;
                return a.Setting + t * (b.Setting - a.Setting);
            }
        }
        return sorted[^1].Setting;
    }
}

public sealed record InterpolationResult(
    decimal Min,
    decimal Max,
    decimal Suggested,
    int AnchorsUsed);
