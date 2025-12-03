# Data Model: CRUD Operation Visual Feedback

**Feature**: 001-crud-feedback  
**Date**: 2025-12-02

## Entities

### FeedbackMessage

Represents a user-facing notification message displayed as a toast or banner.

**Properties**:

| Property | Type | Required | Description | Validation Rules |
|----------|------|----------|-------------|------------------|
| `Id` | `Guid` | Yes | Unique identifier for the message | Auto-generated |
| `Type` | `FeedbackType` (enum) | Yes | Type of feedback (Success, Error, Info, Warning) | Must be valid enum value |
| `Message` | `string` | Yes | User-facing message text | 1-200 characters, not empty/whitespace |
| `RecoveryAction` | `string?` | No | Optional actionable recovery guidance (for errors) | Max 100 characters if provided |
| `DurationMs` | `int` | Yes | Display duration in milliseconds | Min: 1000ms, Max: 10000ms |
| `Timestamp` | `DateTimeOffset` | Yes | When the message was created | Auto-set to UTC now |
| `IsVisible` | `bool` | Yes | Whether the message is currently displayed | Default: true |

**Relationships**: None (transient entity, not persisted to database)

**State Transitions**:
```
Created (IsVisible=true) 
    → Displayed (user sees toast)
    → Dismissed (IsVisible=false, after DurationMs or user action)
```

**Business Rules**:
- Success messages default to 2000ms duration
- Error messages default to 5000ms duration (longer for reading)
- Info/Warning messages default to 3000ms duration
- Only one error message visible at a time (queue others)
- Multiple success messages can be visible simultaneously (stacked)

---

### FeedbackType (Enum)

**Values**:
- `Success = 0`: Operation completed successfully (green, checkmark icon)
- `Error = 1`: Operation failed (warm red, warning icon)
- `Info = 2`: Informational message (blue, info icon)
- `Warning = 3`: Warning message (amber, alert icon)

**Visual Mapping** (per research.md color palette):

| Type | Dark Theme Color | Light Theme Color | Icon | Semantic Meaning |
|------|------------------|-------------------|------|------------------|
| Success | `#8BC34A` | `#689F38` | ✓ Checkmark | Operation succeeded |
| Error | `#E57373` | `#C62828` | ⚠ Warning | Operation failed, needs attention |
| Info | `#64B5F6` | `#1976D2` | ℹ Info | Neutral information |
| Warning | `#FFB74D` | `#F57C00` | ⚠ Alert | Caution, non-blocking |

---

### OperationResult<T>

Generic wrapper for all CRUD operation results, providing consistent error handling.

**Type Parameters**:
- `T`: The type of data returned on success (e.g., `Bean`, `Equipment`, `Shot`)

**Properties**:

| Property | Type | Required | Description | Validation Rules |
|----------|------|----------|-------------|------------------|
| `Success` | `bool` | Yes | Whether the operation succeeded | - |
| `Data` | `T?` | No | The result data (only if Success=true) | Null if Success=false |
| `Message` | `string` | Yes | User-facing message describing the outcome | 1-200 characters |
| `ErrorMessage` | `string?` | No | Detailed error message (only if Success=false) | Max 500 characters |
| `RecoveryAction` | `string?` | No | User-actionable recovery steps (only if Success=false) | Max 100 characters |
| `ErrorCode` | `string?` | No | Machine-readable error code for logging | Max 50 characters |

**Factory Methods**:

```csharp
// Success result
public static OperationResult<T> Ok(T data, string message = "Operation completed successfully")

// Failure result
public static OperationResult<T> Fail(string errorMessage, string? recoveryAction = null, string? errorCode = null)
```

**Usage Example**:

