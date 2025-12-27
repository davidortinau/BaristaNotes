namespace BaristaNotes.Core.Services.DTOs;

/// <summary>
/// Simplified shot data for AI context.
/// </summary>
public record ShotContextDto
{
    /// <summary>
    /// Grams of coffee in.
    /// </summary>
    public decimal DoseIn { get; init; }

    /// <summary>
    /// Grams out (actual yield).
    /// </summary>
    public decimal? ActualOutput { get; init; }

    /// <summary>
    /// Extraction time in seconds.
    /// </summary>
    public decimal? ActualTime { get; init; }

    /// <summary>
    /// Grinder setting.
    /// </summary>
    public string GrindSetting { get; init; } = string.Empty;

    /// <summary>
    /// 0-4 rating scale.
    /// </summary>
    public int? Rating { get; init; }

    /// <summary>
    /// User's tasting notes (optional free text).
    /// </summary>
    public string? TastingNotes { get; init; }

    /// <summary>
    /// Type of drink (e.g., "Espresso", "Latte", "Americano").
    /// </summary>
    public string? DrinkType { get; init; }

    /// <summary>
    /// When shot was logged.
    /// </summary>
    public DateTime Timestamp { get; init; }
}
