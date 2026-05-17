using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.ClientModel;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.Grind;

namespace BaristaNotes.Services;

/// <summary>
/// Chat-client backed implementation of <see cref="IGrindTranslationAI"/>.
/// Mirrors <see cref="AIAdviceService"/>'s provider selection: on-device
/// (Apple Intelligence) first, Azure OpenAI fallback. Requests a strongly
/// typed <see cref="GrindTranslationAIResponse"/> via
/// <c>IChatClient.GetResponseAsync&lt;T&gt;()</c>.
/// </summary>
public class GrindTranslationAI : IGrindTranslationAI
{
    private const string ModelId = "gpt-4.1-mini";
    private const int LocalTimeoutSeconds = 8;
    private const int CloudTimeoutSeconds = 15;

    private const string SystemPrompt = @"You are an expert coffee grinder calibration assistant.
Given a specific grinder make+model, a brew method, and a recipe grind hint (microns like ""725µm"",
descriptors like ""medium-fine"", or a competing grinder's numeric setting), return a JSON object with
min_setting, max_setting, and suggested_setting on the target grinder's native scale, plus a
confidence (low/medium/high) and a one-sentence explanation. Prefer settings reported by credible
community sources (DF64 users, roaster published notes, Home Barista forum). If you know clean
micron↔setting anchor points for this grinder, include up to 3 of them in micron_anchors so the app
can self-calibrate.";

    private readonly IChatClient? _localClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GrindTranslationAI> _logger;
    private IChatClient? _azureClient;
    private bool _localDisabled;

    public GrindTranslationAI(
        IConfiguration configuration,
        ILogger<GrindTranslationAI> logger,
        IChatClient? chatClient = null)
    {
        _configuration = configuration;
        _logger = logger;
        _localClient = chatClient;
    }

    public async Task<GrindTranslationAIResponse?> TranslateAsync(
        string grinderModel,
        BrewMethod method,
        string grindHint,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User,
                $"Grinder: {grinderModel}\nBrew method: {method}\nRecipe grind hint: {grindHint}\n\nReturn only the JSON object."),
        };

        if (_localClient != null && !_localDisabled)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(LocalTimeoutSeconds));
                var r = await _localClient.GetResponseAsync<GrindTranslationAIResponse>(messages, cancellationToken: cts.Token);
                if (r?.Result != null) return r.Result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _localDisabled = true;
                _logger.LogWarning(ex, "On-device AI grind translation failed; disabling for session.");
            }
        }

        var azure = GetOrCreateAzureClient();
        if (azure == null) return null;

        try
        {
            using var cloudCts = new CancellationTokenSource();
            cloudCts.CancelAfter(TimeSpan.FromSeconds(CloudTimeoutSeconds));
            using var reg = cancellationToken.Register(() => cloudCts.Cancel());
            var r = await azure.GetResponseAsync<GrindTranslationAIResponse>(messages, cancellationToken: cloudCts.Token);
            return r?.Result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure OpenAI grind translation failed.");
            return null;
        }
    }

    private IChatClient? GetOrCreateAzureClient()
    {
        if (_azureClient != null) return _azureClient;

        var endpoint = _configuration["AzureOpenAI:Endpoint"];
        var apiKey = _configuration["AzureOpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey)) return null;

        try
        {
            var azure = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
            _azureClient = azure.GetChatClient(ModelId).AsIChatClient();
            return _azureClient;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create Azure OpenAI client for grind translation.");
            return null;
        }
    }
}
