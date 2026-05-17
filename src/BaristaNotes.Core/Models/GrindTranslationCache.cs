using BaristaNotes.Core.Models.Enums;

namespace BaristaNotes.Core.Models;

/// <summary>
/// Cached grind-translation result for a (grinder model, recipe grind hint,
/// brew method) triple. The AI fallback persists every successful response
/// here with a TTL so subsequent translations for the same bean+grinder on
/// the same machine hit cache.
/// </summary>
public class GrindTranslationCache
{
    public int Id { get; set; }

    /// <summary>
    /// Case-insensitive, trimmed grinder model (e.g. "turin df64"). Used as
    /// part of the cache key so two users with the same grinder share hits.
    /// </summary>
    public string GrinderModelNormalized { get; set; } = string.Empty;

    /// <summary>
    /// Normalized grind hint form. For microns we store "um:NNN"; for
    /// descriptors we store "desc:medium-fine"; for numeric scales
    /// "num:ek43:7.5"; for unknown we store "raw:..." lowercased.
    /// </summary>
    public string GrindHintNormalized { get; set; } = string.Empty;

    public BrewMethod BrewMethod { get; set; }

    public decimal? MinSetting { get; set; }
    public decimal? MaxSetting { get; set; }
    public decimal? SuggestedSetting { get; set; }

    /// <summary>Confidence label as a plain string: "low", "medium", "high".</summary>
    public string Confidence { get; set; } = "low";

    /// <summary>Source label: "Deterministic" | "AI" | "Community" | "Default".</summary>
    public string Source { get; set; } = "Default";

    /// <summary>Short human-readable rationale for the UI tooltip.</summary>
    public string? Explanation { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
