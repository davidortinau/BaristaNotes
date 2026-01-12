using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.Number;
using OpenAI;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace BaristaNotes.Services;

// Force en-US culture for number parsing throughout this service
internal static class VoiceCommandCulture
{
    internal static readonly CultureInfo EnUS = CultureInfo.GetCultureInfo("en-US");
}

/// <summary>
/// Service for processing voice commands using AI tool calling.
/// Supports on-device AI (Apple Intelligence) with OpenAI fallback.
/// </summary>
public class VoiceCommandService : IVoiceCommandService
{
    private readonly IShotService _shotService;
    private readonly IBeanService _beanService;
    private readonly IBagService _bagService;
    private readonly IEquipmentService _equipmentService;
    private readonly IUserProfileService _userProfileService;
    private readonly IDataChangeNotifier _dataChangeNotifier;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VoiceCommandService> _logger;

    // Injected on-device client (from AddPlatformChatClient)
    private readonly IChatClient? _localClient;

    // OpenAI client created on demand
    private IChatClient? _openAIClient;

    // Session-level flag: once local client fails, don't retry until app restart
    private bool _localClientDisabled = false;

    private const string ModelId = "gpt-4o-mini";
    private const int LocalTimeoutSeconds = 15;
    private const int CloudTimeoutSeconds = 30;

    private const string SystemPrompt = """
        You are BaristaNotes voice assistant. Help users log espresso shots, manage their coffee data, and query their shot history.

        CONTEXT:
        - Rating scale is 0-4 (0=terrible, 1=bad, 2=average, 3=good, 4=excellent)
        - Common terms: dose (coffee in), yield/output (coffee out), pull time, extraction
        - "Pretty good" = rating 3, "excellent/amazing" = rating 4, "not great/meh" = rating 2, "okay" = rating 2
        - "this morning" or "last shot" refers to most recent shot

        QUERY CAPABILITIES:
        - "What was my last shot?" or "Tell me about my last shot" - returns full details of most recent shot
        - "Find shots with Ethiopia" - searches by bean name
        - "Show me 4-star shots" - filters by rating
        - "What shots did I make for Sarah?" - filters by made for
        - "Find my shots from this week" - filters by time period

        RULES:
        1. Always use available tools to complete actions immediately
        2. NEVER ask follow-up questions - use defaults for missing optional values
        3. For shots: dose, output, and time are required. Rating defaults to 2 (average) if not specified
        4. Execute the tool immediately with provided values, don't ask for confirmation
        5. Keep responses concise - just confirm what was done in one sentence
        6. For queries, return the information directly from the tool response
        """;

    public VoiceCommandService(
        IShotService shotService,
        IBeanService beanService,
        IBagService bagService,
        IEquipmentService equipmentService,
        IUserProfileService userProfileService,
        IDataChangeNotifier dataChangeNotifier,
        IConfiguration configuration,
        ILogger<VoiceCommandService> logger,
        IChatClient? chatClient = null)
    {
        _shotService = shotService;
        _beanService = beanService;
        _bagService = bagService;
        _equipmentService = equipmentService;
        _userProfileService = userProfileService;
        _dataChangeNotifier = dataChangeNotifier;
        _configuration = configuration;
        _logger = logger;
        _localClient = chatClient;
    }

    /// <inheritdoc />
    public async Task<VoiceCommandResponseDto> InterpretCommandAsync(
        VoiceCommandRequestDto request,
        CancellationToken cancellationToken = default)
    {
        // Normalize the transcript to fix common speech-to-text issues
        var normalizedTranscript = NormalizeTranscript(request.Transcript);
        _logger.LogDebug("Interpreting command: {Transcript} (normalized: {Normalized})", 
            request.Transcript, normalizedTranscript);

        try
        {
            var client = GetChatClientWithTools();
            if (client == null)
            {
                return new VoiceCommandResponseDto
                {
                    Intent = CommandIntent.Unknown,
                    ErrorMessage = "Voice commands are temporarily unavailable."
                };
            }

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, SystemPrompt),
                new(ChatRole.User, normalizedTranscript)
            };

            var chatOptions = CreateChatOptions();
            var response = await client.GetResponseAsync(messages, chatOptions, cancellationToken);

            return new VoiceCommandResponseDto
            {
                Intent = CommandIntent.Unknown, // Intent determined by tool call
                ConfirmationMessage = response.Text ?? "Command processed.",
                RequiresConfirmation = false
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Voice command interpretation cancelled");
            return new VoiceCommandResponseDto
            {
                Intent = CommandIntent.Cancel,
                ErrorMessage = "Cancelled"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error interpreting voice command: {Transcript}", request.Transcript);
            
            // Provide more specific error messages based on exception type
            var errorMessage = ex switch
            {
                HttpRequestException => "Network error. Please check your connection and try again.",
                TaskCanceledException => "Request timed out. Please try again.",
                _ when ex.Message.Contains("API key", StringComparison.OrdinalIgnoreCase) => 
                    "Voice commands require an OpenAI API key. Please configure it in settings.",
                _ when ex.Message.Contains("401", StringComparison.OrdinalIgnoreCase) || 
                       ex.Message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) => 
                    "Invalid API key. Please check your OpenAI configuration.",
                _ when ex.Message.Contains("429", StringComparison.OrdinalIgnoreCase) || 
                       ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) => 
                    "Too many requests. Please wait a moment and try again.",
                _ => $"Sorry, I couldn't process that command. Error: {ex.Message}"
            };
            
