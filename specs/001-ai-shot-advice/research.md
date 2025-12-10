# Research: AI Shot Improvement Advice

**Feature**: 001-ai-shot-advice  
**Date**: 2025-12-09  
**Status**: Complete

## Research Tasks

### 1. Microsoft.Extensions.AI with OpenAI Integration

**Task**: Research best practices for using Microsoft.Extensions.AI with OpenAI in .NET MAUI

**Decision**: Use `Microsoft.Extensions.AI.OpenAI` package with `IChatClient` abstraction

**Rationale**: 
- Microsoft.Extensions.AI provides a unified abstraction (`IChatClient`) that decouples the app from specific AI providers
- Easy to swap providers (OpenAI, Azure OpenAI, Ollama) with minimal code changes
- Integrates well with .NET dependency injection
- Supports streaming responses for better UX
- Built-in support for function calling if needed in future

**Implementation Pattern**:
```csharp
// Registration in MauiProgram.cs
builder.Services.AddChatClient(services =>
    new OpenAI.Chat.ChatClient("gpt-4o-mini", 
        SecureStorage.Default.GetAsync("OPENAI_API_KEY").Result)
    .AsIChatClient());

// Usage via DI
[Inject] IChatClient _chatClient;

var response = await _chatClient.GetResponseAsync(prompt);
```

**Alternatives Considered**:
- Direct OpenAI SDK: More verbose, tightly coupled to OpenAI
- Semantic Kernel: More complex, overkill for simple chat completion
- Azure OpenAI SDK: Requires Azure subscription, more setup

---

### 2. API Key Management in .NET MAUI

**Task**: Research best practices for managing app-provided OpenAI API keys in .NET MAUI mobile apps

**Decision**: Load API key from app settings/configuration (app-provided, not user-configured). Future: fetch from backend API.

**Rationale**:
- This is NOT a "bring your own key" app - the token is provided by the app
- For MVP, load from local app settings or embedded configuration
- Future enhancement: fetch key from a secure backend API with app authentication
- User should not see or configure the API key

**Implementation Pattern (MVP)**:
```csharp
// Load API key from app settings (e.g., appsettings.json or embedded resource)
// For development, use a configuration provider
public class AIAdviceService : IAIAdviceService
{
    private readonly string? _apiKey;
    
    public AIAdviceService(IConfiguration configuration)
    {
        _apiKey = configuration["OpenAI:ApiKey"];
    }
    
    public Task<bool> IsConfiguredAsync() 
        => Task.FromResult(!string.IsNullOrEmpty(_apiKey));
}
```

**Future Pattern (Backend API)**:
```csharp
// Fetch key from secure backend API
public async Task<string?> GetApiKeyAsync()
{
    // Call backend with app authentication
    var response = await _httpClient.GetAsync("/api/config/openai-key");
    return await response.Content.ReadAsStringAsync();
}
```

**Security Considerations**:
1. For MVP: Use app settings with obfuscation (not hardcoded strings)
2. Do NOT commit actual API key to source control
3. Use .gitignore for local appsettings.Development.json with real key
4. Future: Backend API provides key with proper app authentication
5. Consider rate limiting and usage monitoring on backend

**Alternatives Considered**:
- User-provided key: Rejected - poor UX, not the desired product model
- Hardcoded in source: NEVER - easily extracted, committed to git
- Azure Key Vault direct: Overkill for MVP, adds complexity

---

### 3. Model Selection: gpt-5-nano

**Task**: Validate gpt-5-nano model for espresso advice use case

**Decision**: Use `gpt-5-nano` model

**Rationale**:
- Cost-effective for conversational tasks
- Fast response times for short prompts
- Sufficient intelligence for domain-specific advice
- Supports system prompts for role specialization
- Good balance of quality vs. cost for consumer app
- Newest lightweight model from OpenAI

---

### 4. Prompt Engineering for Espresso Advice

**Task**: Research effective prompt structure for espresso extraction advice

**Decision**: Use structured system prompt with espresso expertise + user context in messages

**Prompt Structure**:
```
SYSTEM PROMPT:
You are an expert barista and espresso consultant. You help home baristas improve 
their espresso shots by analyzing their parameters and providing specific, actionable 
advice. Focus on the key variables: dose (grams in), yield (grams out), extraction 
time, and grind setting.

When giving advice:
1. Be specific and actionable (e.g., "Try grinding 2 clicks finer" not "adjust your grind")
2. Explain the reasoning (e.g., "Your shot ran fast which typically indicates under-extraction")
3. Suggest one primary change at a time to isolate variables
4. Reference the user's best shots when available
5. Consider the coffee's age since roast date

USER MESSAGE:
Current shot: {doseIn}g in, {actualOutput}g out, {actualTime}s, rated {rating}/4
Grind setting: {grindSetting}
Bean: {beanName} from {roaster}, roasted {daysAgo} days ago
Equipment: {machine}, {grinder}
{tastingNotes ? "Tasting notes: " + tastingNotes : ""}

Historical context (best shots for this bean):
{bestShotsSummary}

Please analyze this shot and suggest how to improve the next one.
```

---

### 5. Error Handling and Offline Graceful Degradation

**Task**: Research error handling patterns for external API calls in mobile apps

**Decision**: Implement timeout, retry, and graceful offline handling

**Patterns**:
```csharp
// Timeout handling
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
try 
{
    var response = await _chatClient.GetResponseAsync(prompt, cancellationToken: cts.Token);
}
catch (OperationCanceledException)
{
    await _feedbackService.ShowErrorAsync(
        "Request timed out", 
        "Please check your connection and try again");
}

// Network error handling
catch (HttpRequestException ex)
{
    await _feedbackService.ShowErrorAsync(
        "Unable to connect", 
        "AI advice requires an internet connection");
}

// API error handling (rate limit, auth failure)
catch (OpenAI.OpenAIException ex)
{
    if (ex.Message.Contains("rate_limit"))
        await _feedbackService.ShowErrorAsync("Too many requests", "Please wait a moment");
    else if (ex.Message.Contains("invalid_api_key"))
        await _feedbackService.ShowErrorAsync("Invalid API key", "Please check your settings");
}
```

---

### 6. Database Migration for TastingNotes

**Task**: Research EF Core migration approach for adding TastingNotes field

**Decision**: Standard EF Core migration with nullable string field

**Migration Steps**:
1. Add property to ShotRecord: `public string? TastingNotes { get; set; }`
2. Generate migration: `dotnet ef migrations add AddTastingNotesToShotRecord`
3. Migration will add nullable column - no data transformation needed

**No data preservation concerns**: New nullable field, all existing records will have NULL.

---

## Summary of Decisions

| Area | Decision | Package/Pattern |
|------|----------|-----------------|
| AI Integration | Microsoft.Extensions.AI abstraction | `Microsoft.Extensions.AI.OpenAI` |
| Model | gpt-5-nano | Cost-effective, fast |
| API Key Storage | SecureStorage | `Microsoft.Maui.Storage.SecureStorage` |
| Error Handling | Timeout + graceful degradation | 10s timeout, user-friendly messages |
| Prompt Strategy | System prompt + contextual user message | Espresso expertise + shot context |
| Database | EF Core migration | Nullable TastingNotes field |

## Open Questions (Resolved)

1. ~~Which AI model to use?~~ → gpt-4o-mini (user specified gpt-5-nano doesn't exist)
2. ~~How to handle API keys securely?~~ → SecureStorage with user-provided key
3. ~~How to structure prompts?~~ → System prompt for expertise, user message for context
