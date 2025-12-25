# Service Contracts: AI Bean Recommendations

**Feature**: 001-ai-bean-recommendations  
**Date**: 2024-12-24

## IAIAdviceService Extension

### GetRecommendationsForBeanAsync

Generates AI-powered extraction parameter recommendations for a selected bean.

**Signature**:
```csharp
Task<AIRecommendationDto> GetRecommendationsForBeanAsync(
    int beanId, 
    CancellationToken cancellationToken = default);
```

**Parameters**:
| Parameter | Type | Description |
|-----------|------|-------------|
| beanId | int | The ID of the selected bean |
| cancellationToken | CancellationToken | Optional cancellation token for request cancellation |

**Returns**: `AIRecommendationDto` containing recommended parameters or error state.

**Behavior**:
1. Query shot history for the bean
2. If no history: Build context from bean characteristics, call AI for new-bean recommendations
3. If history exists: Build context with historical shots, call AI for optimized recommendations
4. Return populated DTO with recommendation type indicator

**Error Handling**:
- AI service unavailable: Return `Success = false`, `ErrorMessage` populated
- Timeout (10s): Return `Success = false`, `ErrorMessage = "Request timed out"`
- Cancellation: Throw `OperationCanceledException`

---

## IShotService Extension

### GetBeanRecommendationContextAsync

Builds the context required for AI bean recommendations.

**Signature**:
```csharp
Task<BeanRecommendationContextDto?> GetBeanRecommendationContextAsync(int beanId);
```

**Parameters**:
| Parameter | Type | Description |
|-----------|------|-------------|
| beanId | int | The ID of the bean |

**Returns**: `BeanRecommendationContextDto` or null if bean not found.

**Behavior**:
1. Load bean entity with characteristics
2. Query shot history (up to 10 best-rated shots across all bags)
3. Load most recent bag for roast date
4. Load current equipment if available
5. Return populated context DTO

---

### GetMostRecentBeanIdAsync

Gets the bean ID of the most recently logged shot.

**Signature**:
```csharp
Task<int?> GetMostRecentBeanIdAsync();
```

**Returns**: Bean ID of most recent shot, or null if no shots exist.

**Behavior**:
1. Query most recent ShotRecord by Timestamp
2. Navigate to Bag → BeanId
3. Return BeanId or null

---

## DTO Definitions

### AIRecommendationDto

```csharp
public class AIRecommendationDto
{
    public bool Success { get; init; }
    public decimal Dose { get; init; }
    public string GrindSetting { get; init; } = string.Empty;
    public decimal Output { get; init; }
    public decimal Duration { get; init; }
    public RecommendationType RecommendationType { get; init; }
    public string? Confidence { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Source { get; init; }
}
```

### RecommendationType

```csharp
public enum RecommendationType
{
    NewBean,
    ReturningBean
}
```

### BeanRecommendationContextDto

```csharp
public class BeanRecommendationContextDto
{
    public int BeanId { get; init; }
    public string BeanName { get; init; } = string.Empty;
    public string? Roaster { get; init; }
    public string? Origin { get; init; }
    public string? Notes { get; init; }
    public DateTime? RoastDate { get; init; }
    public int? DaysFromRoast { get; init; }
    public bool HasHistory { get; init; }
    public List<ShotContextDto>? HistoricalShots { get; init; }
    public EquipmentContextDto? Equipment { get; init; }
}
```

---

## Integration Contract: ShotLoggingPage

### Bean Selection Handler

**Trigger**: Bag picker `OnSelectedIndexChanged` event

**Flow**:
```
1. Get selected bag's BeanId
2. Call IShotService.GetMostRecentBeanIdAsync()
3. If selectedBeanId != mostRecentBeanId OR bean has no history:
   a. Cancel any pending recommendation request
   b. Set IsLoadingRecommendations = true
   c. Call IAIAdviceService.GetRecommendationsForBeanAsync(beanId, cancellationToken)
   d. On success: Populate dose, grind, output, duration fields
   e. Show appropriate toast via IFeedbackService.ShowInfoAsync()
   f. Set IsLoadingRecommendations = false
4. Else: Use existing LoadBestShotSettingsAsync behavior
```

**State Management**:
| State | Type | Description |
|-------|------|-------------|
| IsLoadingRecommendations | bool | Controls loading bar visibility |
| RecommendationCts | CancellationTokenSource | For cancellation on re-selection |

---

## Toast Message Contracts

### New Bean Toast

**Trigger**: `RecommendationType == NewBean` and `Success == true`

**Message Format**:
```
We didn't have any shots for this bean, so we've created a recommended starting point: {Dose}g dose, {GrindSetting} grind, {Output}g output, {Duration}s.
```

### Returning Bean Toast

**Trigger**: `RecommendationType == ReturningBean` and `Success == true`

**Message Format**:
```
I see you're switching beans, so here's a recommended starting point: {Dose}g dose, {GrindSetting} grind, {Output}g output, {Duration}s.
```

### Error Toast

**Trigger**: `Success == false`

**Message Format**:
```
Couldn't get AI recommendations. Enter values manually or try again.
```
