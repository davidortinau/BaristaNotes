# Quickstart: AI Bean Recommendations

**Feature**: 001-ai-bean-recommendations  
**Date**: 2024-12-24

## Overview

This feature adds automatic AI-powered extraction parameter recommendations when users select beans in the shot logging page. Recommendations include dose, grind setting, output weight, and extraction duration.

## Key Patterns

### 1. Extending AIAdviceService

Add the new method to `IAIAdviceService`:

```csharp
// In IAIAdviceService.cs
Task<AIRecommendationDto> GetRecommendationsForBeanAsync(
    int beanId, 
    CancellationToken cancellationToken = default);
```

Implementation follows existing fallback pattern:

```csharp
// In AIAdviceService.cs
public async Task<AIRecommendationDto> GetRecommendationsForBeanAsync(
    int beanId, 
    CancellationToken cancellationToken = default)
{
    var context = await _shotService.GetBeanRecommendationContextAsync(beanId);
    if (context == null)
        return new AIRecommendationDto { Success = false, ErrorMessage = "Bean not found" };

    var prompt = context.HasHistory 
        ? AIPromptBuilder.BuildReturningBeanPrompt(context)
        : AIPromptBuilder.BuildNewBeanPrompt(context);

    // Use existing TryGetResponseWithFallbackAsync pattern
    var (success, response, source) = await TryGetResponseWithFallbackAsync(
        prompt, cancellationToken);

    if (!success)
        return new AIRecommendationDto { Success = false, ErrorMessage = response };

    return ParseRecommendationResponse(response, context.HasHistory, source);
}
```

### 2. Bean Selection Handler

Modify bag picker handler in `ShotLoggingPage.cs`:

```csharp
private CancellationTokenSource? _recommendationCts;

// In bag picker OnSelectedIndexChanged
OnSelectedIndexChanged(async idx =>
{
    if (idx >= 0 && idx < State.AvailableBags.Count)
    {
        var bag = State.AvailableBags[idx];
        SetState(s => { s.SelectedBagIndex = idx; s.SelectedBagId = bag.Id; });
        
        // Check if we need AI recommendations
        var beanId = bag.BeanId;
        var mostRecentBeanId = await _shotService.GetMostRecentBeanIdAsync();
        var hasHistory = await _shotService.BeanHasHistoryAsync(beanId);
        
        if (!hasHistory || beanId != mostRecentBeanId)
        {
            await RequestAIRecommendationsAsync(beanId);
        }
        else
        {
            await LoadBestShotSettingsAsync(bag.Id);
        }
    }
})
```

### 3. AI Recommendation Request

```csharp
private async Task RequestAIRecommendationsAsync(int beanId)
{
    // Cancel any pending request
    _recommendationCts?.Cancel();
    _recommendationCts = new CancellationTokenSource();
    
    SetState(s => s.IsLoadingAdvice = true);
    
    try
    {
        var result = await _aiAdviceService.GetRecommendationsForBeanAsync(
            beanId, _recommendationCts.Token);
        
        if (result.Success)
        {
            SetState(s =>
            {
                s.DoseIn = result.Dose;
                s.GrindSetting = result.GrindSetting;
                s.ExpectedOutput = result.Output;
                s.ExpectedTime = result.Duration;
            });
            
            var message = result.RecommendationType == RecommendationType.NewBean
                ? $"We didn't have any shots for this bean, so we've created a recommended starting point: {result.Dose}g dose, {result.GrindSetting} grind, {result.Output}g output, {result.Duration}s."
                : $"I see you're switching beans, so here's a recommended starting point: {result.Dose}g dose, {result.GrindSetting} grind, {result.Output}g output, {result.Duration}s.";
            
            await _feedbackService.ShowInfoAsync(message);
        }
        else
        {
            await _feedbackService.ShowErrorAsync(
                "Couldn't get AI recommendations. Enter values manually or try again.");
        }
    }
    catch (OperationCanceledException)
    {
        // Request was cancelled, ignore
    }
    finally
    {
        SetState(s => s.IsLoadingAdvice = false);
    }
}
```

