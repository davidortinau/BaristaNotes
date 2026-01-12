# Voice Commands Service Contract

**Date**: 2026-01-09  
**Feature Branch**: `001-voice-commands`

## Overview

This document defines the internal service contracts for the voice commands feature. Since this is a mobile app (not a web API), these contracts define the C# interfaces and method signatures rather than REST endpoints.

## Service Interfaces

### IVoiceCommandService

Primary service for handling voice command processing.

```csharp
/// <summary>
/// Processes voice commands and executes corresponding app actions.
/// Voice commands are page-agnostic - all capabilities are available from any page.
/// </summary>
public interface IVoiceCommandService
{
    /// <summary>
    /// Processes a speech transcript and determines the intent and parameters.
    /// </summary>
    /// <param name="request">The voice command request with transcript.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Interpreted command with intent and parameters.</returns>
    Task<VoiceCommandResponseDto> InterpretCommandAsync(
        VoiceCommandRequestDto request, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Executes a confirmed voice command.
    /// After execution, notifies IDataChangeNotifier to trigger UI refresh on affected pages.
    /// </summary>
    /// <param name="intent">The command intent to execute.</param>
    /// <param name="parameters">Parameters for the command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the command execution.</returns>
    Task<VoiceToolResultDto> ExecuteCommandAsync(
        CommandIntent intent, 
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets available tools/capabilities as descriptions for the user.
    /// All tools are always available regardless of current page.
    /// </summary>
    /// <returns>List of available voice commands.</returns>
    IReadOnlyList<VoiceCapabilityInfo> GetAvailableCapabilities();
}
```

### IDataChangeNotifier

Notifies pages when data changes occur (from voice commands or other sources).

```csharp
/// <summary>
/// Broadcasts data change events to subscribed pages.
/// Pages subscribe to refresh their UI when relevant data changes.
/// </summary>
public interface IDataChangeNotifier
{
    /// <summary>
    /// Event raised when data changes occur.
    /// </summary>
    event EventHandler<DataChangedEventArgs>? DataChanged;
    
    /// <summary>
    /// Notifies all subscribers that data has changed.
    /// Called by VoiceCommandService after executing commands that modify data.
    /// </summary>
    /// <param name="changeType">Type of data that changed.</param>
    /// <param name="entity">Optional - the created/modified entity.</param>
    void NotifyDataChanged(DataChangeType changeType, object? entity = null);
}
```

### ISpeechRecognitionService

Wrapper around CommunityToolkit.Maui SpeechToText for testability.

```csharp
/// <summary>
/// Handles speech-to-text recognition using on-device processing.
/// </summary>
public interface ISpeechRecognitionService
{
    /// <summary>
    /// Gets the current recognition state.
    /// </summary>
    SpeechRecognitionState CurrentState { get; }
    
    /// <summary>
    /// Event raised when partial recognition results are available.
    /// </summary>
    event EventHandler<string>? PartialResultReceived;
    
    /// <summary>
    /// Event raised when final recognition is complete.
    /// </summary>
    event EventHandler<SpeechRecognitionResult>? RecognitionCompleted;
    
    /// <summary>
    /// Event raised when recognition state changes.
    /// </summary>
    event EventHandler<SpeechRecognitionState>? StateChanged;
    
    /// <summary>
    /// Requests microphone and speech recognition permissions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if permissions granted.</returns>
    Task<bool> RequestPermissionsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Starts listening for speech input.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartListeningAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Stops listening and returns final result.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StopListeningAsync(CancellationToken cancellationToken = default);
}
```

## Enums

### SpeechRecognitionState

```csharp
public enum SpeechRecognitionState
{
    Idle,
    RequestingPermission,
    Listening,
    Processing,
    Error
}
```

## DTOs

### VoiceCommandRequestDto