```csharp
// Service method
public async Task<OperationResult<Bean>> CreateBeanAsync(Bean bean)
{
    try
    {
        // Validation
        if (string.IsNullOrWhiteSpace(bean.Name))
            return OperationResult<Bean>.Fail(
                "Bean name is required", 
                "Please enter a name for your coffee bean"
            );
        
        // Save
        var result = await _dbContext.Beans.AddAsync(bean);
        await _dbContext.SaveChangesAsync();
        
        return OperationResult<Bean>.Ok(result.Entity, "Bean saved successfully");
    }
    catch (DbUpdateException ex)
    {
        return OperationResult<Bean>.Fail(
            "Failed to save bean to database",
            "Check your internet connection and try again",
            "DB_UPDATE_FAILED"
        );
    }
}

// Component usage
var result = await _beanService.CreateBeanAsync(newBean);
if (result.Success)
{
    _feedbackService.ShowSuccess(result.Message);
    // Navigate or refresh list
}
else
{
    _feedbackService.ShowError(result.ErrorMessage!, result.RecoveryAction);
}
```

**Business Rules**:
- `Success=true` → `Data` must not be null, `ErrorMessage` must be null
- `Success=false` → `Data` must be null, `ErrorMessage` must be provided
- `RecoveryAction` should only be provided for recoverable errors (network, validation)
- `ErrorCode` is for logging/telemetry, not displayed to users

---

### FeedbackServiceState (Component State)

State for the `FeedbackOverlay` Reactor component that displays feedback messages.

**Properties**:

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `ActiveMessages` | `List<FeedbackMessage>` | Yes | Currently displayed messages (FIFO queue) |
| `IsLoading` | `bool` | Yes | Whether a loading overlay is currently shown |
| `LoadingMessage` | `string?` | No | Message to display in loading overlay |

**State Management**:
- Component subscribes to `IFeedbackService.FeedbackMessages` observable
- On new message: add to `ActiveMessages`, trigger re-render
- On dismiss: remove from `ActiveMessages`, trigger re-render
- Max 3 messages visible simultaneously (oldest auto-dismissed if exceeded)

---

## Entity Validation

### FeedbackMessage Validation

```csharp
public class FeedbackMessageValidator
{
    public ValidationResult Validate(FeedbackMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Message))
            return ValidationResult.Fail("Message text is required");
        
        if (message.Message.Length > 200)
            return ValidationResult.Fail("Message text must be 200 characters or less");
        
        if (message.DurationMs < 1000 || message.DurationMs > 10000)
            return ValidationResult.Fail("Duration must be between 1-10 seconds");
        
        if (message.RecoveryAction?.Length > 100)
            return ValidationResult.Fail("Recovery action must be 100 characters or less");
        
        return ValidationResult.Success();
    }
}
```

### OperationResult<T> Validation

```csharp
// Validation built into factory methods
public static OperationResult<T> Ok(T data, string message = "Operation completed successfully")
{
    if (data == null)
        throw new ArgumentNullException(nameof(data), "Success result must include data");
    
    return new OperationResult<T>
    {
        Success = true,
        Data = data,
        Message = message,
        ErrorMessage = null,
        RecoveryAction = null
    };
}

public static OperationResult<T> Fail(string errorMessage, string? recoveryAction = null, string? errorCode = null)
{
    if (string.IsNullOrWhiteSpace(errorMessage))
        throw new ArgumentException("Error message is required for failure result", nameof(errorMessage));
    
    return new OperationResult<T>
    {
        Success = false,
        Data = default,
        Message = errorMessage,
        ErrorMessage = errorMessage,
        RecoveryAction = recoveryAction,
        ErrorCode = errorCode
    };
}
```

---

## Data Flow Diagrams

### Success Flow

```
User Action (e.g., Save Bean)
    ↓
BeanService.CreateAsync(bean)
    ↓
Validate input → Save to DB → Success
    ↓
Return OperationResult<Bean>.Ok(savedBean, "Bean saved!")
    ↓
Component receives result
    ↓
FeedbackService.ShowSuccess("Bean saved!")
    ↓
FeedbackMessage created (Type=Success, DurationMs=2000)
    ↓
FeedbackOverlay subscribes, receives message
    ↓
ToastComponent renders (green, checkmark, "Bean saved!")
    ↓
After 2000ms, ToastComponent auto-dismisses
    ↓
Message removed from ActiveMessages
```

### Error Flow

