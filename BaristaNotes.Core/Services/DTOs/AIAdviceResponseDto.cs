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
    public string? Advice { get; init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Timestamp of response.
    /// </summary>
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}
