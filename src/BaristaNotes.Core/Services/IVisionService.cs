namespace BaristaNotes.Core.Services;

/// <summary>
/// Service for analyzing images using AI vision capabilities.
/// Used for counting people in photos to determine coffee needs.
/// </summary>
public interface IVisionService
{
    /// <summary>
    /// Analyzes an image to count people and calculate coffee needs.
    /// </summary>
    /// <param name="imageStream">The image stream to analyze.</param>
    /// <param name="userQuestion">The user's question about the image.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Analysis result with people count and coffee recommendations.</returns>
    Task<VisionAnalysisResult> AnalyzeImageAsync(
        Stream imageStream, 
        string userQuestion, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the vision service is configured and available.
    /// </summary>
    Task<bool> IsAvailableAsync();
}

/// <summary>
/// Result of a vision analysis request.
/// </summary>
public class VisionAnalysisResult
{
    /// <summary>
    /// Whether the analysis was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Number of people detected in the image.
    /// </summary>
    public int PeopleCount { get; set; }

    /// <summary>
    /// Number of cups/shots of coffee needed (typically equals PeopleCount).
    /// </summary>
    public int CupsNeeded { get; set; }

    /// <summary>
    /// Estimated grams of coffee beans needed (CupsNeeded × 18g).
    /// </summary>
    public int BeansNeededGrams { get; set; }

    /// <summary>
    /// The AI-generated response message to show the user.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Error message if the analysis failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static VisionAnalysisResult Ok(int peopleCount, string message)
    {
        var cupsNeeded = peopleCount;
        return new VisionAnalysisResult
        {
            Success = true,
            PeopleCount = peopleCount,
            CupsNeeded = cupsNeeded,
            BeansNeededGrams = cupsNeeded * 18, // 18g per shot
            Message = message
        };
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static VisionAnalysisResult Error(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}
