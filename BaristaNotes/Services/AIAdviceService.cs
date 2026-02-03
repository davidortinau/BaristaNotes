using System.Text;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Services;

/// <summary>
/// Service for generating AI-powered espresso shot advice.
/// Tries on-device AI first (Apple Intelligence), falls back to Azure OpenAI if available.
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

    // Azure OpenAI client created on demand
    private IChatClient? _azureOpenAIClient;

    // Session-level flag: once local client fails, don't retry until app restart
    private bool _localClientDisabled = false;

    private const string ModelId = "gpt-4.1-mini";
    private const int LocalTimeoutSeconds = 10;
    private const int CloudTimeoutSeconds = 20;

    // Source strings for transparency
    private const string SourceOnDevice = "via Apple Intelligence";
    private const string SourceCloud = "via Azure OpenAI";

    private const string SystemPrompt = @"You are an expert barista assistant helping improve espresso shots.
Analyze the shot data and provide 1-3 specific parameter adjustments.
Consider extraction ratio (target 1:2 to 1:2.5), time (25-35s), grind, and dose.
Be practical and specific with amounts (e.g., '0.5g', '2 clicks finer', '3 seconds').
Provide brief reasoning in one sentence.";

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
        // Available if local client works (and not disabled) OR Azure OpenAI is configured
        var hasLocalClient = _localClient != null && !_localClientDisabled;
        var hasAzureOpenAI = !string.IsNullOrWhiteSpace(_configuration["AzureOpenAI:Endpoint"]) &&
                             !string.IsNullOrWhiteSpace(_configuration["AzureOpenAI:ApiKey"]);
        var isConfigured = hasLocalClient || hasAzureOpenAI;
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

            // Try to get typed response with fallback
            var (advice, source) = await TryGetTypedResponseWithFallbackAsync<ShotAdviceJson>(
                messages,
                LocalTimeoutSeconds,
                CloudTimeoutSeconds,
                cancellationToken);

            if (advice == null)
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
                Adjustments = advice.Adjustments,
                Reasoning = advice.Reasoning,
                Source = source,
                PromptSent = userMessage,
                HistoricalShotsCount = context.HistoricalShots.Count,
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
                localTimeoutSeconds: 10,
                cloudTimeoutSeconds: 20,
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
    /// Tries to get a typed response from available AI clients with fallback.
    /// Uses IChatClient.GetResponseAsync{T}() for structured JSON output.
    /// Tries local client first (if not disabled), then falls back to OpenAI.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the response to.</typeparam>
    /// <returns>Tuple of (typed response, source string) or (null, null) if all fail.</returns>
    private async Task<(T? Response, string? Source)> TryGetTypedResponseWithFallbackAsync<T>(
        List<ChatMessage> messages,
        int localTimeoutSeconds,
        int cloudTimeoutSeconds,
        CancellationToken cancellationToken) where T : class
    {
        // Try local client first if available and not disabled
        if (_localClient != null && !_localClientDisabled)
        {
            try
            {
                _logger.LogDebug("Attempting on-device AI request for type {ResponseType}", typeof(T).Name);

                using var localCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                localCts.CancelAfter(TimeSpan.FromSeconds(localTimeoutSeconds));

                var localResponse = await _localClient.GetResponseAsync<T>(
                    messages,
                    cancellationToken: localCts.Token);

                if (localResponse?.Result != null)
                {
                    _logger.LogDebug("On-device AI request succeeded for type {ResponseType}", typeof(T).Name);
                    return (localResponse.Result, SourceOnDevice);
                }
            }
            catch (Exception ex)
            {
                // Local client failed - disable for remainder of session
                _localClientDisabled = true;
                _logger.LogWarning(ex, "On-device AI failed for type {ResponseType}, disabling for session. Falling back to Azure OpenAI.", typeof(T).Name);
            }
        }

        // Try Azure OpenAI as fallback
        var azureClient = GetOrCreateAzureOpenAIClient();
        if (azureClient != null)
        {
            // Check if user explicitly cancelled before attempting fallback
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Skipping Azure OpenAI fallback - request was cancelled");
                return (null, null);
            }

            try
            {
                _logger.LogDebug("Attempting Azure OpenAI request for type {ResponseType}", typeof(T).Name);

                // Create independent timeout for Azure OpenAI - don't link to potentially-cancelled local token
                using var cloudCts = new CancellationTokenSource();
                cloudCts.CancelAfter(TimeSpan.FromSeconds(cloudTimeoutSeconds));

                // Also cancel if user cancels during the call
                using var registration = cancellationToken.Register(() => cloudCts.Cancel());

                var cloudResponse = await azureClient.GetResponseAsync<T>(
                    messages,
                    cancellationToken: cloudCts.Token);

                if (cloudResponse?.Result != null)
                {
                    _logger.LogDebug("Azure OpenAI request succeeded for type {ResponseType}", typeof(T).Name);
                    return (cloudResponse.Result, SourceCloud);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Azure OpenAI request failed for type {ResponseType}", typeof(T).Name);
                throw; // Re-throw to be handled by caller's error handling
            }
        }

        return (null, null);
    }

    /// <summary>
    /// Tries to get a text response from available AI clients with fallback.
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
                _logger.LogWarning(ex, "On-device AI failed, disabling for session. Falling back to Azure OpenAI.");
            }
        }

        // Try Azure OpenAI as fallback
        var azureClient = GetOrCreateAzureOpenAIClient();
        if (azureClient != null)
        {
            // Check if user explicitly cancelled before attempting fallback
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Skipping Azure OpenAI fallback - request was cancelled");
                return (null, null);
            }

            try
            {
                _logger.LogDebug("Attempting Azure OpenAI request");

                // Create independent timeout for Azure OpenAI - don't link to potentially-cancelled local token
                using var cloudCts = new CancellationTokenSource();
                cloudCts.CancelAfter(TimeSpan.FromSeconds(cloudTimeoutSeconds));

                // Also cancel if user cancels during the call
                using var registration = cancellationToken.Register(() => cloudCts.Cancel());

                var cloudResponse = await azureClient.GetResponseAsync(
                    messages,
                    cancellationToken: cloudCts.Token);

                if (!string.IsNullOrWhiteSpace(cloudResponse?.Text))
                {
                    _logger.LogDebug("Azure OpenAI request succeeded");
                    return (cloudResponse.Text, SourceCloud);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Azure OpenAI request failed");
                throw; // Re-throw to be handled by caller's error handling
            }
        }

        return (null, null);
    }

    /// <summary>
    /// Gets or creates the Azure OpenAI client instance.
    /// </summary>
    private IChatClient? GetOrCreateAzureOpenAIClient()
    {
        if (_azureOpenAIClient != null)
        {
            return _azureOpenAIClient;
        }

        var endpoint = _configuration["AzureOpenAI:Endpoint"];
        var apiKey = _configuration["AzureOpenAI:ApiKey"];
        
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        try
        {
            var azureClient = new AzureOpenAIClient(
                new Uri(endpoint),
                new AzureKeyCredential(apiKey));
            _azureOpenAIClient = azureClient.GetChatClient(ModelId).AsIChatClient();
            return _azureOpenAIClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Azure OpenAI client");
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
Recommend espresso extraction parameters based on bean characteristics.
Use standard ratios (1:2 to 1:2.5), typical times (25-35s).
Adjust for roast level: darker roasts need coarser grind.";

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

            // Try to get typed response with fallback
            var (recommendation, source) = await TryGetTypedResponseWithFallbackAsync<BeanRecommendationJson>(
                messages,
                LocalTimeoutSeconds,
                CloudTimeoutSeconds,
                cancellationToken);

            if (recommendation == null)
            {
                return new AIRecommendationDto
                {
                    Success = false,
                    ErrorMessage = "AI service error. Please try again later."
                };
            }

            return new AIRecommendationDto
            {
                Success = true,
                Dose = recommendation.Dose,
                GrindSetting = recommendation.Grind,
                Output = recommendation.Output,
                Duration = recommendation.Duration,
                RecommendationType = recommendationType,
                Source = source
            };
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

}
