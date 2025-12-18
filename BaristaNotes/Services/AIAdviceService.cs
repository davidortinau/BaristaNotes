using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Services;

/// <summary>
/// Service for generating AI-powered espresso shot advice using OpenAI gpt-5-nano.
/// API key is app-provided via IConfiguration, not user-configured.
/// </summary>
public class AIAdviceService : IAIAdviceService
{
    private readonly IShotService _shotService;
    private readonly IFeedbackService _feedbackService;
    private readonly IConfiguration _configuration;
    private IChatClient? _chatClient;

    private const string ModelId = "gpt-4o-mini";//"gpt-5-nano";
    private const int TimeoutSeconds = 10;

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
        IChatClient chatClient)
    {
        _shotService = shotService;
        _feedbackService = feedbackService;
        _configuration = configuration;
        _chatClient = chatClient;
    }

    /// <inheritdoc />
    public Task<bool> IsConfiguredAsync()
    {
        var apiKey = _configuration["OpenAI:ApiKey"];
        var isConfigured = !string.IsNullOrWhiteSpace(apiKey);
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

            // Get or create the chat client
            var client = GetOrCreateClient();
            if (client == null)
            {
                return new AIAdviceResponseDto
                {
                    Success = false,
                    ErrorMessage = "AI advice is temporarily unavailable. Please try again later."
                };
            }

            // Create timeout cancellation
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

            // Call the AI
            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, SystemPrompt),
                new ChatMessage(ChatRole.User, userMessage)
            };

            var response = await client.GetResponseAsync(
                messages,
                cancellationToken: timeoutCts.Token);

            var advice = response?.Text;

            if (string.IsNullOrWhiteSpace(advice))
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
                Advice = advice,
                GeneratedAt = DateTime.UtcNow
            };
        }
        catch (OperationCanceledException oce)
        {
            return new AIAdviceResponseDto
            {
                Success = false,
                ErrorMessage = $"Request timed out. Please try again. {oce.Message}"
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
        catch (Exception)
        {
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

            // For passive insights, use a shorter prompt for quick response
            var client = GetOrCreateClient();
            if (client == null)
            {
                return null;
            }

            var passivePrompt = AIPromptBuilder.BuildPassivePrompt(context);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, "You are a brief espresso advisor. Give ONE short sentence of advice (max 15 words)."),
                new ChatMessage(ChatRole.User, passivePrompt)
            };

            var response = await client.GetResponseAsync(messages, cancellationToken: cts.Token);

            return response?.Text;
        }
        catch
        {
            // Passive insights are non-critical - silently fail
            return null;
        }
    }

    /// <summary>
    /// Gets or creates the IChatClient instance.
    /// </summary>
    private IChatClient? GetOrCreateClient()
    {
        if (_chatClient != null)
        {
            return _chatClient;
        }

        var apiKey = _configuration["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        try
        {
            var openAIClient = new OpenAIClient(apiKey);
            _chatClient = openAIClient.GetChatClient(ModelId).AsIChatClient();
            return _chatClient;
        }
        catch
        {
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
}
