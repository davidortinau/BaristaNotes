using BaristaNotes.Core.Services.DTOs;

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
    /// <param name="imageStream">The image stream to analyze. Caller retains ownership and is responsible for disposal.</param>
    /// <param name="userQuestion">The user's question about the image.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Analysis result with people count and coffee recommendations.</returns>
    Task<VisionAnalysisResult> AnalyzeImageAsync(
        Stream imageStream, 
        string userQuestion, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts coffee-bag-label fields (name, roaster, origin, roast date) from a photo.
    /// Uses a lighter vision model (gpt-4o-mini) optimized for OCR-style extraction.
    /// </summary>
    /// <param name="imageStream">The image stream of the coffee bag label. Caller retains ownership and is responsible for disposal.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Structured extraction result; <see cref="BeanLabelExtraction.Success"/> is false on any failure.</returns>
    Task<BeanLabelExtraction> ExtractBeanLabelAsync(Stream imageStream, CancellationToken ct = default);

    /// <summary>
    /// Compares a target photo against a set of candidate profile avatars and
    /// returns the best match (or null if no avatar plausibly matches).
    /// Multimodal call: gpt-4o receives the target image plus each candidate
    /// avatar with the candidate's name, and is asked to pick the best ID.
    /// </summary>
    /// <param name="targetPhoto">Image bytes of the unknown person.</param>
    /// <param name="candidates">Profiles with avatar bytes already loaded. Profiles without an avatar should be omitted by the caller.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<PersonIdentificationResult> IdentifyPersonFromPhotoAsync(
        byte[] targetPhoto,
        IReadOnlyList<PersonIdentificationCandidate> candidates,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if the vision service is configured and available.
    /// </summary>
    Task<bool> IsAvailableAsync();
}

/// <summary>
/// One candidate profile for face-matching.
/// </summary>
public sealed class PersonIdentificationCandidate
{
    public required int ProfileId { get; init; }
    public required string Name { get; init; }
    public required byte[] AvatarBytes { get; init; }
}

/// <summary>
/// Result of <see cref="IVisionService.IdentifyPersonFromPhotoAsync"/>.
/// </summary>
public sealed class PersonIdentificationResult
{
    public bool Success { get; init; }
    public int? MatchedProfileId { get; init; }
    public string? MatchedName { get; init; }
    /// <summary>Free-form rationale from the model. Useful for "moderate confidence" cases.</summary>
    public string? Rationale { get; init; }
    public string? ErrorMessage { get; init; }

    public static PersonIdentificationResult NoMatch(string? rationale = null) => new()
    {
        Success = true,
        Rationale = rationale
    };

    public static PersonIdentificationResult Match(int profileId, string name, string? rationale) => new()
    {
        Success = true,
        MatchedProfileId = profileId,
        MatchedName = name,
        Rationale = rationale
    };

    public static PersonIdentificationResult Error(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };
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
