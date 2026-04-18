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
    /// Recommended grinder setting.
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
