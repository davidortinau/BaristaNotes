# IFeedbackService Contract

**Feature**: 001-crud-feedback  
**Date**: 2025-12-02  
**Language**: C# Interface

## Overview

Centralized service interface for displaying user feedback (success, error, loading states) across all CRUD operations in the BaristaNotes app.

## Interface Definition

```csharp
using System;
using System.Reactive.Subjects;

namespace BaristaNotes.Services
{
    /// <summary>
    /// Service for displaying user feedback messages and loading states.
    /// Provides reactive stream of feedback messages for UI components to subscribe to.
    /// </summary>
    public interface IFeedbackService
    {
        /// <summary>
        /// Displays a success message to the user.
        /// </summary>
        /// <param name="message">The success message text (1-200 characters)</param>
        /// <param name="durationMs">Display duration in milliseconds (default: 2000ms)</param>
        /// <exception cref="ArgumentException">If message is null/empty or duration is out of range (1000-10000ms)</exception>
        void ShowSuccess(string message, int durationMs = 2000);

        /// <summary>
        /// Displays an error message to the user with optional recovery guidance.
        /// </summary>
        /// <param name="message">The error message text (1-200 characters)</param>
        /// <param name="recoveryAction">Optional user-actionable recovery steps (max 100 characters)</param>
        /// <param name="durationMs">Display duration in milliseconds (default: 5000ms for errors)</param>
        /// <exception cref="ArgumentException">If message is null/empty or duration is out of range (1000-10000ms)</exception>
        void ShowError(string message, string? recoveryAction = null, int durationMs = 5000);

        /// <summary>
        /// Displays an informational message to the user.
        /// </summary>
        /// <param name="message">The info message text (1-200 characters)</param>
        /// <param name="durationMs">Display duration in milliseconds (default: 3000ms)</param>
        /// <exception cref="ArgumentException">If message is null/empty or duration is out of range (1000-10000ms)</exception>
        void ShowInfo(string message, int durationMs = 3000);

        /// <summary>
        /// Displays a warning message to the user.
        /// </summary>
        /// <param name="message">The warning message text (1-200 characters)</param>
        /// <param name="durationMs">Display duration in milliseconds (default: 3000ms)</param>
        /// <exception cref="ArgumentException">If message is null/empty or duration is out of range (1000-10000ms)</exception>
        void ShowWarning(string message, int durationMs = 3000);

        /// <summary>
        /// Displays a loading overlay with a message.
        /// Only one loading overlay can be shown at a time.
        /// </summary>
        /// <param name="message">The loading message text (e.g., "Syncing data...")</param>
        /// <exception cref="ArgumentException">If message is null/empty</exception>
        void ShowLoading(string message);

        /// <summary>
        /// Hides the currently displayed loading overlay.
        /// Safe to call even if no loading overlay is shown.
        /// </summary>
        void HideLoading();

        /// <summary>
        /// Observable stream of feedback messages.
        /// UI components subscribe to this to reactively render feedback.
        /// Messages are published when Show* methods are called.
        /// </summary>
        IObservable<FeedbackMessage> FeedbackMessages { get; }

        /// <summary>
        /// Observable stream of loading state changes.
        /// Emits tuple of (isLoading, message) when ShowLoading/HideLoading are called.
        /// </summary>
        IObservable<(bool IsLoading, string? Message)> LoadingState { get; }
    }
}
```

## Data Contracts

### FeedbackMessage

```csharp
namespace BaristaNotes.Models
{
    /// <summary>
    /// Represents a user-facing feedback message.
    /// </summary>
    public class FeedbackMessage
    {
        /// <summary>
        /// Unique identifier for the message.
        /// </summary>
        public Guid Id { get; init; } = Guid.NewGuid();

        /// <summary>
        /// Type of feedback (Success, Error, Info, Warning).
        /// </summary>
        public FeedbackType Type { get; init; }

        /// <summary>
        /// User-facing message text (1-200 characters).
        /// </summary>
        public required string Message { get; init; }

        /// <summary>
        /// Optional recovery action guidance (for errors, max 100 characters).
        /// </summary>
        public string? RecoveryAction { get; init; }

        /// <summary>
        /// Display duration in milliseconds (1000-10000ms).
        /// </summary>
        public int DurationMs { get; init; }

        /// <summary>
        /// Timestamp when the message was created (UTC).
        /// </summary>
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Whether the message is currently visible.
        /// </summary>
        public bool IsVisible { get; set; } = true;
    }
}
```

### FeedbackType

```csharp
namespace BaristaNotes.Models
{
    /// <summary>
    /// Type of user feedback message.
    /// </summary>
    public enum FeedbackType
    {
        /// <summary>
        /// Success message (green, checkmark icon).
        /// </summary>
        Success = 0,

        /// <summary>
        /// Error message (warm red, warning icon).
        /// </summary>
        Error = 1,

        /// <summary>
        /// Informational message (blue, info icon).
        /// </summary>
        Info = 2,

        /// <summary>
        /// Warning message (amber, alert icon).
        /// </summary>
        Warning = 3
    }
}
```

## Usage Examples

### Example 1: Success Feedback

```csharp
// In BeanService after successful create
var result = await _dbContext.Beans.AddAsync(bean);
await _dbContext.SaveChangesAsync();

// Show success feedback
_feedbackService.ShowSuccess("Bean saved successfully");

return OperationResult<Bean>.Ok(result.Entity, "Bean saved successfully");
```

### Example 2: Error Feedback with Recovery