```
User Action (e.g., Save Bean without name)
    ↓
BeanService.CreateAsync(bean)
    ↓
Validate input → FAIL: name is empty
    ↓
Return OperationResult<Bean>.Fail("Bean name is required", "Please enter a name")
    ↓
Component receives result
    ↓
FeedbackService.ShowError("Bean name is required", "Please enter a name")
    ↓
FeedbackMessage created (Type=Error, DurationMs=5000)
    ↓
FeedbackOverlay subscribes, receives message
    ↓
ToastComponent renders (red, warning icon, error + recovery text)
    ↓
User can dismiss early or wait 5000ms
    ↓
Message removed from ActiveMessages
```

### Loading Flow

```
User Action (e.g., Sync data)
    ↓
Component calls FeedbackService.ShowLoading("Syncing...")
    ↓
FeedbackOverlay.State.IsLoading = true, LoadingMessage = "Syncing..."
    ↓
LoadingOverlay renders (spinner + message)
    ↓
Sync operation completes
    ↓
Component calls FeedbackService.HideLoading()
    ↓
FeedbackOverlay.State.IsLoading = false
    ↓
LoadingOverlay hides
    ↓
(Optional) ShowSuccess or ShowError for sync result
```

---

## Persistence

**FeedbackMessage**: Not persisted. Transient, in-memory only.

**OperationResult<T>**: Not persisted. Wrapper for operation outcomes, exists only in method call stack.

**Why?**: Feedback is ephemeral UI state, not business data. Logging/telemetry can capture errors via `ErrorCode` if needed, but feedback messages themselves are not stored.

---

## Performance Considerations

### FeedbackMessage

- **Memory**: ~200 bytes per message, max 3 active = 600 bytes (negligible)
- **CPU**: Message creation is synchronous, <1ms
- **Rendering**: Toasts use compositor thread animations (no layout thrashing)

### OperationResult<T>

- **Memory**: Generic struct, no heap allocation for primitive types
- **CPU**: Factory methods are inline, <0.1ms overhead
- **Pattern**: Eliminates exception throwing/catching overhead (more performant than try-catch everywhere)

### FeedbackServiceState

- **Memory**: List of max 3 messages = ~600 bytes + overhead
- **Reactivity**: IObservable pattern means only subscribed components re-render
- **Thread Safety**: All mutations via Dispatcher ensure UI thread safety

---

## Accessibility Considerations

### FeedbackMessage

- **Screen Reader**: All messages announced via `SemanticProperties.Announce`
- **Timing**: 2-5s duration allows time to hear full announcement
- **Text**: Message + RecoveryAction read sequentially
- **Dismissal**: User can dismiss early via tap/swipe (accessible action)

### Visual Indicators

- **Color + Icon + Text**: Never rely on color alone (WCAG 2.1 AA)
- **Contrast**: All text meets 4.5:1 ratio, icons 3:1 ratio
- **Size**: Minimum 18sp text (exceeds 16sp recommendation)
- **Touch Targets**: 48x48dp minimum (exceeds 44x44px WCAG requirement)

---

## Testing Considerations

### Unit Tests

- `FeedbackMessage`: Validate message length limits, duration bounds, type values
- `OperationResult<T>`: Test factory methods enforce success/failure invariants
- `FeedbackServiceState`: Test message queue FIFO, max 3 messages, auto-dismiss logic

### Integration Tests

- `BeanService.CreateAsync` → verify returns `OperationResult<Bean>` with correct success/failure
- `FeedbackService.ShowSuccess` → verify message appears in observable stream
- `FeedbackOverlay` → verify subscribes to messages and renders ToastComponent

### Manual UI Tests

- Verify feedback appears within 100ms (stopwatch)
- Verify animations run at 60fps (visual inspection)
- Verify screen reader announces messages (VoiceOver/TalkBack)
- Verify color contrast meets WCAG AA (contrast checker tool)

---

## Summary

Two primary entities:

1. **FeedbackMessage**: Transient UI notification with type, message, duration
2. **OperationResult<T>**: Generic result wrapper for consistent error handling

Supporting types:

- **FeedbackType**: Enum for Success/Error/Info/Warning
- **FeedbackServiceState**: Reactor component state for active messages

All entities designed for:
- **Performance**: Minimal memory, no persistence, compositor thread animations
- **Accessibility**: Screen reader support, WCAG AA compliance, multi-modal feedback
- **Testability**: Simple validation, clear state transitions, mockable interfaces
- **Consistency**: Uniform pattern across all CRUD operations
