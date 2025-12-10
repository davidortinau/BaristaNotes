# Quickstart: AI Shot Improvement Advice

**Feature**: 001-ai-shot-advice  
**Date**: 2025-12-09

## Prerequisites

1. OpenAI API key (user must obtain from https://platform.openai.com)
2. Internet connectivity for AI requests
3. At least one logged shot to analyze

## Package Dependencies

Add to `BaristaNotes.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.AI" Version="9.0.0-preview.*" />
  <PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="9.0.0-preview.*" />
</ItemGroup>
```

## Quick Setup

### 1. Register AI Service in DI

In `MauiProgram.cs`:

```csharp
// Register AI advice service
builder.Services.AddSingleton<IAIAdviceService, AIAdviceService>();

// Note: API key is loaded from IConfiguration (app-provided, not user-configured)
// For development: use appsettings.Development.json (gitignored)
// For production: key will be fetched from backend API (future enhancement)
```

### 2. Create AI Advice Service

```csharp
public class AIAdviceService : IAIAdviceService
{
    private readonly IShotService _shotService;
    private readonly IFeedbackService _feedbackService;
    private readonly string? _apiKey;
    private IChatClient? _chatClient;

    public AIAdviceService(
        IShotService shotService, 
        IFeedbackService feedbackService,
        IConfiguration configuration)
    {
        _shotService = shotService;
        _feedbackService = feedbackService;
        _apiKey = configuration["OpenAI:ApiKey"];
    }

    public Task<bool> IsConfiguredAsync()
    {
        return Task.FromResult(!string.IsNullOrEmpty(_apiKey));
    }

    public async Task<AIAdviceResponseDto> GetAdviceForShotAsync(
        int shotId, 
        CancellationToken cancellationToken = default)
    {
        var client = GetOrCreateClient();
        if (client == null)
        {
            return new AIAdviceResponseDto 
            { 
                Success = false, 
                ErrorMessage = "AI advice is temporarily unavailable. Please try again later." 
            };
        }

        var context = await _shotService.GetShotContextForAIAsync(shotId);
        if (context == null)
        {
            return new AIAdviceResponseDto 
            { 
                Success = false, 
                ErrorMessage = "Shot not found" 
            };
        }

        var prompt = BuildPrompt(context);
        
        try
        {
            var response = await client.GetResponseAsync(prompt, cancellationToken: cancellationToken);
            return new AIAdviceResponseDto
            {
                Success = true,
                Advice = response.Message.Text,
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
    }

    private IChatClient? GetOrCreateClient()
    {
        if (_chatClient != null) return _chatClient;
        if (string.IsNullOrEmpty(_apiKey)) return null;

        _chatClient = new OpenAI.Chat.ChatClient("gpt-5-nano", _apiKey)
            .AsIChatClient();
        
        return _chatClient;
    }

    private List<ChatMessage> BuildPrompt(AIAdviceRequestDto context)
    {
        var systemPrompt = @"You are an expert barista and espresso consultant. You help home baristas improve their espresso shots by analyzing their parameters and providing specific, actionable advice.

When giving advice:
1. Be specific and actionable (e.g., ""Try grinding 2 clicks finer"" not ""adjust your grind"")
2. Explain the reasoning briefly
3. Suggest one primary change at a time to isolate variables
4. Reference the user's successful shots when available
5. Consider the coffee's age since roast date";

        var userMessage = BuildUserMessage(context);

        return new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userMessage)
        };
    }

    private string BuildUserMessage(AIAdviceRequestDto context)
    {
        var shot = context.CurrentShot;
        var bean = context.BeanInfo;
        
        var sb = new StringBuilder();
        sb.AppendLine($"Current shot: {shot.DoseIn}g in, {shot.ActualOutput ?? 0}g out, {shot.ActualTime ?? 0}s");
        sb.AppendLine($"Grind setting: {shot.GrindSetting}");
        
        if (shot.Rating.HasValue)
            sb.AppendLine($"Rating: {shot.Rating}/4");
        
        if (!string.IsNullOrEmpty(shot.TastingNotes))
            sb.AppendLine($"Tasting notes: {shot.TastingNotes}");
        
        sb.AppendLine();
        sb.AppendLine($"Bean: {bean.Name}");
        if (!string.IsNullOrEmpty(bean.Roaster))
            sb.AppendLine($"Roaster: {bean.Roaster}");
        sb.AppendLine($"Days from roast: {bean.DaysFromRoast}");
        
        if (context.Equipment != null)
        {
            if (!string.IsNullOrEmpty(context.Equipment.MachineName))
                sb.AppendLine($"Machine: {context.Equipment.MachineName}");
            if (!string.IsNullOrEmpty(context.Equipment.GrinderName))
                sb.AppendLine($"Grinder: {context.Equipment.GrinderName}");
        }

        if (context.HistoricalShots?.Any() == true)
        {
            var bestShots = context.HistoricalShots
                .Where(s => s.Rating >= 3)
                .Take(3);
            
            if (bestShots.Any())
            {
                sb.AppendLine();
                sb.AppendLine("Best rated shots for this bean:");
                foreach (var best in bestShots)
                {
                    sb.AppendLine($"  - {best.DoseIn}g in, {best.ActualOutput}g out, {best.ActualTime}s (rated {best.Rating}/4)");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("Please analyze this shot and suggest how to improve the next one.");
        
        return sb.ToString();
    }
}
```

### 3. Add "Get Advice" Button to Shot Detail Page

```csharp
// In shot detail page render
Button("Get AI Advice")
    .OnClicked(async () => await RequestAdviceAsync())
    .IsEnabled(!State.IsLoadingAdvice)
    .HeightRequest(50)
```

## User Flow

```
Shot Detail Page
       |
       v
[Get AI Advice] ──> Check API Key configured?
       |                    |
       |              No -> Show "unavailable" message
       |                    
       v                    
Show Loading ───────> Build Context
       |                    |
       v                    v
Call OpenAI API <──── Build Prompt
       |
       v
Display Advice ───────> User reads suggestions
```

## Configuration Setup (Development)

For local development, create `appsettings.Development.json` (gitignored):

```json
{
  "OpenAI": {
    "ApiKey": "sk-your-api-key-here"
  }
}
```

Ensure this file is in `.gitignore` to prevent committing the key.

**Future Enhancement**: Fetch API key from a secure backend API with app authentication.

## Testing

### Unit Test: Prompt Building

```csharp
[Fact]
public void BuildPrompt_IncludesAllShotParameters()
{
    var context = new AIAdviceRequestDto
    {
        CurrentShot = new ShotContextDto
        {
            DoseIn = 18.0m,
            ActualOutput = 36.0m,
            ActualTime = 28.0m,
            GrindSetting = "5.5",
            Rating = 2
        },
        BeanInfo = new BeanContextDto
        {
            Name = "Test Bean",
            DaysFromRoast = 14
        }
    };

    var prompt = BuildUserMessage(context);

    prompt.Should().Contain("18");
    prompt.Should().Contain("36");
    prompt.Should().Contain("28");
    prompt.Should().Contain("5.5");
    prompt.Should().Contain("2/4");
}
```

### Integration Test: Mock AI Response

```csharp
[Fact]
public async Task GetAdviceForShot_ReturnsAdvice_WhenConfigured()
{
    var mockChatClient = new MockChatClient("Try grinding finer.");
    var service = new AIAdviceService(mockChatClient, _shotService);

    var result = await service.GetAdviceForShotAsync(1);

    result.Success.Should().BeTrue();
    result.Advice.Should().Contain("grind");
}
```