```csharp
// In EquipmentService on network failure
try
{
    await _syncService.SyncEquipmentAsync();
}
catch (HttpRequestException ex)
{
    _feedbackService.ShowError(
        "Failed to sync equipment",
        "Check your internet connection and try again"
    );
    
    return OperationResult<bool>.Fail(
        "Failed to sync equipment",
        "Check your internet connection and try again",
        "NETWORK_ERROR"
    );
}
```

### Example 3: Loading State

```csharp
// In ShotService during long operation
_feedbackService.ShowLoading("Loading shot history...");

try
{
    var shots = await _dbContext.Shots
        .Include(s => s.Bean)
        .Include(s => s.Equipment)
        .OrderByDescending(s => s.CreatedAt)
        .ToListAsync();
    
    return shots;
}
finally
{
    _feedbackService.HideLoading();
}
```

### Example 4: Reactive UI Subscription

```csharp
// In FeedbackOverlay Reactor component
public partial class FeedbackOverlay : Component<FeedbackOverlayState>
{
    private IDisposable? _subscription;

    protected override void OnMounted()
    {
        var feedbackService = Services.GetRequiredService<IFeedbackService>();
        
        _subscription = feedbackService.FeedbackMessages.Subscribe(message =>
        {
            SetState(s => s.ActiveMessages.Add(message));
            
            // Auto-dismiss after duration
            Task.Delay(message.DurationMs).ContinueWith(_ =>
            {
                SetState(s => s.ActiveMessages.Remove(message));
            });
        });
        
        base.OnMounted();
    }

    protected override void OnWillUnmount()
    {
        _subscription?.Dispose();
        base.OnWillUnmount();
    }
}
```

## Implementation Requirements

### Thread Safety

- All methods must be thread-safe (may be called from background threads)
- Use `Dispatcher.Dispatch` for UI thread marshalling when publishing messages
- Observable streams must be published on UI thread

### Performance

- `Show*` methods must complete in <1ms (lightweight object creation only)
- Observable publish must be non-blocking
- No database/network I/O in feedback service

### Validation

- Validate message length (1-200 characters)
- Validate duration range (1000-10000ms)
- Throw `ArgumentException` for invalid inputs (fail fast)

### Behavior

- Multiple success/info/warning messages can be shown simultaneously (stacked)
- Only one error message shown at a time (queue additional errors)
- Only one loading overlay shown at a time (subsequent ShowLoading calls replace message)
- Auto-dismiss after `DurationMs` timeout
- Users can manually dismiss messages early (tap/swipe)

## Error Handling

### Invalid Inputs

```csharp
// Message too long
_feedbackService.ShowSuccess(new string('x', 201)); 
// Throws ArgumentException: "Message must be 1-200 characters"

// Duration out of range
_feedbackService.ShowError("Error", durationMs: 500); 
// Throws ArgumentException: "Duration must be between 1000-10000ms"

// Empty message
_feedbackService.ShowSuccess(""); 
// Throws ArgumentException: "Message cannot be null or empty"
```

### Service Unavailable

```csharp
// If IFeedbackService is not registered in DI
var feedbackService = Services.GetService<IFeedbackService>();
if (feedbackService == null)
{
    // Fallback: log error, don't crash
    Debug.WriteLine("FeedbackService not registered");
    return;
}
```

## Testing Contract

### Unit Test Requirements

```csharp
[Fact]
public void ShowSuccess_PublishesMessage_WithSuccessType()
{
    // Arrange
    var service = new FeedbackService();
    FeedbackMessage? published = null;
    service.FeedbackMessages.Subscribe(msg => published = msg);

    // Act
    service.ShowSuccess("Test message");

    // Assert
    Assert.NotNull(published);
    Assert.Equal(FeedbackType.Success, published.Type);
    Assert.Equal("Test message", published.Message);
    Assert.Equal(2000, published.DurationMs);
}

[Fact]
public void ShowError_WithRecoveryAction_IncludesRecoveryInMessage()
{
    // Arrange
    var service = new FeedbackService();
    FeedbackMessage? published = null;
    service.FeedbackMessages.Subscribe(msg => published = msg);

    // Act
    service.ShowError("Failed", "Try again");

    // Assert
    Assert.NotNull(published);
    Assert.Equal(FeedbackType.Error, published.Type);
    Assert.Equal("Failed", published.Message);
    Assert.Equal("Try again", published.RecoveryAction);
}

[Fact]
public void ShowLoading_PublishesLoadingState()
{
    // Arrange
    var service = new FeedbackService();
    (bool IsLoading, string? Message)? published = null;
    service.LoadingState.Subscribe(state => published = state);

    // Act
    service.ShowLoading("Loading...");

    // Assert
    Assert.NotNull(published);
    Assert.True(published.Value.IsLoading);
    Assert.Equal("Loading...", published.Value.Message);
}

[Fact]
public void ShowSuccess_WithInvalidMessage_ThrowsArgumentException()
{
    // Arrange
    var service = new FeedbackService();

    // Act & Assert
    Assert.Throws<ArgumentException>(() => service.ShowSuccess(""));
    Assert.Throws<ArgumentException>(() => service.ShowSuccess(new string('x', 201)));
}
```

## Dependencies

**Required NuGet Packages**:
- `System.Reactive` (v6.0+) for `IObservable<T>` and `Subject<T>`

**Service Registration**:

```csharp
// In MauiProgram.cs
builder.Services.AddSingleton<IFeedbackService, FeedbackService>();
```

## Versioning

- **Version**: 1.0.0
- **Breaking Changes**: None (initial version)
- **Backward Compatibility**: N/A (new interface)

## Related Contracts

- `OperationResult<T>` (see data-model.md): Result wrapper that services return, triggering feedback calls
- Existing service interfaces: `IBeanService`, `IEquipmentService`, etc. will be updated to return `OperationResult<T>`