```csharp
/// <summary>
/// Request to interpret a voice command.
/// </summary>
public record VoiceCommandRequestDto(
    /// <summary>Raw transcript from speech recognition.</summary>
    string Transcript,
    
    /// <summary>Recognition confidence (0.0-1.0).</summary>
    double Confidence,
    
    /// <summary>Currently active bag ID for context.</summary>
    Guid? ActiveBagId = null,
    
    /// <summary>Currently active equipment ID for context.</summary>
    Guid? ActiveEquipmentId = null,
    
    /// <summary>Currently active user ID for context.</summary>
    Guid? ActiveUserId = null
);
```

### VoiceCommandResponseDto

```csharp
/// <summary>
/// Response from voice command interpretation.
/// </summary>
public record VoiceCommandResponseDto(
    /// <summary>Identified intent from the command.</summary>
    CommandIntent Intent,
    
    /// <summary>Extracted parameters for the command.</summary>
    Dictionary<string, object> Parameters,
    
    /// <summary>Human-readable confirmation message.</summary>
    string ConfirmationMessage,
    
    /// <summary>Whether user confirmation is required before execution.</summary>
    bool RequiresConfirmation,
    
    /// <summary>Error message if interpretation failed.</summary>
    string? ErrorMessage = null
);
```

### VoiceToolResultDto

```csharp
/// <summary>
/// Result from executing a voice command.
/// </summary>
public record VoiceToolResultDto(
    /// <summary>Whether execution succeeded.</summary>
    bool Success,
    
    /// <summary>Human-readable result message.</summary>
    string Message,
    
    /// <summary>The created/modified entity if applicable.</summary>
    object? CreatedEntity = null,
    
    /// <summary>ID of created/modified entity.</summary>
    Guid? EntityId = null
);
```

### SpeechRecognitionResult

```csharp
/// <summary>
/// Result from speech recognition.
/// </summary>
public record SpeechRecognitionResult(
    /// <summary>Recognized text.</summary>
    string Text,
    
    /// <summary>Recognition confidence (0.0-1.0).</summary>
    double Confidence,
    
    /// <summary>Whether recognition was successful.</summary>
    bool IsSuccessful,
    
    /// <summary>Error if recognition failed.</summary>
    Exception? Error = null
);
```

### VoiceCapabilityInfo

```csharp
/// <summary>
/// Information about an available voice capability.
/// </summary>
public record VoiceCapabilityInfo(
    /// <summary>Name of the capability.</summary>
    string Name,
    
    /// <summary>Description for the user.</summary>
    string Description,
    
    /// <summary>Example phrases that trigger this capability.</summary>
    IReadOnlyList<string> ExamplePhrases
);
```

## AI Tool Definitions

These are the tool functions exposed to the AI via `AIFunctionFactory.Create()`:

### LogShot

```csharp
[Description("Logs a new espresso shot with the specified parameters")]
public async Task<string> LogShot(
    [Description("Coffee dose in grams")] double doseGrams,
    [Description("Coffee output/yield in grams")] double outputGrams,
    [Description("Extraction time in seconds")] int timeSeconds,
    [Description("Shot rating from 0-4 (0=terrible, 4=excellent)")] int? rating = null,
    [Description("Tasting notes describing the shot")] string? tastingNotes = null,
    [Description("Preinfusion time in seconds")] int? preinfusionSeconds = null
);
```

### AddBean

```csharp
[Description("Creates a new coffee bean entry")]
public async Task<string> AddBean(
    [Description("Name of the coffee bean")] string name,
    [Description("Roaster company name")] string? roaster = null,
    [Description("Origin country or region")] string? origin = null,
    [Description("Tasting notes describing the bean")] string? tastingNotes = null
);
```

### AddBag

```csharp
[Description("Creates a new bag of an existing coffee bean")]
public async Task<string> AddBag(
    [Description("Name of the bean this bag is for")] string beanName,
    [Description("Roast date in ISO format (YYYY-MM-DD) or relative like 'today', 'yesterday'")] string? roastDate = null
);
```

