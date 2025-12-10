# Service Contracts: AI Shot Improvement Advice

**Feature**: 001-ai-shot-advice  
**Date**: 2025-12-09

## IAIAdviceService

Primary service for AI advice functionality.

```csharp
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
}
```

## IShotService (Extended)

Add method to existing interface.

```csharp
// Add to existing IShotService interface
namespace BaristaNotes.Core.Services;

public interface IShotService
{
    // ... existing methods ...

    /// <summary>
    /// Gets shot context data formatted for AI analysis.
    /// Includes current shot, historical shots for same bag, and bean info.
    /// </summary>
    /// <param name="shotId">The shot to get context for.</param>
    /// <returns>AI advice request context.</returns>
    Task<AIAdviceRequestDto?> GetShotContextForAIAsync(int shotId);
}
```

## Usage Patterns

### Explicit Advice Request (P1)

```csharp
// In ShotDetailPage
async Task RequestAdviceAsync()
{
    if (!await _aiAdviceService.IsConfiguredAsync())
    {
        // API key is app-provided - show error if not configured
        await _feedbackService.ShowErrorAsync("AI advice is not available", 
            "Please update the app or contact support.");
        return;
    }

    SetState(s => s.IsLoadingAdvice = true);
    
    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var response = await _aiAdviceService.GetAdviceForShotAsync(
            Props.ShotId, 
            cts.Token);

        SetState(s => 
        {
            s.IsLoadingAdvice = false;
            s.AdviceResponse = response;
        });
    }
    catch (OperationCanceledException)
    {
        await _feedbackService.ShowErrorAsync("Request timed out");
        SetState(s => s.IsLoadingAdvice = false);
    }
    catch (Exception ex)
    {
        await _feedbackService.ShowErrorAsync("Failed to get advice", ex.Message);
        SetState(s => s.IsLoadingAdvice = false);
    }
}
```

### Passive Insight After Logging (P2)

```csharp
// In ShotLoggingPage after save
async Task SaveShotAsync()
{
    var shot = await _shotService.CreateShotAsync(createDto);
    await _feedbackService.ShowSuccessAsync("Shot logged");

    // Fire-and-forget passive insight check
    _ = CheckForPassiveInsightAsync(shot.Id);
}

async Task CheckForPassiveInsightAsync(int shotId)
{
    try
    {
        var insight = await _aiAdviceService.GetPassiveInsightAsync(shotId);
        if (!string.IsNullOrEmpty(insight))
        {
            SetState(s => s.PassiveInsight = insight);
        }
    }
    catch
    {
        // Silently fail - passive insights are non-critical
    }
}
```

## Error Contracts

### AIAdviceResponseDto Error States

| Scenario | Success | ErrorMessage |
|----------|---------|--------------|
| Advice generated | true | null |
| API key not configured | false | "AI advice is temporarily unavailable. Please try again later." |
| Network error | false | "Unable to connect. Please check your internet connection." |
| Timeout | false | "Request timed out. Please try again." |
| Rate limited | false | "Too many requests. Please wait a moment." |
| API error | false | "AI service error. Please try again later." |
| Unknown error | false | "An unexpected error occurred. Please try again." |
