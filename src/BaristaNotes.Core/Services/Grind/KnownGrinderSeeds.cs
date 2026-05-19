namespace BaristaNotes.Core.Services.Grind;

/// <summary>
/// Maps a grinder's free-form display name to community-consensus seed
/// anchors so users see a useful translation before they've calibrated
/// their grinder. Currently knows only the Turin DF64 family; extend as
/// more reliable seed data lands.
/// </summary>
/// <remarks>
/// Centralized so the picker UI, <see cref="GrindTranslationService"/>, and
/// any future settings UI all agree on which grinders ship with seeds.
/// </remarks>
public static class KnownGrinderSeeds
{
    /// <summary>
    /// If <paramref name="grinderName"/> matches a known seeded grinder,
    /// return its anchor list. Otherwise return null.
    /// </summary>
    public static IReadOnlyList<GrindAnchor>? TryGet(string? grinderName)
    {
        if (string.IsNullOrWhiteSpace(grinderName)) return null;

        // DF64 family: "DF64", "Turin DF64", "DF64v", "DF64E", etc.
        if (grinderName.Contains("DF64", StringComparison.OrdinalIgnoreCase))
            return DeterministicGrindInterpolator.DF64SeedAnchors;

        return null;
    }
}
