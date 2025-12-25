namespace BaristaNotes.Core.Services.DTOs;

/// <summary>
/// Response from AI service.
/// </summary>
public record AIAdviceResponseDto
{
    /// <summary>
    /// Whether advice was generated successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The AI-generated advice text.
    /// </summary>
    [Obsolete("Use Adjustments and Reasoning properties for structured advice output.")]
    public string? Advice { get; init; }

    /// <summary>
    /// List of specific parameter adjustments recommended by AI.
    /// </summary>
    public List<ShotAdjustment> Adjustments { get; init; } = [];

    /// <summary>
    /// Brief reasoning explaining why these adjustments are recommended.
    /// </summary>
    public string? Reasoning { get; init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The source of the AI response for transparency.
    /// Values: "via Apple Intelligence", "via OpenAI", or null if failed.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Timestamp of response.
    /// </summary>
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}
