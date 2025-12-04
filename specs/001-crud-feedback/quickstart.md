# Quickstart: CRUD Operation Visual Feedback

**Feature**: 001-crud-feedback  
**Date**: 2025-12-02  
**Estimated Setup Time**: 15 minutes

## Prerequisites

- BaristaNotes project already set up with .NET MAUI and MauiReactor
- Visual Studio or VS Code with C# Dev Kit
- Basic understanding of MVU (Model-View-Update) pattern
- Familiarity with Reactor fluent syntax

## Installation

### Step 1: Install Required NuGet Packages

```bash
cd BaristaNotes
dotnet add package System.Reactive --version 6.0.0
```

> **Note**: `System.Reactive` provides `IObservable<T>` and `Subject<T>` for reactive feedback streams.

### Step 2: Verify Existing Dependencies

Ensure these packages are already installed (they should be from the base project):

```bash
dotnet list package | grep "MauiReactor\|CommunityToolkit"
```

Expected output:
- `MauiReactor` (preview version)
- `CommunityToolkit.Maui`

## Project Structure

After implementation, your project structure will include:

```
BaristaNotes/
├── Models/
│   ├── FeedbackMessage.cs          # NEW: Feedback message model
│   ├── FeedbackType.cs              # NEW: Success/Error/Info/Warning enum
│   └── OperationResult.cs           # NEW: Generic result wrapper
├── Services/
│   ├── FeedbackService.cs           # NEW: Centralized feedback service
│   ├── BeanService.cs               # MODIFIED: Returns OperationResult<T>
│   ├── EquipmentService.cs          # MODIFIED: Returns OperationResult<T>
│   ├── ProfileService.cs            # MODIFIED: Returns OperationResult<T>
│   └── ShotService.cs               # MODIFIED: Returns OperationResult<T>
├── Components/
│   ├── Feedback/
│   │   ├── FeedbackOverlay.cs       # NEW: Top-level feedback renderer
│   │   ├── ToastComponent.cs        # NEW: Individual toast message
│   │   └── LoadingOverlay.cs        # NEW: Loading spinner overlay
│   ├── Beans/                       # MODIFIED: Uses FeedbackService
│   ├── Equipment/                   # MODIFIED: Uses FeedbackService
│   └── Shots/                       # MODIFIED: Uses FeedbackService
└── MauiProgram.cs                   # MODIFIED: Register FeedbackService
```

## Quick Example: Adding Feedback to a Service

### Before (without feedback)

```csharp
// BeanService.cs
public async Task<Bean> CreateBeanAsync(Bean bean)
{
    var result = await _dbContext.Beans.AddAsync(bean);
    await _dbContext.SaveChangesAsync();
    return result.Entity;
}

// BeanForm.cs (Component)
private async Task SaveBeanAsync()
{
    try
    {
        var savedBean = await _beanService.CreateBeanAsync(_newBean);
        // No feedback, user doesn't know if it worked
        NavigateBack();
    }
    catch (Exception ex)
    {
        // Silent failure, or generic error
    }
}
```

### After (with feedback)

```csharp
// BeanService.cs
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

        var result = await _dbContext.Beans.AddAsync(bean);
        await _dbContext.SaveChangesAsync();
        
        return OperationResult<Bean>.Ok(result.Entity, "Bean saved successfully");
    }
    catch (DbUpdateException ex)
    {
        return OperationResult<Bean>.Fail(
            "Failed to save bean",
            "Check your internet connection and try again",
            "DB_UPDATE_FAILED"
        );
    }
}

// BeanForm.cs (Component)
private async Task SaveBeanAsync()
{
    _feedbackService.ShowLoading("Saving bean...");
    
    var result = await _beanService.CreateBeanAsync(_newBean);
    
    _feedbackService.HideLoading();
    
    if (result.Success)
    {
        _feedbackService.ShowSuccess(result.Message); // "Bean saved successfully"
        NavigateBack();
    }
    else
    {
        _feedbackService.ShowError(result.ErrorMessage!, result.RecoveryAction);
        // Stay on form, user can fix and retry
    }
}
```

## Quick Example: Using Feedback in Components

### FeedbackOverlay Component (Top-level)

Add this to your `MainPage.cs` or root layout:

```csharp
public class MainPage : Component
{
    public override VisualNode Render()
        => ContentPage(
            Grid(
                // Your existing content
                NavigationStack(...),
                
                // Add feedback overlay (highest Z-index)
                new FeedbackOverlay()
            )
        );
}
```

The `FeedbackOverlay` subscribes to `IFeedbackService` and automatically renders toasts when `Show*` methods are called.

### Individual Service Usage

```csharp
// Success
_feedbackService.ShowSuccess("Equipment deleted");

// Error with recovery
_feedbackService.ShowError(
    "Failed to sync data",
    "Check your internet connection and try again"
);

// Info
_feedbackService.ShowInfo("New features available!");

// Warning
_feedbackService.ShowWarning("Low storage space");

// Loading
_feedbackService.ShowLoading("Syncing data...");
// ... long operation ...
_feedbackService.HideLoading();
```

## Testing Your Implementation

### Manual Testing Checklist

1. **Success Feedback**
   - [ ] Save a bean → green toast appears with checkmark
   - [ ] Toast auto-dismisses after 2 seconds
   - [ ] Multiple success toasts stack vertically

2. **Error Feedback**
   - [ ] Try to save bean without name → red toast with error message
   - [ ] Error toast includes recovery action ("Please enter a name")
   - [ ] Toast persists for 5 seconds (longer than success)
   - [ ] Only one error toast visible at a time

3. **Loading State**
   - [ ] Sync data → loading overlay appears with spinner
   - [ ] UI is interactive during loading (can navigate away)
   - [ ] Loading overlay hides when operation completes