### 4. AI Prompt Templates

For new beans (no history):

```text
You are an espresso extraction expert. Based on the following bean characteristics, 
recommend starting extraction parameters.

Bean: {BeanName}
Roaster: {Roaster}
Origin: {Origin}
Roast Date: {RoastDate} ({DaysFromRoast} days ago)
Flavor Notes: {Notes}
Equipment: {MachineName}, {GrinderName}

Provide recommendations in this exact JSON format:
{
  "dose": <number in grams>,
  "grind": "<grinder setting as string>",
  "output": <number in grams>,
  "duration": <number in seconds>
}

Use standard espresso ratios (1:2 to 1:2.5) and typical extraction times (25-35s).
Adjust for roast level and freshness. Darker roasts typically need coarser grind.
```

For returning beans (with history):

```text
You are an espresso extraction expert. Based on the user's shot history with this bean,
recommend optimal extraction parameters.

Bean: {BeanName}
Historical Shots (best rated first):
{foreach shot in HistoricalShots}
- Dose: {shot.DoseIn}g, Grind: {shot.GrindSetting}, Output: {shot.ActualOutput}g, 
  Time: {shot.ActualTime}s, Rating: {shot.Rating}/4
{/foreach}

Current Equipment: {MachineName}, {GrinderName}

Analyze the patterns in their successful shots and recommend parameters.
Provide recommendations in this exact JSON format:
{
  "dose": <number in grams>,
  "grind": "<grinder setting as string>",
  "output": <number in grams>,
  "duration": <number in seconds>
}
```

## Testing Approach

### Unit Tests

```csharp
[Fact]
public async Task GetRecommendationsForBeanAsync_NewBean_ReturnsNewBeanType()
{
    // Arrange
    var beanId = 1;
    _mockShotService.Setup(s => s.GetBeanRecommendationContextAsync(beanId))
        .ReturnsAsync(new BeanRecommendationContextDto 
        { 
            BeanId = beanId, 
            BeanName = "Test Bean",
            HasHistory = false 
        });
    
    // Act
    var result = await _sut.GetRecommendationsForBeanAsync(beanId);
    
    // Assert
    result.Success.Should().BeTrue();
    result.RecommendationType.Should().Be(RecommendationType.NewBean);
}
```

### Integration Tests

```csharp
[Fact]
public async Task ToastMessage_NewBean_ContainsAllParameters()
{
    // Verify toast message format includes dose, grind, output, duration
}

[Fact]
public async Task BeanSelection_CancelsPreviousRequest_OnRapidSwitch()
{
    // Verify cancellation behavior
}
```

## Files to Modify

| File | Changes |
|------|---------|
| `BaristaNotes/Services/IAIAdviceService.cs` | Add `GetRecommendationsForBeanAsync` method |
| `BaristaNotes/Services/AIAdviceService.cs` | Implement bean recommendation logic |
| `BaristaNotes.Core/Services/IShotService.cs` | Add `GetBeanRecommendationContextAsync`, `GetMostRecentBeanIdAsync`, `BeanHasHistoryAsync` |
| `BaristaNotes.Core/Services/ShotService.cs` | Implement context building methods |
| `BaristaNotes/DTOs/AIRecommendationDto.cs` | Create new DTO |
| `BaristaNotes/DTOs/RecommendationType.cs` | Create enum |
| `BaristaNotes/DTOs/BeanRecommendationContextDto.cs` | Create context DTO |
| `BaristaNotes/Pages/ShotLoggingPage.cs` | Modify bag picker handler |
| `BaristaNotes/Helpers/AIPromptBuilder.cs` | Add prompt templates |
| `BaristaNotes.Tests/Services/AIAdviceServiceTests.cs` | Add recommendation tests |

## Common Pitfalls

1. **Don't forget cancellation**: Always cancel pending requests when user switches beans rapidly
2. **Reuse loading state**: Use existing `IsLoadingAdvice` state, not a new field
3. **Parse AI response carefully**: AI may return invalid JSON; handle gracefully
4. **Respect timeouts**: Match existing 10-second timeout pattern
