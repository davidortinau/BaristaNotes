using System.Globalization;
using System.Text.RegularExpressions;

namespace BaristaNotes.Core.Services.Grind;

/// <summary>
/// Kind of grind reference we parsed from a free-form hint string.
/// </summary>
public enum GrindHintKind
{
    /// <summary>Could not be parsed into any known form.</summary>
    Unknown = 0,
    /// <summary>Microns (e.g. "725µm", "725 microns").</summary>
    Microns = 1,
    /// <summary>Descriptive bucket (e.g. "medium-fine", "fine").</summary>
    Descriptive = 2,
    /// <summary>Numeric on a specific grinder scale (e.g. "EK43 7.5").</summary>
    Numeric = 3,
}

/// <summary>
/// Broad grind-size buckets from common recipe language. Each bucket maps
/// to an approximate micron range used as fallback for interpolation when
/// only a descriptor is available.
/// </summary>
public enum GrindDescriptor
{
    ExtraFine,
    Fine,
    MediumFine,
    Medium,
    MediumCoarse,
    Coarse,
    ExtraCoarse,
}

/// <summary>
/// Parsed representation of a free-form grind hint.
/// </summary>
public sealed record ParsedGrindHint(
    GrindHintKind Kind,
    string RawHint,
    string Normalized,
    decimal? Microns = null,
    (decimal Min, decimal Max)? MicronRange = null,
    GrindDescriptor? Descriptor = null,
    string? NumericScale = null,
    decimal? NumericValue = null);

/// <summary>
/// Parses recipe-provided grind hints into a normalized representation. Values
/// observed from real roaster sites: "725µm", "750 microns", "medium",
/// "medium-fine", "fine", "EK43: 7.5", "Mahlkonig K30 2.8", "2.5 on DF64".
///
/// When a descriptor is detected we also return an approximate micron range
/// so the deterministic interpolator has something to work with.
/// </summary>
public static class GrindHintParser
{
    private static readonly TimeSpan GrindRegexTimeout = TimeSpan.FromMilliseconds(500);

    private static readonly Regex MicronsRegex = new(
        @"(?<v>\d{2,4}(?:\.\d+)?)\s*(?:µm|μm|um|micron[s]?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        GrindRegexTimeout);

    private static readonly Regex NumericWithScaleRegex = new(
        @"(?:(?<scale>[A-Za-z][A-Za-z0-9 ]{1,20})\s*[:\-]\s*(?<v>\d{1,3}(?:\.\d+)?))|(?:(?<v2>\d{1,3}(?:\.\d+)?)\s+on\s+(?<scale2>[A-Za-z][A-Za-z0-9 ]{1,20}))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        GrindRegexTimeout);

    public static ParsedGrindHint Parse(string? rawHint)
    {
        if (string.IsNullOrWhiteSpace(rawHint))
            return new ParsedGrindHint(GrindHintKind.Unknown, rawHint ?? string.Empty, "raw:");

        var raw = rawHint.Trim();
        var lower = raw.ToLowerInvariant();

        try
        {
            var m = MicronsRegex.Match(raw);
            if (m.Success &&
                decimal.TryParse(m.Groups["v"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) &&
                v is >= 50 and <= 3000)
            {
                var microns = Math.Round(v, 0);
                return new ParsedGrindHint(
                    Kind: GrindHintKind.Microns,
                    RawHint: raw,
                    Normalized: $"um:{microns:0}",
                    Microns: microns,
                    MicronRange: (microns - 25, microns + 25));
            }
        }
        catch (RegexMatchTimeoutException) { }

        var descriptor = MatchDescriptor(lower);
        if (descriptor != null)
        {
            var range = DescriptorMicronRange(descriptor.Value);
            var mid = Math.Round((range.Min + range.Max) / 2m, 0);
            return new ParsedGrindHint(
                Kind: GrindHintKind.Descriptive,
                RawHint: raw,
                Normalized: $"desc:{DescriptorToken(descriptor.Value)}",
                Microns: mid,
                MicronRange: range,
                Descriptor: descriptor);
        }

        try
        {
            var numeric = NumericWithScaleRegex.Match(raw);
            if (numeric.Success)
            {
                var scaleGroup = numeric.Groups["scale"].Success ? numeric.Groups["scale"] : numeric.Groups["scale2"];
                var valueGroup = numeric.Groups["v"].Success ? numeric.Groups["v"] : numeric.Groups["v2"];
                if (scaleGroup.Success && valueGroup.Success &&
                    decimal.TryParse(valueGroup.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var nv))
                {
                    var scaleNorm = scaleGroup.Value.Trim().ToLowerInvariant();
                    return new ParsedGrindHint(
                        Kind: GrindHintKind.Numeric,
                        RawHint: raw,
                        Normalized: $"num:{scaleNorm}:{nv}",
                        NumericScale: scaleNorm,
                        NumericValue: nv);
                }
            }
        }
        catch (RegexMatchTimeoutException) { }

        return new ParsedGrindHint(
            Kind: GrindHintKind.Unknown,
            RawHint: raw,
            Normalized: $"raw:{lower}");
    }

    private static GrindDescriptor? MatchDescriptor(string lower)
    {
        if (lower.Contains("extra-fine") || lower.Contains("extra fine") || lower.Contains("very fine"))
            return GrindDescriptor.ExtraFine;
        if (lower.Contains("extra-coarse") || lower.Contains("extra coarse") || lower.Contains("very coarse"))
            return GrindDescriptor.ExtraCoarse;
        if (lower.Contains("medium-fine") || lower.Contains("medium fine"))
            return GrindDescriptor.MediumFine;
        if (lower.Contains("medium-coarse") || lower.Contains("medium coarse"))
            return GrindDescriptor.MediumCoarse;
        if (lower.Contains("medium"))
            return GrindDescriptor.Medium;
        if (lower.Contains("fine"))
            return GrindDescriptor.Fine;
        if (lower.Contains("coarse"))
            return GrindDescriptor.Coarse;
        return null;
    }

    private static string DescriptorToken(GrindDescriptor d) => d switch
    {
        GrindDescriptor.ExtraFine => "extra-fine",
        GrindDescriptor.Fine => "fine",
        GrindDescriptor.MediumFine => "medium-fine",
        GrindDescriptor.Medium => "medium",
        GrindDescriptor.MediumCoarse => "medium-coarse",
        GrindDescriptor.Coarse => "coarse",
        GrindDescriptor.ExtraCoarse => "extra-coarse",
        _ => "unknown"
    };

    public static (decimal Min, decimal Max) DescriptorMicronRange(GrindDescriptor d) => d switch
    {
        GrindDescriptor.ExtraFine => (100m, 200m),
        GrindDescriptor.Fine => (200m, 400m),
        GrindDescriptor.MediumFine => (400m, 600m),
        GrindDescriptor.Medium => (600m, 800m),
        GrindDescriptor.MediumCoarse => (800m, 1000m),
        GrindDescriptor.Coarse => (1000m, 1200m),
        GrindDescriptor.ExtraCoarse => (1200m, 1500m),
        _ => (600m, 800m)
    };
}