### RateLastShot

```csharp
[Description("Rates the most recently logged shot")]
public async Task<string> RateLastShot(
    [Description("Rating from 0-4 (0=terrible, 4=excellent)")] int rating
);
```

### AddTastingNotes

```csharp
[Description("Adds tasting notes to the most recently logged shot")]
public async Task<string> AddTastingNotes(
    [Description("Tasting notes to add")] string notes
);
```

### AddEquipment

```csharp
[Description("Creates new coffee equipment (machine or grinder)")]
public async Task<string> AddEquipment(
    [Description("Name of the equipment")] string name,
    [Description("Type: 'machine', 'grinder', or 'other'")] string type,
    [Description("Additional notes about the equipment")] string? notes = null
);
```

### AddProfile

```csharp
[Description("Creates a new user profile for tracking who made/received shots")]
public async Task<string> AddProfile(
    [Description("Name of the person")] string name
);
```

### NavigateTo

```csharp
[Description("Navigates to a specific page in the app")]
public async Task<string> NavigateTo(
    [Description("Page name: 'beans', 'equipment', 'profiles', 'activity', 'settings', 'home'")] string pageName
);
```

### GetShotCount

```csharp
[Description("Gets the count of shots for a time period")]
public async Task<string> GetShotCount(
    [Description("Time period: 'today', 'yesterday', 'week', 'month', 'all'")] string period = "today"
);
```

### FilterShots

```csharp
[Description("Shows shots filtered by criteria")]
public async Task<string> FilterShots(
    [Description("Bean name to filter by")] string? beanName = null,
    [Description("Equipment name to filter by")] string? equipmentName = null,
    [Description("Person name to filter by")] string? personName = null,
    [Description("Time period: 'today', 'yesterday', 'week', 'month'")] string? period = null
);
```

## Error Handling

All service methods should return structured errors:

| Error Type | HTTP-Equivalent | Description |
|------------|-----------------|-------------|
| `ValidationException` | 400 | Invalid parameters |
| `EntityNotFoundException` | 404 | Referenced entity not found |
| `PermissionDeniedException` | 403 | Microphone/speech permission denied |
| `ServiceUnavailableException` | 503 | Speech recognition or AI unavailable |
| `TimeoutException` | 408 | Recognition or AI request timed out |

## Usage Flow

```
┌──────────────┐     ┌─────────────────────┐     ┌────────────────────┐
│ UI Component │────▶│ISpeechRecognition   │────▶│ Platform Speech-   │
│ (ToolbarItem)│     │Service              │     │ ToText (on-device) │
└──────────────┘     └─────────┬───────────┘     └────────────────────┘
                               │
                               │ Transcript
                               ▼
                     ┌─────────────────────┐
                     │IVoiceCommandService │
                     │.InterpretCommandAsync│
                     └─────────┬───────────┘
                               │
                               │ Via IChatClient + Tools
                               ▼
                     ┌─────────────────────┐
                     │ AI Model (Cloud)    │
                     │ with Tool Calling   │
                     └─────────┬───────────┘
                               │
                               │ Tool Call + Parameters
                               ▼
                     ┌─────────────────────┐
                     │IVoiceCommandService │
                     │.ExecuteCommandAsync │
                     └─────────┬───────────┘
                               │
                               │ Calls existing services
                               ▼
                     ┌─────────────────────┐
                     │ IShotService,       │
                     │ IBeanService, etc.  │
                     └─────────┬───────────┘
                               │
                               │ After data modification
                               ▼
                     ┌─────────────────────┐
                     │ IDataChangeNotifier │
                     │ .NotifyDataChanged  │
                     └─────────┬───────────┘
                               │
                               │ Event broadcast
                               ▼
                     ┌─────────────────────┐
                     │ Subscribed Pages    │
                     │ refresh their UI    │
                     └─────────────────────┘
```

## Cross-Page Voice Command Design

