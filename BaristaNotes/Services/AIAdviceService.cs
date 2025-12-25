using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Services;

/// <summary>
/// Service for generating AI-powered espresso shot advice.
/// Tries on-device AI first (Apple Intelligence), falls back to OpenAI if available.
/// Once local client fails, it is disabled for the remainder of the app session.
/// </summary>
public class AIAdviceService : IAIAdviceService
{
    private readonly IShotService _shotService;
    private readonly IFeedbackService _feedbackService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AIAdviceService> _logger;

    // Injected on-device client (from AddPlatformChatClient)
    private readonly IChatClient? _localClient;

    // OpenAI client created on demand
    private IChatClient? _openAIClient;

    // Session-level flag: once local client fails, don't retry until app restart
    private bool _localClientDisabled = false;

    private const string ModelId = "gpt-4o-mini";
    private const int LocalTimeoutSeconds = 3;
    private const int CloudTimeoutSeconds = 10;

    // Source strings for transparency
    private const string SourceOnDevice = "via Apple Intelligence";
    private const string SourceCloud = "via OpenAI";

    private const string SystemPrompt = @"You are an expert barista assistant helping home espresso enthusiasts improve their shots. 
You have deep knowledge of espresso extraction, grind settings, dosing, and timing.

When analyzing shots, consider:
- Extraction ratio (dose in vs yield out) - typical target is 1:2 to 1:2.5
- Extraction time - typical range is 25-35 seconds for a balanced shot
- Grind setting adjustments - finer grind slows extraction, coarser speeds it up
- Bean age affects extraction - fresher beans may need coarser grind
- Rating patterns from history indicate user preferences

Provide concise, actionable advice. Focus on 1-3 specific adjustments the user can try.
Be encouraging and practical. Avoid technical jargon unless necessary.";

    public AIAdviceService(
        IShotService shotService,
        IFeedbackService feedbackService,
        IConfiguration configuration,
        ILogger<AIAdviceService> logger,
        IChatClient? chatClient = null)
    {
        _shotService = shotService;
        _feedbackService = feedbackService;
        _configuration = configuration;
        _logger = logger;
        _localClient = chatClient;
    }

    /// <inheritdoc />
    public Task<bool> IsConfiguredAsync()
    {
        // Available if local client works (and not disabled) OR OpenAI key exists
        var hasLocalClient = _localClient != null && !_localClientDisabled;
        var hasOpenAIKey = !string.IsNullOrWhiteSpace(_configuration["OpenAI:ApiKey"]);
        var isConfigured = hasLocalClient || hasOpenAIKey;
        return Task.FromResult(isConfigured);
    }