4. **Accessibility**
   - [ ] Enable VoiceOver (iOS) or TalkBack (Android)
   - [ ] Toasts are announced by screen reader
   - [ ] Error message + recovery action read sequentially
   - [ ] Touch targets are at least 48x48dp

5. **Performance**
   - [ ] Feedback appears within 100ms of action
   - [ ] Animations are smooth (60fps, no stuttering)
   - [ ] No UI blocking during feedback display

### Unit Testing Example

```csharp
// In BaristaNotes.Tests/Unit/FeedbackServiceTests.cs
[Fact]
public void ShowSuccess_PublishesSuccessMessage()
{
    // Arrange
    var service = new FeedbackService();
    FeedbackMessage? published = null;
    service.FeedbackMessages.Subscribe(msg => published = msg);

    // Act
    service.ShowSuccess("Test success");

    // Assert
    Assert.NotNull(published);
    Assert.Equal(FeedbackType.Success, published.Type);
    Assert.Equal("Test success", published.Message);
    Assert.True(published.IsVisible);
}
```

## Troubleshooting

### Feedback Not Appearing

**Problem**: Called `ShowSuccess()` but no toast appeared.

**Solution**:
1. Verify `FeedbackOverlay` is added to your main layout
2. Check `IFeedbackService` is registered in `MauiProgram.cs`:
   ```csharp
   builder.Services.AddSingleton<IFeedbackService, FeedbackService>();
   ```
3. Ensure service is injected in components:
   ```csharp
   private readonly IFeedbackService _feedbackService = Services.GetRequiredService<IFeedbackService>();
   ```

### Feedback Appearing Behind Content

**Problem**: Toast is rendered but hidden behind other UI elements.

**Solution**: Ensure `FeedbackOverlay` is the last child in your layout (highest Z-index):

```csharp
Grid(
    // Content layers (lower Z-index)
    NavigationStack(...),
    
    // Feedback overlay (highest Z-index)
    new FeedbackOverlay()
)
```

### Screen Reader Not Announcing

**Problem**: VoiceOver/TalkBack doesn't announce feedback messages.

**Solution**: Verify `SemanticProperties.Announce` is set in `ToastComponent`:

```csharp
// In ToastComponent.Render()
Label(message.Message)
    .SemanticProperties(sp => sp.Announce(message.Message))
```

### Multiple Error Messages Stacking

**Problem**: Multiple error toasts appear simultaneously (intended: queue them).

**Solution**: Implement error queueing in `FeedbackService`:

```csharp
private Queue<FeedbackMessage> _errorQueue = new();
private bool _isShowingError = false;

public void ShowError(string message, string? recoveryAction = null, int durationMs = 5000)
{
    var feedbackMessage = new FeedbackMessage { /* ... */ };
    
    if (_isShowingError)
    {
        _errorQueue.Enqueue(feedbackMessage);
    }
    else
    {
        _isShowingError = true;
        _feedbackSubject.OnNext(feedbackMessage);
        
        // After duration, show next in queue
        Task.Delay(durationMs).ContinueWith(_ => ShowNextError());
    }
}
```

## Configuration

### Customizing Durations

Default durations can be adjusted in service calls:

```csharp
// Show success for 3 seconds instead of 2
_feedbackService.ShowSuccess("Saved!", durationMs: 3000);

// Show error for 7 seconds instead of 5
_feedbackService.ShowError("Failed", "Retry", durationMs: 7000);
```

### Customizing Colors

Coffee-themed colors are defined in the theme (applied via style keys). To change colors, modify theme constants (implementation-specific, see tasks.md).

### Disabling Feedback (Testing)

For integration tests that don't need UI feedback:

```csharp
// In test setup
builder.Services.AddSingleton<IFeedbackService, NoOpFeedbackService>();

public class NoOpFeedbackService : IFeedbackService
{
    public void ShowSuccess(string message, int durationMs = 2000) { }
    public void ShowError(string message, string? recoveryAction = null, int durationMs = 5000) { }
    public void ShowInfo(string message, int durationMs = 3000) { }
    public void ShowWarning(string message, int durationMs = 3000) { }
    public void ShowLoading(string message) { }
    public void HideLoading() { }
    public IObservable<FeedbackMessage> FeedbackMessages => Observable.Never<FeedbackMessage>();
    public IObservable<(bool, string?)> LoadingState => Observable.Never<(bool, string?)>();
}
```

## Next Steps

After completing this quickstart:

1. **Implement Tests**: Write unit tests for `FeedbackService` and `OperationResult<T>` (see `tasks.md`)
2. **Update Services**: Modify all CRUD services to return `OperationResult<T>` (see `tasks.md`)
3. **Update Components**: Add `FeedbackService` calls to all CRUD operations (see `tasks.md`)
4. **Manual Testing**: Verify accessibility, performance, and visual consistency (see `spec.md` success criteria)

## Resources

- **Feature Spec**: `specs/001-crud-feedback/spec.md` (full requirements)
- **Data Model**: `specs/001-crud-feedback/data-model.md` (entity definitions)
- **Contracts**: `specs/001-crud-feedback/contracts/IFeedbackService.md` (interface documentation)
- **Tasks**: `specs/001-crud-feedback/tasks.md` (implementation checklist, created by `/speckit.tasks`)

## Support

For questions or issues:
1. Check `spec.md` edge cases and requirements
2. Review `research.md` for design decisions and rationale
3. Consult MauiReactor documentation: https://adospace.gitbook.io/mauireactor/
4. Search existing issues in BaristaNotes repo

---

**Last Updated**: 2025-12-02  
**Status**: Ready for implementation (Phase 2: Tasks generation pending)