### Key Principle: Voice Commands Are Page-Agnostic

Voice commands can execute ANY app capability regardless of which page the user is currently viewing.

**Example Scenario**:
- User is on `ShotLoggingPage` (which has a bag picker but NO "Add Bean" button)
- User says: "Add a new bean called Ethiopia Yirgacheffe from Counter Culture"
- Voice command:
  1. Calls `IBeanService.CreateAsync()` to create the bean
  2. Calls `IDataChangeNotifier.NotifyDataChanged(BeanCreated, newBean)`
  3. `ShotLoggingPage` receives event, calls `RefreshBagsAndBeansAsync()`
  4. Bag picker now shows the new bean
  5. User can immediately select it for their shot

### IDataChangeNotifier Contract

```csharp
/// <summary>
/// Broadcasts data change events to all subscribed pages.
/// Implemented as a singleton to ensure all pages receive notifications.
/// </summary>
public interface IDataChangeNotifier
{
    event EventHandler<DataChangedEventArgs>? DataChanged;
    void NotifyDataChanged(DataChangeType changeType, object? entity = null);
}

public class DataChangedEventArgs : EventArgs
{
    public DataChangeType ChangeType { get; }
    public object? Entity { get; }
    
    public DataChangedEventArgs(DataChangeType changeType, object? entity = null)
    {
        ChangeType = changeType;
        Entity = entity;
    }
}

public enum DataChangeType
{
    BeanCreated,
    BeanUpdated,
    BagCreated,
    BagUpdated,
    ShotCreated,
    ShotUpdated,
    ShotDeleted,
    EquipmentCreated,
    EquipmentUpdated,
    ProfileCreated,
    ProfileUpdated
}
```

### Page Subscription Pattern

```csharp
// In ShotLoggingPage (and other pages)
partial class ShotLoggingPage : Component<ShotLoggingState, ShotLoggingPageProps>
{
    [Inject]
    IDataChangeNotifier _dataChangeNotifier;

    protected override void OnMounted()
    {
        base.OnMounted();
        _dataChangeNotifier.DataChanged += OnDataChanged;
        // ... existing code
    }

    protected override void OnWillUnmount()
    {
        _dataChangeNotifier.DataChanged -= OnDataChanged;
        // ... existing code
    }

    private void OnDataChanged(object? sender, DataChangedEventArgs e)
    {
        // Refresh UI based on what changed
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            switch (e.ChangeType)
            {
                case DataChangeType.BeanCreated:
                case DataChangeType.BagCreated:
                    await RefreshBagsAndBeansAsync();
                    break;
                case DataChangeType.EquipmentCreated:
                    await RefreshEquipmentAsync();
                    break;
                case DataChangeType.ProfileCreated:
                    await RefreshUsersAsync();
                    break;
            }
        });
    }

    private async Task RefreshBagsAndBeansAsync()
    {
        var bags = await _bagService.GetActiveBagsForShotLoggingAsync();
        var beans = await _beanService.GetAllActiveBeansAsync();
        SetState(s =>
        {
            s.AvailableBags = bags;
            s.AvailableBeans = beans;
        });
    }
}
```

### Tool Implementation with Data Change Notification

```csharp
// In VoiceCommandService
[Description("Creates a new coffee bean entry")]
private async Task<string> AddBeanTool(
    [Description("Name of the coffee bean")] string name,
    [Description("Roaster company name")] string? roaster = null)
{
    _logger.LogInformation("AddBean tool called: {Name} from {Roaster}", name, roaster);
    
    // 1. Create the bean
    var bean = await _beanService.CreateAsync(new CreateBeanDto(name, roaster));
    
    // 2. Notify all subscribed pages that a bean was created
    _dataChangeNotifier.NotifyDataChanged(DataChangeType.BeanCreated, bean);
    
    return $"Added bean: {name}" + (roaster != null ? $" from {roaster}" : "");
}
```
