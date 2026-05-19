namespace BaristaNotes.Core.Services.DTOs;

/// <summary>
/// Response from AI service for bean extraction recommendations.
/// </summary>
public record AIRecommendationDto
{
    /// <summary>
    /// Whether recommendation was generated successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Recommended dose in grams.
    /// </summary>
    public decimal Dose { get; init; }

    /// <summary>
    /// Recommended grind, as an advisory descriptor (e.g. "medium-fine" or
    /// "around 270µm"). This is intentionally a free-form string: it's the
    /// AI's suggestion to the user, not a value we auto-apply to the shot.
    /// The actual <see cref="ShotRecord.GrindMicrons"/> is captured by the
    /// user via the picker, optionally informed by this advice.
    /// </summary>
    public string GrindSetting { get; init; } = string.Empty;

    /// <summary>
    /// Recommended output in grams.
    /// </summary>
    public decimal Output { get; init; }

    /// <summary>
    /// Recommended extraction duration in seconds.
    /// </summary>
    public decimal Duration { get; init; }

    /// <summary>
    /// Type of recommendation: NewBean or ReturningBean.
    /// </summary>
    public RecommendationType RecommendationType { get; init; }

    /// <summary>
    /// Optional confidence indicator for the recommendation.
    /// </summary>
    public string? Confidence { get; init; }

    /// <summary>
    /// Error message if Success is false.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The source of the AI response for transparency.
    /// Values: "via Apple Intelligence", "via OpenAI", or null if failed.
    /// </summary>
    public string? Source { get; init; }
}