            return new VoiceCommandResponseDto
            {
                Intent = CommandIntent.Unknown,
                ErrorMessage = errorMessage
            };
        }
    }

    /// <inheritdoc />
    public async Task<VoiceToolResultDto> ExecuteCommandAsync(
        VoiceCommandResponseDto response,
        CancellationToken cancellationToken = default)
    {
        // Tool execution happens automatically via UseFunctionInvocation()
        // This method is here for manual execution if needed
        return new VoiceToolResultDto
        {
            Success = true,
            Message = response.ConfirmationMessage
        };
    }

    /// <inheritdoc />
    public async Task<VoiceToolResultDto> ProcessCommandAsync(
        VoiceCommandRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var interpretation = await InterpretCommandAsync(request, cancellationToken);

        if (!string.IsNullOrEmpty(interpretation.ErrorMessage))
        {
            return new VoiceToolResultDto
            {
                Success = false,
                Message = interpretation.ErrorMessage
            };
        }

        return new VoiceToolResultDto
        {
            Success = true,
            Message = interpretation.ConfirmationMessage
        };
    }

    private IChatClient? GetChatClientWithTools()
    {
        // IMPORTANT: Apple Intelligence (local client) does NOT support function/tool calling.
        // The FunctionCallContent type throws ArgumentException on Apple Intelligence.
        // We must skip directly to OpenAI for voice commands that require tool execution.
        //
        // If we ever get a local AI that supports tools, we can re-enable this:
        // if (_localClient != null && !_localClientDisabled && SupportsToolCalling(_localClient))
        // {
        //     ...
        // }
        
        _logger.LogDebug("Using OpenAI for voice commands (local AI doesn't support tool calling)");

        // Use OpenAI which supports function calling
        var openAIClient = GetOrCreateOpenAIClient();
        if (openAIClient != null)
        {
            return new ChatClientBuilder(openAIClient)
                .UseFunctionInvocation()
                .Build();
        }

        _logger.LogWarning("No AI client available for voice commands. Configure OpenAI:ApiKey in settings.");
        return null;
    }

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
    /// Normalizes speech-to-text transcript to fix common issues like mixed number formats
    /// and misrecognized coffee terminology.
    /// Uses Microsoft.Recognizers.Text library for robust number parsing, with pre-processing
    /// for iOS-specific quirks like "30 4" instead of "34".
    /// </summary>
    private string NormalizeTranscript(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
            return transcript;

        var result = transcript;

        // COFFEE VOCABULARY NORMALIZATION: Fix commonly misrecognized coffee terms
        // Speech recognition often mishears domain-specific words - correct them here
        var coffeeVocabularyFixes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Grind variations
            {"grand", "grind"},
            {"grin", "grind"},
            {"grined", "grind"},
            {"grinding", "grind"},
            {"ground", "grind"},  // Context: "ground setting" -> "grind setting"
            
            // Dose/dose variations
            {"doze", "dose"},
            {"those", "dose"},  // Common mishearing
            {"doughs", "dose"},
            
            // Espresso variations
            {"expresso", "espresso"},
            {"express oh", "espresso"},
            {"s presso", "espresso"},
            
            // Yield/output variations
            {"yelled", "yield"},
            {"yeild", "yield"},
            
            // Shot variations
            {"short", "shot"},  // In context of coffee
            {"shut", "shot"},
            
            // Extraction variations
            {"extra action", "extraction"},
            {"extra shin", "extraction"},
            
            // Portafilter variations
            {"porta filter", "portafilter"},
            {"port a filter", "portafilter"},
            {"quarter filter", "portafilter"},
            
            // Tamper variations
            {"temper", "tamper"},
            {"tapper", "tamper"},
            
            // Puck variations
            {"pock", "puck"},
            {"pack", "puck"},
            
            // Crema variations
            {"cream a", "crema"},
            {"creamer", "crema"},
            
            // Bloom variations
            {"blue", "bloom"},  // In coffee context
            
            // Pre-infusion variations
            {"pre infusion", "preinfusion"},
            {"pre-infusion", "preinfusion"},
            
            // Rating/stars
            {"store", "star"},
            {"stores", "stars"},
            {"stare", "star"},
            {"stares", "stars"},
            
            // Common bean origins often misheard
            {"ethiopia", "Ethiopia"},
            {"ethiopian", "Ethiopian"},
            {"columbia", "Colombia"},
            {"columbian", "Colombian"},
            {"brazilian", "Brazilian"},
            {"brazil", "Brazil"},
            {"guatemalan", "Guatemalan"},
            {"guatemala", "Guatemala"},
            {"costa rican", "Costa Rican"},
            {"costa rica", "Costa Rica"},
            {"kenyan", "Kenyan"},
            {"kenya", "Kenya"},
            {"sumatran", "Sumatran"},
            {"sumatra", "Sumatra"},
            {"yirgacheffe", "Yirgacheffe"},
            {"your gosh if", "Yirgacheffe"},
            {"your gosh if a", "Yirgacheffe"},
            
            // Common roasters
            {"counter culture", "Counter Culture"},
            {"blue bottle", "Blue Bottle"},
            {"stumped town", "Stumptown"},
            {"stump town", "Stumptown"},
            {"intelligencia", "Intelligentsia"},
            {"intelligence ya", "Intelligentsia"},
        };

        // Apply vocabulary fixes (word boundary aware where possible)
        foreach (var (misheard, correct) in coffeeVocabularyFixes)
        {
            // Use word boundaries to avoid partial replacements
            result = Regex.Replace(result, $@"\b{Regex.Escape(misheard)}\b", correct, RegexOptions.IgnoreCase);
        }

        // PRE-PROCESSING: iOS speech recognition quirk - splits compound numbers like "34" into "30 4"
        // Pattern: tens digit (20,30,40...90) followed by space and single digit (1-9)
        // "30 4" -> "34", "20 8" -> "28", etc.
        result = Regex.Replace(result, @"\b([2-9])0\s+([1-9])\b", 
            m => $"{m.Groups[1].Value}{m.Groups[2].Value}");
        
        // Also handle: "30 four" -> "34" (tens digit + space + word digit)
        var onesWords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"one", "1"}, {"two", "2"}, {"three", "3"}, {"four", "4"}, {"five", "5"},
            {"six", "6"}, {"seven", "7"}, {"eight", "8"}, {"nine", "9"}
        };
        foreach (var (word, digit) in onesWords)
        {
            result = Regex.Replace(result, $@"\b([2-9])0\s+{word}\b", 
                m => $"{m.Groups[1].Value}{digit}", RegexOptions.IgnoreCase);
        }

        // Use Microsoft.Recognizers.Text to find all number expressions (handles "thirty four" -> 34)
        var recognizedNumbers = NumberRecognizer.RecognizeNumber(result, Culture.English);

        // Sort by position descending so we can replace from end to start without offset issues
        // Filter out invalid values like infinity (recognizer sometimes misparses)
        var sortedResults = recognizedNumbers
            .Where(r => r.Resolution.ContainsKey("value"))
            .Where(r => 
            {
                var val = r.Resolution["value"]?.ToString();
                // Skip infinity, NaN, or other invalid values
                if (string.IsNullOrEmpty(val) || val == "∞" || val == "Infinity" || val == "NaN")
                    return false;
                // Only accept values that parse as valid numbers (use en-US culture to handle Korean phone locale)
                return double.TryParse(val, NumberStyles.Any, VoiceCommandCulture.EnUS, out var d) && double.IsFinite(d);
            })
            .OrderByDescending(r => r.Start)
            .ToList();

        foreach (var recognized in sortedResults)
        {
            var value = recognized.Resolution["value"]?.ToString();
            if (!string.IsNullOrEmpty(value))
            {
                // Replace the original text with the numeric value
                result = result.Remove(recognized.Start, recognized.End - recognized.Start + 1)
                               .Insert(recognized.Start, value);
            }
        }

        // Clean up multiple spaces that may result from replacements
        result = Regex.Replace(result, @"\s+", " ").Trim();

        _logger.LogDebug("Normalized transcript: '{Original}' -> '{Normalized}'", transcript, result);

        return result;
    }

    private ChatOptions CreateChatOptions()
    {
        return new ChatOptions
        {
            Tools =
            [
                AIFunctionFactory.Create(LogShotToolAsync),
                AIFunctionFactory.Create(AddBeanToolAsync),
                AIFunctionFactory.Create(AddBagToolAsync),
                AIFunctionFactory.Create(RateLastShotToolAsync),
                AIFunctionFactory.Create(AddTastingNotesToolAsync),
                AIFunctionFactory.Create(AddEquipmentToolAsync),
                AIFunctionFactory.Create(AddProfileToolAsync),
                AIFunctionFactory.Create(GetShotCountToolAsync),
                AIFunctionFactory.Create(NavigateToToolAsync),
                AIFunctionFactory.Create(FilterShotsToolAsync),
                AIFunctionFactory.Create(GetLastShotToolAsync),
                AIFunctionFactory.Create(FindShotsToolAsync),
            ]
        };
    }

    #region Tool Functions

    [Description("Logs a new espresso shot with the specified parameters")]
    private async Task<string> LogShotToolAsync(
        [Description("Coffee dose in grams (input weight)")] double doseGrams,
        [Description("Coffee output/yield in grams")] double outputGrams,
        [Description("Extraction time in seconds")] int timeSeconds,
        [Description("Shot rating from 0-4 (0=terrible, 4=excellent)")] int? rating = null,
        [Description("Tasting notes describing the shot flavor")] string? tastingNotes = null)
    {
        _logger.LogInformation("LogShot tool called: {Dose}g in, {Output}g out, {Time}s, rating: {Rating}",
            doseGrams, outputGrams, timeSeconds, rating);

        try
        {
            // Validate parameters
            if (doseGrams <= 0 || outputGrams <= 0 || timeSeconds <= 0)
            {
                return "Please provide valid dose, output, and time values.";
            }

            if (rating.HasValue && (rating < 0 || rating > 4))
            {
                return "Rating must be between 0 and 4.";
            }

            // Get the most recently used active bag as default
            var activeBags = await _bagService.GetActiveBagsForShotLoggingAsync();
            var defaultBag = activeBags.FirstOrDefault();
            
            if (defaultBag == null)
            {
                return "No active coffee bag found. Please add a bag first before logging shots.";
            }

            // Get the most recent shot to inherit defaults (grind setting, drink type, made by/for, equipment)
            var lastShot = await _shotService.GetMostRecentShotAsync();

            // Convert 0-4 scale to service 1-5 scale (0->1, 4->5)
            // Default to rating 2 (average) if not specified
            var serviceRating = rating.HasValue ? rating.Value + 1 : 3; // 2+1=3 on 1-5 scale

            var createDto = new CreateShotDto
            {
                Timestamp = DateTime.UtcNow,
                BagId = defaultBag.Id,
                DoseIn = (decimal)doseGrams,
                ExpectedOutput = (decimal)outputGrams,
                ExpectedTime = (decimal)timeSeconds,
                ActualOutput = (decimal)outputGrams,
                ActualTime = (decimal)timeSeconds,
                Rating = serviceRating,
                TastingNotes = tastingNotes,
                // Inherit from last shot or use sensible defaults
                GrindSetting = lastShot?.GrindSetting ?? "5.5",
                DrinkType = lastShot?.DrinkType ?? "Espresso",
                MadeById = lastShot?.MadeBy?.Id,
                MadeForId = lastShot?.MadeFor?.Id,
                MachineId = lastShot?.Machine?.Id,
                GrinderId = lastShot?.Grinder?.Id,
                AccessoryIds = lastShot?.Accessories?.Select(a => a.Id).ToList() ?? new List<int>()
            };

            var shot = await _shotService.CreateShotAsync(createDto);
            _dataChangeNotifier.NotifyDataChanged(DataChangeType.ShotCreated, shot);

            var result = $"Shot logged: {doseGrams}g → {outputGrams}g in {timeSeconds}s";
            if (rating.HasValue)
            {
                result += $", rated {rating}/4";
            }
            result += $" (using {defaultBag.BeanName})";

            _logger.LogInformation("Shot logged successfully via voice, ID: {ShotId}, BagId: {BagId}", 
                shot.Id, defaultBag.Id);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging shot via voice");
            return "Sorry, I couldn't log that shot. Please try again.";
        }
    }

    [Description("Creates a new coffee bean entry")]
    private async Task<string> AddBeanToolAsync(
        [Description("Name of the coffee bean")] string name,
        [Description("Roaster company name")] string? roaster = null,
        [Description("Origin country or region")] string? origin = null)
    {
        _logger.LogInformation("AddBean tool called: {Name} from {Roaster}, origin: {Origin}",
            name, roaster, origin);

        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Please provide a bean name.";
            }

            var createDto = new CreateBeanDto
            {
                Name = name,
                Roaster = roaster,
                Origin = origin
            };

            var operationResult = await _beanService.CreateBeanAsync(createDto);
            if (!operationResult.Success)
            {
                return operationResult.ErrorMessage ?? "Failed to create bean.";
            }

            var bean = operationResult.Data!;
            _dataChangeNotifier.NotifyDataChanged(DataChangeType.BeanCreated, bean);

            var result = $"Added bean: {name}";
            if (!string.IsNullOrEmpty(roaster))
            {
                result += $" from {roaster}";
            }

            _logger.LogInformation("Bean created successfully via voice: {Name}, ID: {BeanId}", name, bean.Id);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bean via voice");
            return "Sorry, I couldn't add that bean. Please try again.";
        }
    }

    [Description("Creates a new bag of an existing coffee bean")]
    private async Task<string> AddBagToolAsync(
        [Description("Name of the bean for this bag")] string beanName,
        [Description("Roast date (YYYY-MM-DD, 'today', 'yesterday', or days ago like '3 days ago')")] string? roastDate = null)
    {
        _logger.LogInformation("AddBag tool called: {Bean}, roasted {Date}", beanName, roastDate);

        try
        {
            if (string.IsNullOrWhiteSpace(beanName))
            {
                return "Please provide the bean name for this bag.";
            }

            // Find bean by name (case-insensitive)
            var beans = await _beanService.GetAllActiveBeansAsync();
            var matchingBean = beans.FirstOrDefault(b =>
                b.Name.Equals(beanName, StringComparison.OrdinalIgnoreCase));

            if (matchingBean == null)
            {
                return $"Bean '{beanName}' not found. Please add the bean first or check the name.";
            }

            // Parse roast date
            var parsedDate = ParseRoastDate(roastDate);

            var bag = new Core.Models.Bag
            {
                BeanId = matchingBean.Id,
                RoastDate = parsedDate,
                IsActive = true,
                IsComplete = false,
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow,
                SyncId = Guid.NewGuid()
            };

            var operationResult = await _bagService.CreateBagAsync(bag);
            if (!operationResult.Success)
            {
                return operationResult.ErrorMessage ?? "Failed to create bag.";
            }

            var createdBag = operationResult.Data!;
            _dataChangeNotifier.NotifyDataChanged(DataChangeType.BagCreated, createdBag);

            var result = $"Added bag of {beanName}";
            if (!string.IsNullOrEmpty(roastDate))
            {
                result += $" roasted {parsedDate:MMM d}";
            }

            _logger.LogInformation("Bag created successfully via voice for bean: {BeanName}, ID: {BagId}", beanName, createdBag.Id);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bag via voice");
            return "Sorry, I couldn't add that bag. Please try again.";
        }
    }

    /// <summary>
    /// Parses natural language roast date expressions.
    /// </summary>
    private DateTime ParseRoastDate(string? roastDate)
    {
        if (string.IsNullOrEmpty(roastDate))
            return DateTime.Today;

        var lowerDate = roastDate.ToLowerInvariant().Trim();

        if (lowerDate == "today")
            return DateTime.Today;

        if (lowerDate == "yesterday")
            return DateTime.Today.AddDays(-1);

        // Try "X days ago" pattern
        if (lowerDate.Contains("days ago"))
        {
            var parts = lowerDate.Replace("days ago", "").Trim().Split(' ');
            if (int.TryParse(parts.LastOrDefault(), NumberStyles.Integer, VoiceCommandCulture.EnUS, out var daysAgo))
                return DateTime.Today.AddDays(-daysAgo);
        }

        // Try standard date parsing with en-US culture
        if (DateTime.TryParse(roastDate, VoiceCommandCulture.EnUS, DateTimeStyles.None, out var parsed))
            return parsed;

        // Default to today
        return DateTime.Today;
    }

    [Description("Rates the most recently logged shot")]
    private async Task<string> RateLastShotToolAsync(
        [Description("Rating from 0-4 (0=terrible, 4=excellent)")] int rating)
    {
        _logger.LogInformation("RateLastShot tool called: {Rating}", rating);

        try
        {
            if (rating < 0 || rating > 4)
            {
                return "Rating must be between 0 and 4.";
            }

            var lastShot = await _shotService.GetMostRecentShotAsync();
            if (lastShot == null)
            {
                return "No shots found to rate. Log a shot first.";
            }

            // Convert 0-4 scale to service 1-5 scale
            var serviceRating = rating + 1;

            var updateDto = new UpdateShotDto
            {
                Rating = serviceRating,
                DrinkType = lastShot.DrinkType
            };

            var updatedShot = await _shotService.UpdateShotAsync(lastShot.Id, updateDto);
            _dataChangeNotifier.NotifyDataChanged(DataChangeType.ShotUpdated, updatedShot);

            _logger.LogInformation("Last shot rated via voice: {Rating}, shot ID: {ShotId}", rating, lastShot.Id);
            return $"Rated your last shot {rating}/4";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rating shot via voice");
            return "Sorry, I couldn't rate that shot. Please try again.";
        }
    }

    [Description("Creates new coffee equipment (machine, grinder, tamper, etc.)")]
    private async Task<string> AddEquipmentToolAsync(
        [Description("Equipment name")] string name,
        [Description("Type of equipment: 'machine', 'grinder', 'tamper', 'puckscreen', or 'other'")] string type)
    {
        _logger.LogInformation("AddEquipment tool called: {Name} ({Type})", name, type);

        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Please provide an equipment name.";
            }

            // Parse equipment type
            var equipmentType = ParseEquipmentType(type);

            var createDto = new CreateEquipmentDto
            {
                Name = name,
                Type = equipmentType
            };

            var equipment = await _equipmentService.CreateEquipmentAsync(createDto);
            _dataChangeNotifier.NotifyDataChanged(DataChangeType.EquipmentCreated, equipment);

            _logger.LogInformation("Equipment created successfully via voice: {Name}, ID: {EquipmentId}", name, equipment.Id);
            return $"Added {type}: {name}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating equipment via voice");
            return "Sorry, I couldn't add that equipment. Please try again.";
        }
    }

    /// <summary>
    /// Parses equipment type string to enum.
    /// </summary>
    private EquipmentType ParseEquipmentType(string type)
    {
        var lowerType = type?.ToLowerInvariant().Trim() ?? "";

        return lowerType switch
        {
            "machine" or "espresso machine" => EquipmentType.Machine,
            "grinder" or "coffee grinder" => EquipmentType.Grinder,
            "tamper" => EquipmentType.Tamper,
            "puckscreen" or "puck screen" => EquipmentType.PuckScreen,
            _ => EquipmentType.Other
        };
    }

    [Description("Creates a new user profile")]
    private async Task<string> AddProfileToolAsync(
        [Description("Person's name")] string name)
    {
        _logger.LogInformation("AddProfile tool called: {Name}", name);

        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Please provide a name for the profile.";
            }

            var createDto = new CreateUserProfileDto
            {
                Name = name
            };

            var profile = await _userProfileService.CreateProfileAsync(createDto);
            _dataChangeNotifier.NotifyDataChanged(DataChangeType.ProfileCreated, profile);

            _logger.LogInformation("Profile created successfully via voice: {Name}, ID: {ProfileId}", name, profile.Id);
            return $"Added profile for {name}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating profile via voice");
            return "Sorry, I couldn't add that profile. Please try again.";
        }
    }

    [Description("Gets shot count for a time period")]
    private async Task<string> GetShotCountToolAsync(
        [Description("Period: 'today', 'this week', 'this month', or 'all time'")] string period = "today")
    {
        _logger.LogInformation("GetShotCount tool called: {Period}", period);

        try
        {
            // Get shots with pagination - we'll count them
            var result = await _shotService.GetShotHistoryAsync(0, 1000); // Get up to 1000 shots

            // Filter by period
            var now = DateTime.UtcNow;
            var shots = result.Items;
            int count;

            var lowerPeriod = period?.ToLowerInvariant().Trim() ?? "today";
            switch (lowerPeriod)
            {
                case "today":
                    count = shots.Count(s => s.Timestamp.Date == now.Date);
                    break;
                case "this week" or "week":
                    var weekStart = now.AddDays(-(int)now.DayOfWeek);
                    count = shots.Count(s => s.Timestamp >= weekStart);
                    break;
                case "this month" or "month":
                    count = shots.Count(s => s.Timestamp.Year == now.Year && s.Timestamp.Month == now.Month);
                    break;
                case "all time" or "all":
                    count = result.TotalCount;
                    break;
                default:
                    count = shots.Count(s => s.Timestamp.Date == now.Date);
                    break;
            }

            _logger.LogInformation("Shot count queried via voice for period: {Period}, count: {Count}", period, count);
            return $"You've pulled {count} shot{(count != 1 ? "s" : "")} {lowerPeriod}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shot count via voice");
            return "Sorry, I couldn't get that information. Please try again.";
        }
    }

    [Description("Adds tasting notes to the most recently logged shot")]
    private async Task<string> AddTastingNotesToolAsync(
        [Description("Tasting notes describing flavor, body, acidity, etc.")] string notes)
    {
        _logger.LogInformation("AddTastingNotes tool called: {Notes}", notes);

        try
        {
            if (string.IsNullOrWhiteSpace(notes))
            {
                return "Please provide tasting notes to add.";
            }

            var lastShot = await _shotService.GetMostRecentShotAsync();
            if (lastShot == null)
            {
                return "No shots found to add notes to. Log a shot first.";
            }

            // Append to existing notes if any
            var existingNotes = lastShot.TastingNotes;
            var newNotes = string.IsNullOrEmpty(existingNotes) 
                ? notes 
                : $"{existingNotes}; {notes}";

            var updateDto = new UpdateShotDto
            {
                TastingNotes = newNotes,
                DrinkType = lastShot.DrinkType
            };

            var updatedShot = await _shotService.UpdateShotAsync(lastShot.Id, updateDto);
            _dataChangeNotifier.NotifyDataChanged(DataChangeType.ShotUpdated, updatedShot);

            _logger.LogInformation("Tasting notes added via voice to shot ID: {ShotId}", lastShot.Id);
            return $"Added tasting notes to your last shot: \"{notes}\"";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding tasting notes via voice");
            return "Sorry, I couldn't add those tasting notes. Please try again.";
        }
    }

    [Description("Navigates to a specific page in the app")]
    private Task<string> NavigateToToolAsync(
        [Description("Page to navigate to: 'home', 'activity', 'shots', 'beans', 'equipment', 'settings', 'profile'")] string pageName)
    {
        _logger.LogInformation("NavigateTo tool called: {Page}", pageName);

        try
        {
            var route = ParsePageRoute(pageName);
            if (route == null)
            {
                return Task.FromResult($"Unknown page '{pageName}'. Try: home, activity, beans, equipment, settings, or profile.");
            }

            // Navigate on main thread
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await Microsoft.Maui.Controls.Shell.Current.GoToAsync(route);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error navigating to {Route}", route);
                }
            });

            _logger.LogInformation("Navigating via voice to: {Route}", route);
            return Task.FromResult($"Opening {pageName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating via voice");
            return Task.FromResult("Sorry, I couldn't navigate there. Please try again.");
        }
    }

    /// <summary>
    /// Parses page name to Shell route.
    /// </summary>
    private string? ParsePageRoute(string pageName)
    {
        var lowerPage = pageName?.ToLowerInvariant().Trim() ?? "";

        return lowerPage switch
        {
            "home" or "main" => "//MainPage",
            "activity" or "shots" or "history" or "feed" => "//ActivityFeedPage",
            "beans" or "bean" or "coffee beans" => "//BeanListPage",
            "equipment" or "gear" => "//EquipmentPage",
            "settings" => "//SettingsPage",
            "profile" or "profiles" or "users" => "//ProfilePage",
            _ => null
        };
    }

    [Description("Filters shots by criteria and navigates to activity feed")]
    private async Task<string> FilterShotsToolAsync(
        [Description("Filter by bean name")] string? beanName = null,
        [Description("Filter by period: 'today', 'this week', 'this month'")] string? period = null,
        [Description("Filter by minimum rating (0-4)")] int? minRating = null)
    {
        _logger.LogInformation("FilterShots tool called: bean={Bean}, period={Period}, minRating={MinRating}",
            beanName, period, minRating);

        try
        {
            // Build query parameters for navigation
            var queryParams = new List<string>();

            if (!string.IsNullOrEmpty(beanName))
            {
                queryParams.Add($"bean={Uri.EscapeDataString(beanName)}");
            }

            if (!string.IsNullOrEmpty(period))
            {
                queryParams.Add($"period={Uri.EscapeDataString(period)}");
            }

            if (minRating.HasValue && minRating >= 0 && minRating <= 4)
            {
                queryParams.Add($"minRating={minRating}");
            }

            var route = "//ActivityFeedPage";
            if (queryParams.Count > 0)
            {
                route += "?" + string.Join("&", queryParams);
            }

            // Navigate on main thread
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await Microsoft.Maui.Controls.Shell.Current.GoToAsync(route);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error navigating to {Route}", route);
                }
            });

            // Build response message
            var filterDesc = new List<string>();
            if (!string.IsNullOrEmpty(beanName))
                filterDesc.Add($"{beanName} bean");
            if (!string.IsNullOrEmpty(period))
                filterDesc.Add(period);
            if (minRating.HasValue)
                filterDesc.Add($"{minRating}+ stars");

            var description = filterDesc.Count > 0 
                ? string.Join(", ", filterDesc) 
                : "all";

            _logger.LogInformation("Filtering shots via voice and navigating to activity feed: {Route}", route);
            return $"Showing shots: {description}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering shots via voice");
            return "Sorry, I couldn't filter those shots. Please try again.";
        }
    }

    [Description("Gets details about the most recent/last shot including all settings and values")]
    private async Task<string> GetLastShotToolAsync()
    {
        _logger.LogInformation("GetLastShot tool called");

        try
        {
            var lastShot = await _shotService.GetMostRecentShotAsync();
            if (lastShot == null)
            {
                return "No shots found. Log a shot first.";
            }

            // Build a comprehensive summary of the last shot
            var details = new List<string>
            {
                $"Last shot ({lastShot.Timestamp:MMM d} at {lastShot.Timestamp:h:mm tt}):",
                $"• Dose: {lastShot.DoseIn:F1}g in → {lastShot.ActualOutput ?? lastShot.ExpectedOutput:F1}g out",
                $"• Time: {lastShot.ActualTime ?? lastShot.ExpectedTime:F0} seconds",
                $"• Grind: {lastShot.GrindSetting}",
                $"• Drink: {lastShot.DrinkType}"
            };

            if (lastShot.Rating.HasValue)
            {
                var rating = lastShot.Rating.Value - 1; // Convert from 1-5 to 0-4 scale
                details.Add($"• Rating: {rating}/4");
            }

            if (lastShot.Bean != null)
            {
                details.Add($"• Bean: {lastShot.Bean.Name}" + 
                    (lastShot.Bean.Roaster != null ? $" by {lastShot.Bean.Roaster}" : ""));
            }

            if (lastShot.Machine != null)
            {
                details.Add($"• Machine: {lastShot.Machine.Name}");
            }

            if (lastShot.Grinder != null)
            {
                details.Add($"• Grinder: {lastShot.Grinder.Name}");
            }

            if (lastShot.MadeBy != null)
            {
                details.Add($"• Made by: {lastShot.MadeBy.Name}");
            }

            if (lastShot.MadeFor != null)
            {
                details.Add($"• Made for: {lastShot.MadeFor.Name}");
            }

            if (!string.IsNullOrEmpty(lastShot.TastingNotes))
            {
                details.Add($"• Notes: {lastShot.TastingNotes}");
            }

            _logger.LogInformation("Retrieved last shot details via voice, ID: {ShotId}", lastShot.Id);
            return string.Join("\n", details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting last shot via voice");
            return "Sorry, I couldn't retrieve that information. Please try again.";
        }
    }

    [Description("Finds and summarizes shots matching specified criteria (bean, rating, person, time period)")]
    private async Task<string> FindShotsToolAsync(
        [Description("Filter by bean name")] string? beanName = null,
        [Description("Filter by who made the shot")] string? madeBy = null,
        [Description("Filter by who the shot was made for")] string? madeFor = null,
        [Description("Filter by minimum rating (0-4)")] int? minRating = null,
        [Description("Filter by period: 'today', 'yesterday', 'this week', 'this month'")] string? period = null,
        [Description("How many shots to return (default 5, max 10)")] int? limit = null)
    {
        _logger.LogInformation("FindShots tool called: bean={Bean}, madeBy={MadeBy}, madeFor={MadeFor}, minRating={MinRating}, period={Period}, limit={Limit}",
            beanName, madeBy, madeFor, minRating, period, limit);

        try
        {
            var actualLimit = Math.Min(limit ?? 5, 10);
            
            // Get shots from service (page 0, 100 items to have enough to filter)
            var pagedResult = await _shotService.GetShotHistoryAsync(0, 100);
            var allShots = pagedResult?.Items;
            
            if (allShots == null || allShots.Count == 0)
            {
                return "No shots found in your history.";
            }

            var filtered = allShots.AsEnumerable();

            // Apply bean filter
            if (!string.IsNullOrEmpty(beanName))
            {
                filtered = filtered.Where(s => 
                    s.Bean?.Name?.Contains(beanName, StringComparison.OrdinalIgnoreCase) == true ||
                    s.Bag?.BeanName?.Contains(beanName, StringComparison.OrdinalIgnoreCase) == true);
            }

            // Apply made by filter
            if (!string.IsNullOrEmpty(madeBy))
            {
                filtered = filtered.Where(s => 
                    s.MadeBy?.Name?.Contains(madeBy, StringComparison.OrdinalIgnoreCase) == true);
            }

            // Apply made for filter
            if (!string.IsNullOrEmpty(madeFor))
            {
                filtered = filtered.Where(s => 
                    s.MadeFor?.Name?.Contains(madeFor, StringComparison.OrdinalIgnoreCase) == true);
            }

            // Apply rating filter (convert from 0-4 to 1-5 for comparison)
            if (minRating.HasValue)
            {
                var minRatingService = minRating.Value + 1;
                filtered = filtered.Where(s => s.Rating.HasValue && s.Rating.Value >= minRatingService);
            }

            // Apply period filter
            if (!string.IsNullOrEmpty(period))
            {
                var now = DateTime.Now;
                var startDate = period.ToLowerInvariant() switch
                {
                    "today" => now.Date,
                    "yesterday" => now.Date.AddDays(-1),
                    "this week" => now.Date.AddDays(-(int)now.DayOfWeek),
                    "this month" => new DateTime(now.Year, now.Month, 1),
                    _ => DateTime.MinValue
                };

                if (startDate != DateTime.MinValue)
                {
                    var endDate = period.ToLowerInvariant() == "yesterday" 
                        ? now.Date 
                        : DateTime.MaxValue;
                    
                    filtered = filtered.Where(s => s.Timestamp >= startDate && s.Timestamp < endDate);
                }
            }

            var results = filtered.Take(actualLimit).ToList();

            if (results.Count == 0)
            {
                var filterDesc = BuildFilterDescription(beanName, madeBy, madeFor, minRating, period);
                return $"No shots found matching: {filterDesc}";
            }

            // Build summary
            var summary = new List<string>();
            var filterDescription = BuildFilterDescription(beanName, madeBy, madeFor, minRating, period);
            summary.Add($"Found {results.Count} shot{(results.Count != 1 ? "s" : "")}" + 
                (filterDescription != "all" ? $" matching {filterDescription}" : "") + ":");

            foreach (var shot in results)
            {
                var rating = shot.Rating.HasValue ? $" ({shot.Rating.Value - 1}/4)" : "";
                var beanInfo = shot.Bean?.Name ?? shot.Bag?.BeanName ?? "unknown bean";
                summary.Add($"• {shot.Timestamp:MMM d}: {shot.DoseIn:F1}g→{shot.ActualOutput ?? shot.ExpectedOutput:F1}g, {shot.ActualTime ?? shot.ExpectedTime:F0}s{rating} [{beanInfo}]");
            }

            _logger.LogInformation("Found {Count} shots via voice query", results.Count);
            return string.Join("\n", summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding shots via voice");
            return "Sorry, I couldn't search for shots. Please try again.";
        }
    }

    private static string BuildFilterDescription(string? beanName, string? madeBy, string? madeFor, int? minRating, string? period)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(beanName)) parts.Add($"{beanName} bean");
        if (!string.IsNullOrEmpty(madeBy)) parts.Add($"made by {madeBy}");
        if (!string.IsNullOrEmpty(madeFor)) parts.Add($"made for {madeFor}");
        if (minRating.HasValue) parts.Add($"{minRating}+ rating");
        if (!string.IsNullOrEmpty(period)) parts.Add(period);
        return parts.Count > 0 ? string.Join(", ", parts) : "all";
    }

    #endregion
}