    /// <inheritdoc />
    public async Task<AIAdviceResponseDto> GetAdviceForShotAsync(
        int shotId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check configuration
            if (!await IsConfiguredAsync())
            {
                return new AIAdviceResponseDto
                {
                    Success = false,
                    ErrorMessage = "AI advice is temporarily unavailable. Please try again later."
                };
            }

            // Get shot context
            var context = await _shotService.GetShotContextForAIAsync(shotId);
            if (context == null)
            {
                return new AIAdviceResponseDto
                {
                    Success = false,
                    ErrorMessage = "Shot not found."
                };
            }

            // Build the prompt using shared utility
            var userMessage = AIPromptBuilder.BuildPrompt(context);

            // Build messages
            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, SystemPrompt),
                new ChatMessage(ChatRole.User, userMessage)
            };

            // Try to get response with fallback
            var (response, source) = await TryGetResponseWithFallbackAsync(
                messages,
                LocalTimeoutSeconds,
                CloudTimeoutSeconds,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(response))
            {
                return new AIAdviceResponseDto
                {
                    Success = false,
                    ErrorMessage = "AI service error. Please try again later."
                };
            }

            return new AIAdviceResponseDto
            {
                Success = true,
                Advice = response,
                Source = source,
                GeneratedAt = DateTime.UtcNow
            };
        }
        catch (OperationCanceledException)
        {
            return new AIAdviceResponseDto
            {
                Success = false,
                ErrorMessage = "Request timed out. Please try again."
            };
        }
        catch (HttpRequestException)
        {
            return new AIAdviceResponseDto
            {
                Success = false,
                ErrorMessage = "Unable to connect. Please check your internet connection."
            };
        }
        catch (Exception ex) when (ex.Message.Contains("rate", StringComparison.OrdinalIgnoreCase) ||
                                    ex.Message.Contains("429"))
        {
            return new AIAdviceResponseDto
            {
                Success = false,
                ErrorMessage = "Too many requests. Please wait a moment."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting AI advice for shot {ShotId}", shotId);
            return new AIAdviceResponseDto
            {
                Success = false,
                ErrorMessage = "An unexpected error occurred. Please try again."
            };
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetPassiveInsightAsync(int shotId)
    {
        try
        {
            if (!await IsConfiguredAsync())
            {
                return null;
            }

            var context = await _shotService.GetShotContextForAIAsync(shotId);
            if (context == null)
            {
                return null;
            }

            // Check if there's significant deviation from best shots
            if (!HasSignificantDeviation(context))
            {
                return null;
            }

            var passivePrompt = AIPromptBuilder.BuildPassivePrompt(context);

            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, "You are a brief espresso advisor. Give ONE short sentence of advice (max 15 words)."),
                new ChatMessage(ChatRole.User, passivePrompt)
            };

            // Use shorter timeouts for passive insights (2s local, 5s cloud)
            var (response, _) = await TryGetResponseWithFallbackAsync(
                messages,
                localTimeoutSeconds: 2,
                cloudTimeoutSeconds: 5,
                CancellationToken.None);

            return response;
        }
        catch
        {
            // Passive insights are non-critical - silently fail
            return null;
        }
    }

    /// <summary>
    /// Tries to get a response from available AI clients with fallback.
    /// Tries local client first (if not disabled), then falls back to OpenAI.
    /// </summary>
    /// <returns>Tuple of (response text, source string) or (null, null) if all fail.</returns>
    private async Task<(string? Response, string? Source)> TryGetResponseWithFallbackAsync(
        List<ChatMessage> messages,
        int localTimeoutSeconds,
        int cloudTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        // Try local client first if available and not disabled
        if (_localClient != null && !_localClientDisabled)
        {
            try
            {
                _logger.LogDebug("Attempting on-device AI request");

                using var localCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                localCts.CancelAfter(TimeSpan.FromSeconds(localTimeoutSeconds));

                var localResponse = await _localClient.GetResponseAsync(
                    messages,
                    cancellationToken: localCts.Token);

                if (!string.IsNullOrWhiteSpace(localResponse?.Text))
                {
                    _logger.LogDebug("On-device AI request succeeded");
                    return (localResponse.Text, SourceOnDevice);
                }
            }
            catch (Exception ex)
            {
                // Local client failed - disable for remainder of session
                _localClientDisabled = true;
                _logger.LogWarning(ex, "On-device AI failed, disabling for session. Falling back to OpenAI.");
            }
        }

        // Try OpenAI as fallback
        var openAIClient = GetOrCreateOpenAIClient();
        if (openAIClient != null)
        {
            // Check if user explicitly cancelled before attempting fallback
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Skipping OpenAI fallback - request was cancelled");
                return (null, null);
            }

            try
            {
                _logger.LogDebug("Attempting OpenAI request");

                // Create independent timeout for OpenAI - don't link to potentially-cancelled local token
                using var cloudCts = new CancellationTokenSource();
                cloudCts.CancelAfter(TimeSpan.FromSeconds(cloudTimeoutSeconds));

                // Also cancel if user cancels during the call
                using var registration = cancellationToken.Register(() => cloudCts.Cancel());

                var cloudResponse = await openAIClient.GetResponseAsync(
                    messages,
                    cancellationToken: cloudCts.Token);

                if (!string.IsNullOrWhiteSpace(cloudResponse?.Text))
                {
                    _logger.LogDebug("OpenAI request succeeded");
                    return (cloudResponse.Text, SourceCloud);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OpenAI request failed");
                throw; // Re-throw to be handled by caller's error handling
            }
        }

        return (null, null);
    }

    /// <summary>
    /// Gets or creates the OpenAI client instance.
    /// </summary>
    private IChatClient? GetOrCreateOpenAIClient()
    {
        if (_openAIClient != null)
        {
            return _openAIClient;
        }

        var apiKey = _configuration["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        try
        {
            var openAIClient = new OpenAIClient(apiKey);
            _openAIClient = openAIClient.GetChatClient(ModelId).AsIChatClient();
            return _openAIClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create OpenAI client");
            return null;
        }
    }

    /// <summary>
    /// Builds the user prompt from shot context.
    /// Uses the shared AIPromptBuilder for testability.
    /// </summary>
    internal string BuildPrompt(AIAdviceRequestDto context) => AIPromptBuilder.BuildPrompt(context);

    /// <summary>
    /// Checks if current shot deviates significantly from best historical shots.
    /// </summary>
    private bool HasSignificantDeviation(AIAdviceRequestDto context)
    {
        var bestShots = context.HistoricalShots
            .Where(s => s.Rating.HasValue && s.Rating >= 3)
            .ToList();

        if (bestShots.Count == 0)
        {
            // No best shots to compare against
            return false;
        }

        var avgDose = bestShots.Average(s => (double)s.DoseIn);
        var avgOutput = bestShots
            .Where(s => s.ActualOutput.HasValue)
            .Select(s => (double)s.ActualOutput!.Value)
            .DefaultIfEmpty(0)
            .Average();
        var avgTime = bestShots
            .Where(s => s.ActualTime.HasValue)
            .Select(s => (double)s.ActualTime!.Value)
            .DefaultIfEmpty(0)
            .Average();

        var current = context.CurrentShot;

        // Check dose deviation (>10%)
        if (Math.Abs((double)current.DoseIn - avgDose) / avgDose > 0.1)
            return true;

        // Check output deviation (>15%)
        if (current.ActualOutput.HasValue && avgOutput > 0)
        {
            if (Math.Abs((double)current.ActualOutput.Value - avgOutput) / avgOutput > 0.15)
                return true;
        }

        // Check time deviation (>20%)
        if (current.ActualTime.HasValue && avgTime > 0)
        {
            if (Math.Abs((double)current.ActualTime.Value - avgTime) / avgTime > 0.2)
                return true;
        }

        return false;
    }

    /// <summary>
    /// System prompt for bean recommendations.
    /// </summary>
    private const string RecommendationSystemPrompt = @"You are an expert barista assistant. 
Based on the bean characteristics and any historical shot data provided, recommend starting extraction parameters.
Use standard espresso ratios (1:2 to 1:2.5) and typical extraction times (25-35 seconds).
Adjust for roast level and freshness. Darker roasts typically need coarser grind.
Respond ONLY with valid JSON - no other text.";

    /// <inheritdoc />
    public async Task<AIRecommendationDto> GetRecommendationsForBeanAsync(
        int beanId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check configuration
            if (!await IsConfiguredAsync())
            {
                return new AIRecommendationDto
                {
                    Success = false,
                    ErrorMessage = "AI recommendations are temporarily unavailable. Please try again later."
                };
            }

            // Get bean context
            var context = await _shotService.GetBeanRecommendationContextAsync(beanId);
            if (context == null)
            {
                return new AIRecommendationDto
                {
                    Success = false,
                    ErrorMessage = "Bean not found."
                };
            }

            // Build prompt based on whether bean has history
            var userMessage = context.HasHistory
                ? AIPromptBuilder.BuildReturningBeanPrompt(context)
                : AIPromptBuilder.BuildNewBeanPrompt(context);

            var recommendationType = context.HasHistory
                ? RecommendationType.ReturningBean
                : RecommendationType.NewBean;

            // Build messages
            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, RecommendationSystemPrompt),
                new ChatMessage(ChatRole.User, userMessage)
            };

            // Try to get response with fallback
            var (response, source) = await TryGetResponseWithFallbackAsync(
                messages,
                LocalTimeoutSeconds,
                CloudTimeoutSeconds,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(response))
            {
                return new AIRecommendationDto
                {
                    Success = false,
                    ErrorMessage = "AI service error. Please try again later."
                };
            }

            // Parse the JSON response
            return ParseRecommendationResponse(response, recommendationType, source);
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw cancellation to caller
        }
        catch (HttpRequestException)
        {
            return new AIRecommendationDto
            {
                Success = false,
                ErrorMessage = "Unable to connect. Please check your internet connection."
            };
        }
        catch (Exception ex) when (ex.Message.Contains("rate", StringComparison.OrdinalIgnoreCase) ||
                                    ex.Message.Contains("429"))
        {
            return new AIRecommendationDto
            {
                Success = false,
                ErrorMessage = "Too many requests. Please wait a moment."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting AI recommendations for bean {BeanId}", beanId);
            return new AIRecommendationDto
            {
                Success = false,
                ErrorMessage = "An unexpected error occurred. Please try again."
            };
        }
    }

    /// <summary>
    /// Parses the AI JSON response into an AIRecommendationDto.
    /// </summary>
    private AIRecommendationDto ParseRecommendationResponse(
        string response, 
        RecommendationType recommendationType, 
        string? source)
    {
        try
        {
            // Clean up response - AI sometimes wraps JSON in markdown code blocks
            var jsonText = response.Trim();
            if (jsonText.StartsWith("```json"))
                jsonText = jsonText.Substring(7);
            else if (jsonText.StartsWith("```"))
                jsonText = jsonText.Substring(3);
            if (jsonText.EndsWith("```"))
                jsonText = jsonText.Substring(0, jsonText.Length - 3);
            jsonText = jsonText.Trim();

            using var doc = System.Text.Json.JsonDocument.Parse(jsonText);
            var root = doc.RootElement;

            var dose = root.TryGetProperty("dose", out var doseEl) 
                ? doseEl.GetDecimal() 
                : 18m; // Default
            var grind = root.TryGetProperty("grind", out var grindEl) 
                ? grindEl.GetString() ?? "medium" 
                : "medium";
            var output = root.TryGetProperty("output", out var outputEl) 
                ? outputEl.GetDecimal() 
                : 36m; // Default
            var duration = root.TryGetProperty("duration", out var durationEl) 
                ? durationEl.GetDecimal() 
                : 30m; // Default

            return new AIRecommendationDto
            {
                Success = true,
                Dose = dose,
                GrindSetting = grind,
                Output = output,
                Duration = duration,
                RecommendationType = recommendationType,
                Source = source
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI recommendation response: {Response}", response);
            
            // Return defaults on parse failure
            return new AIRecommendationDto
            {
                Success = true,
                Dose = 18m,
                GrindSetting = "medium",
                Output = 36m,
                Duration = 30m,
                RecommendationType = recommendationType,
                Source = source,
                Confidence = "Using default values due to parsing issue"
            };
        }
    }
}
