using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Services;

/// <summary>
/// Service for analyzing images using Azure OpenAI gpt-4o vision capabilities.
/// Counts people in photos to help determine coffee needs.
/// </summary>
public class VisionService : IVisionService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<VisionService> _logger;
    private IChatClient? _visionClient;
    private IChatClient? _extractionClient;

    private const string VisionModelId = "gpt-4o";
    private const string ExtractionModelId = "gpt-4o-mini";
    private const int TimeoutSeconds = 30;

    private const string BeanLabelExtractionSystemPrompt = """
        You are a specialist at reading coffee bag labels. Extract these fields from the image and return ONLY JSON, no prose:
        {
          "name": string or null,       // The specific bean/blend name (e.g. "Yirgacheffe", "Monarch", "Black Cat Espresso")
          "roaster": string or null,    // The roastery/brand (e.g. "Blue Bottle", "Onyx", "Counter Culture")
          "origin": string or null,     // Country, region, or farm (e.g. "Ethiopia", "Colombia Huila")
          "roastDate": "YYYY-MM-DD" or null  // Only if a roast date is explicitly printed
        }
        If a field is unclear or missing from the label, use null. Never guess a date.
        """;

    private const string SystemPrompt = """
        You are a helpful barista assistant with vision capabilities.
        When shown an image, count the number of people visible and help calculate coffee needs.
        
        Rules:
        - Count all visible people in the image accurately
        - Each person needs 1 cup/shot of coffee
        - Each shot requires approximately 18 grams of coffee beans
        - Be friendly and helpful in your response
        - If you cannot clearly see people or the image is unclear, say so
        - Keep responses concise (1-2 sentences)
        """;

    public VisionService(IConfiguration configuration, ILogger<VisionService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<bool> IsAvailableAsync()
    {
        var endpoint = _configuration["AzureOpenAI:Endpoint"];
        var apiKey = _configuration["AzureOpenAI:ApiKey"];
        var isAvailable = !string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey);
        return Task.FromResult(isAvailable);
    }

    /// <inheritdoc />
    public async Task<VisionAnalysisResult> AnalyzeImageAsync(
        Stream imageStream,
        string userQuestion,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = GetOrCreateVisionClient();
            if (client == null)
            {
                return VisionAnalysisResult.Error("Vision service is not configured.");
            }

            // Read image to bytes and convert to base64
            using var memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream, cancellationToken);
            var imageBytes = memoryStream.ToArray();
            var base64Image = Convert.ToBase64String(imageBytes);

            _logger.LogDebug("Analyzing image ({Size} bytes) with question: {Question}", 
                imageBytes.Length, userQuestion);

            // Build the multimodal message with image
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, SystemPrompt),
                new(ChatRole.User, new AIContent[]
                {
                    new TextContent(userQuestion),
                    new DataContent(imageBytes, "image/jpeg")
                })
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

            var response = await client.GetResponseAsync(messages, cancellationToken: cts.Token);
            var responseText = response.Text ?? "I couldn't analyze the image.";

            _logger.LogInformation("Vision analysis complete: {Response}", responseText);

            // Parse the response to extract people count
            var peopleCount = ExtractPeopleCount(responseText);

            return VisionAnalysisResult.Ok(peopleCount, responseText);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Vision analysis timed out");
            return VisionAnalysisResult.Error("Analysis timed out. Please try again.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing image");
            return VisionAnalysisResult.Error($"Failed to analyze image: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<BeanLabelExtraction> ExtractBeanLabelAsync(
        Stream imageStream,
        CancellationToken ct = default)
    {
        string? rawResponse = null;
        try
        {
            var client = GetOrCreateExtractionClient();
            if (client == null)
            {
                return new BeanLabelExtraction
                {
                    Success = false,
                    ErrorMessage = "Vision service is not configured."
                };
            }

            using var memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream, ct);
            var imageBytes = memoryStream.ToArray();

            _logger.LogDebug("Extracting bean label fields from image ({Size} bytes)", imageBytes.Length);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, BeanLabelExtractionSystemPrompt),
                new(ChatRole.User, new AIContent[]
                {
                    new TextContent("Extract the fields."),
                    new DataContent(imageBytes, "image/jpeg")
                })
            };

            var options = new ChatOptions
            {
                ResponseFormat = ChatResponseFormat.Json
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

            var response = await client.GetResponseAsync(messages, options, cts.Token);
            rawResponse = response.Text;

            var result = BeanLabelParser.ParseResponse(rawResponse);
            if (result.Success)
            {
                _logger.LogInformation(
                    "Bean label extracted: name={Name} roaster={Roaster} origin={Origin} roastDate={RoastDate}",
                    result.Name, result.Roaster, result.Origin, result.RoastDate);
            }
            else
            {
                _logger.LogWarning("Bean label extraction failed to parse: {Error}", result.ErrorMessage);
            }
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Bean label extraction timed out");
            return new BeanLabelExtraction
            {
                Success = false,
                ErrorMessage = "Extraction timed out. Please try again.",
                RawResponse = rawResponse
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting bean label");
            return new BeanLabelExtraction
            {
                Success = false,
                ErrorMessage = $"Failed to extract bean label: {ex.Message}",
                RawResponse = rawResponse
            };
        }
    }

    /// <summary>
    /// Gets or creates the Azure OpenAI vision client (gpt-4o).
    /// </summary>
    private IChatClient? GetOrCreateVisionClient()
    {
        if (_visionClient != null)
        {
            return _visionClient;
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
                new ApiKeyCredential(apiKey));
            _visionClient = azureClient.GetChatClient(VisionModelId).AsIChatClient();
            return _visionClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Azure OpenAI vision client");
            return null;
        }
    }

    /// <summary>
    /// Gets or creates the Azure OpenAI extraction client (gpt-4o-mini) for OCR-style field extraction.
    /// </summary>
    private IChatClient? GetOrCreateExtractionClient()
    {
        if (_extractionClient != null)
        {
            return _extractionClient;
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
                new ApiKeyCredential(apiKey));
            _extractionClient = azureClient.GetChatClient(ExtractionModelId).AsIChatClient();
            return _extractionClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Azure OpenAI extraction client");
            return null;
        }
    }

    /// <summary>
    /// Extracts the people count from the AI response text.
    /// </summary>
    private static int ExtractPeopleCount(string responseText)
    {
        // Look for common patterns like "I see X people", "X people", "count of X"
        var patterns = new[]
        {
            @"(?:I\s+(?:see|count|spot|notice|can\s+see))\s+(\d+)\s+(?:people|person|individuals?)",
            @"(?:there\s+(?:are|is))\s+(\d+)\s+(?:people|person|individuals?)",
            @"(\d+)\s+(?:people|person|individuals?)",
            @"(?:count|total)\s+(?:of\s+)?(\d+)",
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                responseText, 
                pattern, 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            if (match.Success && int.TryParse(match.Groups[1].Value, out var count))
            {
                return count;
            }
        }

        // Check for words like "no one", "nobody", "empty"
        if (System.Text.RegularExpressions.Regex.IsMatch(
            responseText, 
            @"\b(no\s+one|nobody|no\s+people|empty|zero)\b", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            return 0;
        }

        // Check for "one person" or "a person"
        if (System.Text.RegularExpressions.Regex.IsMatch(
            responseText, 
            @"\b(one|a|single)\s+person\b", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            return 1;
        }

        // Default to 0 if we can't parse
        return 0;
    }
}
