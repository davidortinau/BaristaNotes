using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Services;

/// <summary>
/// Service for generating AI-powered espresso shot advice.
/// API key is app-provided (loaded from configuration), not user-configured.
/// </summary>
public interface IAIAdviceService
{
    /// <summary>
    /// Checks if the AI service is configured and ready to use.
    /// Returns true if the app-provided API key is available.
    /// </summary>
    /// <returns>True if API key is configured.</returns>
    Task<bool> IsConfiguredAsync();

    /// <summary>
    /// Gets detailed AI advice for a specific shot.
    /// </summary>
    /// <param name="shotId">The ID of the shot to analyze.</param>
    /// <param name="cancellationToken">Cancellation token for timeout.</param>
    /// <returns>AI advice response with suggestions.</returns>
    Task<AIAdviceResponseDto> GetAdviceForShotAsync(
        int shotId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a brief passive insight for a shot if parameters deviate from history.
    /// </summary>
    /// <param name="shotId">The ID of the shot to check.</param>
    /// <returns>Brief insight string, or null if no significant deviation.</returns>
    Task<string?> GetPassiveInsightAsync(int shotId);

    /// <summary>
    /// Gets AI-powered extraction parameter recommendations for a selected bean.
    /// </summary>
    /// <param name="beanId">The ID of the selected bean.</param>
    /// <param name="cancellationToken">Cancellation token for request cancellation.</param>
    /// <returns>Recommendation with suggested dose, grind, output, and duration.</returns>
    Task<AIRecommendationDto> GetRecommendationsForBeanAsync(
        int beanId,
        CancellationToken cancellationToken = default);
}
