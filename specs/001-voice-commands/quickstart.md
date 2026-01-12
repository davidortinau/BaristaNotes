# Quickstart: Voice Commands Implementation

**Date**: 2026-01-09  
**Feature Branch**: `001-voice-commands`  
**Estimated Effort**: 3-5 days

## Prerequisites

All dependencies are already installed in the project:
- ✅ `CommunityToolkit.Maui` 9.1.1 (SpeechToText)
- ✅ `Microsoft.Extensions.AI` 10.1.1 (IChatClient, tool calling)
- ✅ `Microsoft.Extensions.AI.OpenAI` (OpenAI client)

## Architecture Overview

```
┌────────────────────────────────────────────────────────────────┐
│                        UI Layer                                 │
│  ┌──────────────────┐    ┌─────────────────────────────────┐   │
│  │ ShotLoggingPage  │    │    VoiceInputOverlay            │   │
│  │ (ToolbarItem)    │───▶│ (Listening indicator, transcript)│   │
│  └────────┬─────────┘    └─────────────────────────────────┘   │
│           │                                                     │
│           │ subscribes to IDataChangeNotifier                   │
│           │ to refresh pickers when voice adds data             │
│           ▼                                                     │
└────────────────────────────────────────────────────────────────┘
                               │
                               ▼
┌────────────────────────────────────────────────────────────────┐
│                      Service Layer                              │
│  ┌─────────────────────┐    ┌─────────────────────────────┐    │
│  │ISpeechRecognition   │    │  IVoiceCommandService       │    │
│  │Service              │───▶│  (AI interpretation +       │    │
│  │(CommunityToolkit)   │    │   tool execution)           │    │
│  └─────────────────────┘    └──────────────┬──────────────┘    │
│                                            │                    │
│                                            │ notifies           │
│                                            ▼                    │
│                             ┌─────────────────────────────┐    │
│                             │  IDataChangeNotifier        │    │
│                             │  (broadcasts to all pages)  │    │
│                             └─────────────────────────────┘    │
└────────────────────────────────────────────────────────────────┘
                               │
                               ▼
┌────────────────────────────────────────────────────────────────┐
│                     AI Layer                                    │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  IChatClient + AIFunctionFactory                        │   │
│  │  (Tool Calling: LogShot, AddBean, RateShot, etc.)       │   │
│  │  ** ALL TOOLS AVAILABLE FROM ANY PAGE **                │   │
│  └─────────────────────────────────────────────────────────┘   │
└────────────────────────────────────────────────────────────────┘
                               │
                               ▼
┌────────────────────────────────────────────────────────────────┐
│                   Existing Services                             │
│  IShotService │ IBeanService │ IBagService │ IEquipmentService │
└────────────────────────────────────────────────────────────────┘
```

## Key Design: Cross-Page Voice Commands

**Voice commands are page-agnostic.** Users can execute ANY app capability via voice regardless of which page they're viewing.

**Example**: User is on ShotLoggingPage (no "Add Bean" button) and says:
> "Add a new bean called Ethiopia Yirgacheffe from Counter Culture"

**Result**:
1. Voice command creates the bean via `IBeanService`
2. `IDataChangeNotifier` broadcasts `BeanCreated` event
3. `ShotLoggingPage` receives event, refreshes bag picker
4. New bean immediately appears in picker

## Implementation Steps

### Step 1: Platform Configuration

**iOS - Info.plist**:
```xml
<key>NSSpeechRecognitionUsageDescription</key>
<string>BaristaNotes uses speech recognition to let you log shots hands-free while brewing.</string>
<key>NSMicrophoneUsageDescription</key>
<string>BaristaNotes needs microphone access to hear your voice commands.</string>
```

**Android - AndroidManifest.xml**:
```xml
<uses-permission android:name="android.permission.RECORD_AUDIO" />
```

### Step 2: Create Data Change Notifier

**File**: `BaristaNotes.Core/Services/IDataChangeNotifier.cs`

```csharp
namespace BaristaNotes.Core.Services;

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
    BeanCreated, BeanUpdated,
    BagCreated, BagUpdated,
    ShotCreated, ShotUpdated, ShotDeleted,
    EquipmentCreated, EquipmentUpdated,
    ProfileCreated, ProfileUpdated
}
```

**File**: `BaristaNotes/Services/DataChangeNotifier.cs`

```csharp
using BaristaNotes.Core.Services;

namespace BaristaNotes.Services;

public class DataChangeNotifier : IDataChangeNotifier
{
    public event EventHandler<DataChangedEventArgs>? DataChanged;
    
    public void NotifyDataChanged(DataChangeType changeType, object? entity = null)
    {
        DataChanged?.Invoke(this, new DataChangedEventArgs(changeType, entity));
    }
}
```

### Step 3: Create Service Interfaces

**File**: `BaristaNotes.Core/Services/IVoiceCommandService.cs`

```csharp
using BaristaNotes.Core.DTOs;

namespace BaristaNotes.Core.Services;

public interface IVoiceCommandService
{
    Task<VoiceCommandResponseDto> InterpretCommandAsync(
        VoiceCommandRequestDto request, 
        CancellationToken cancellationToken = default);
    
    Task<VoiceToolResultDto> ExecuteCommandAsync(
        CommandIntent intent, 
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default);
}
```

**File**: `BaristaNotes.Core/Services/ISpeechRecognitionService.cs`

```csharp
namespace BaristaNotes.Core.Services;

public interface ISpeechRecognitionService
{
    SpeechRecognitionState CurrentState { get; }
    event EventHandler<string>? PartialResultReceived;
    event EventHandler<SpeechRecognitionResult>? RecognitionCompleted;
    event EventHandler<SpeechRecognitionState>? StateChanged;
    
    Task<bool> RequestPermissionsAsync(CancellationToken cancellationToken = default);
    Task StartListeningAsync(CancellationToken cancellationToken = default);
    Task StopListeningAsync(CancellationToken cancellationToken = default);
}
```

### Step 3: Create DTOs

**File**: `BaristaNotes.Core/DTOs/VoiceCommandDtos.cs`

```csharp
namespace BaristaNotes.Core.DTOs;

public enum CommandIntent
{
    Unknown, LogShot, AddBean, AddBag, RateShot, 
    AddTastingNotes, AddEquipment, AddProfile, 
    Navigate, Query, Cancel, Help
}

public enum CommandStatus
{
    Listening, Processing, AwaitingConfirmation, 
    Executing, Completed, Failed, Cancelled
}

public record VoiceCommandRequestDto(
    string Transcript,
    double Confidence,
    Guid? ActiveBagId = null,
    Guid? ActiveEquipmentId = null,
    Guid? ActiveUserId = null);

public record VoiceCommandResponseDto(
    CommandIntent Intent,
    Dictionary<string, object> Parameters,
    string ConfirmationMessage,
    bool RequiresConfirmation,
    string? ErrorMessage = null);

public record VoiceToolResultDto(
    bool Success,
    string Message,
    object? CreatedEntity = null,
    Guid? EntityId = null);

public record SpeechRecognitionResult(
    string Text,
    double Confidence,
    bool IsSuccessful,
    Exception? Error = null);

public enum SpeechRecognitionState
{
    Idle, RequestingPermission, Listening, Processing, Error
}
```

### Step 4: Implement Speech Recognition Service

**File**: `BaristaNotes/Services/SpeechRecognitionService.cs`

```csharp
using System.Globalization;
using CommunityToolkit.Maui.Media;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.Services;

public class SpeechRecognitionService : ISpeechRecognitionService, IDisposable
{
    private readonly ISpeechToText _speechToText;
    private readonly ILogger<SpeechRecognitionService> _logger;
    private SpeechRecognitionState _currentState = SpeechRecognitionState.Idle;

    public SpeechRecognitionState CurrentState => _currentState;
    public event EventHandler<string>? PartialResultReceived;
    public event EventHandler<SpeechRecognitionResult>? RecognitionCompleted;
    public event EventHandler<SpeechRecognitionState>? StateChanged;

    public SpeechRecognitionService(
        ISpeechToText speechToText,
        ILogger<SpeechRecognitionService> logger)
    {
        _speechToText = speechToText;
        _logger = logger;
        
        _speechToText.RecognitionResultUpdated += OnRecognitionUpdated;
        _speechToText.RecognitionResultCompleted += OnRecognitionCompleted;
    }

    public async Task<bool> RequestPermissionsAsync(CancellationToken cancellationToken = default)
    {
        SetState(SpeechRecognitionState.RequestingPermission);
        var granted = await _speechToText.RequestPermissions(cancellationToken);
        SetState(granted ? SpeechRecognitionState.Idle : SpeechRecognitionState.Error);
        return granted;
    }

    public async Task StartListeningAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting speech recognition");
        SetState(SpeechRecognitionState.Listening);
        
        await _speechToText.StartListenAsync(new SpeechToTextOptions
        {
            Culture = CultureInfo.CurrentCulture,
            ShouldReportPartialResults = true
        }, cancellationToken);
    }

    public async Task StopListeningAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Stopping speech recognition");
        SetState(SpeechRecognitionState.Processing);
        await _speechToText.StopListenAsync(cancellationToken);
    }

    private void OnRecognitionUpdated(object? sender, SpeechToTextRecognitionResultUpdatedEventArgs e)
    {
        PartialResultReceived?.Invoke(this, e.RecognitionResult);
    }

    private void OnRecognitionCompleted(object? sender, SpeechToTextRecognitionResultCompletedEventArgs e)
    {
        var result = new SpeechRecognitionResult(
            e.RecognitionResult,
            1.0, // CommunityToolkit doesn't provide confidence
            !string.IsNullOrWhiteSpace(e.RecognitionResult));
        
        SetState(SpeechRecognitionState.Idle);
        RecognitionCompleted?.Invoke(this, result);
    }

    private void SetState(SpeechRecognitionState state)
    {
        _currentState = state;
        StateChanged?.Invoke(this, state);
    }

    public void Dispose()
    {
        _speechToText.RecognitionResultUpdated -= OnRecognitionUpdated;
        _speechToText.RecognitionResultCompleted -= OnRecognitionCompleted;
    }
}
```

### Step 5: Implement Voice Command Service with Tool Calling

**File**: `BaristaNotes/Services/VoiceCommandService.cs`

```csharp
using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.DTOs;

namespace BaristaNotes.Services;

public class VoiceCommandService : IVoiceCommandService
{
    private readonly IChatClient _chatClient;
    private readonly IShotService _shotService;
    private readonly IBeanService _beanService;
    private readonly IBagService _bagService;
    private readonly IEquipmentService _equipmentService;
    private readonly IUserProfileService _userProfileService;
    private readonly ILogger<VoiceCommandService> _logger;
    private readonly ChatOptions _chatOptions;

    private const string SystemPrompt = """
        You are BaristaNotes voice assistant. Help users log espresso shots and manage their coffee data.

        CONTEXT:
        - Rating scale is 0-4 (0=terrible, 1=bad, 2=average, 3=good, 4=excellent)
        - Common terms: dose (coffee in), yield/output (coffee out), pull time, extraction
        - "Pretty good" = rating 3, "excellent/amazing" = rating 4, "not great/meh" = rating 2

        RULES:
        1. Always use available tools to complete actions
        2. For shots, dose/output/time are required - ask if not provided
        3. Use smart defaults: current bag, default equipment
        4. Confirm actions with brief natural response
        5. If ambiguous, ask one clarifying question
        """;

    public VoiceCommandService(
        IChatClient chatClient,
        IShotService shotService,
        IBeanService beanService,
        IBagService bagService,
        IEquipmentService equipmentService,
        IUserProfileService userProfileService,
        ILogger<VoiceCommandService> logger)
    {
        _chatClient = chatClient;
        _shotService = shotService;
        _beanService = beanService;
        _bagService = bagService;
        _equipmentService = equipmentService;
        _userProfileService = userProfileService;
        _logger = logger;

        // Create tools from methods
        _chatOptions = new ChatOptions
        {
            Tools = 
            [
                AIFunctionFactory.Create(LogShotTool),
                AIFunctionFactory.Create(AddBeanTool),
                AIFunctionFactory.Create(AddBagTool),
                AIFunctionFactory.Create(RateLastShotTool),
                AIFunctionFactory.Create(AddEquipmentTool),
                AIFunctionFactory.Create(AddProfileTool),
                AIFunctionFactory.Create(GetShotCountTool),
            ]
        };
    }

    public async Task<VoiceCommandResponseDto> InterpretCommandAsync(
        VoiceCommandRequestDto request, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Interpreting command: {Transcript}", request.Transcript);

        try
        {
            var client = new ChatClientBuilder(_chatClient)
                .UseFunctionInvocation()
                .Build();

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, SystemPrompt),
                new(ChatRole.User, request.Transcript)
            };

            var response = await client.GetResponseAsync(messages, _chatOptions, cancellationToken);
            
            return new VoiceCommandResponseDto(
                CommandIntent.Unknown, // Intent determined by tool call
                new Dictionary<string, object>(),
                response.Text ?? "Command processed.",
                false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error interpreting voice command");
            return new VoiceCommandResponseDto(
                CommandIntent.Unknown,
                new Dictionary<string, object>(),
                "Sorry, I couldn't understand that. Please try again.",
                false,
                ex.Message);
        }
    }

    // Tool implementations - THESE WORK FROM ANY PAGE
    // After data modification, each tool notifies IDataChangeNotifier
    
    [Description("Logs a new espresso shot with the specified parameters")]
    private async Task<string> LogShotTool(
        [Description("Coffee dose in grams")] double doseGrams,
        [Description("Coffee output/yield in grams")] double outputGrams,
        [Description("Extraction time in seconds")] int timeSeconds,
        [Description("Shot rating from 0-4")] int? rating = null,
        [Description("Tasting notes")] string? tastingNotes = null)
    {
        _logger.LogInformation("LogShot tool called: {Dose}g in, {Output}g out, {Time}s", 
            doseGrams, outputGrams, timeSeconds);
        
        // TODO: Call _shotService.CreateAsync() with parameters
        // _dataChangeNotifier.NotifyDataChanged(DataChangeType.ShotCreated, newShot);
        
        return $"Shot logged: {doseGrams}g → {outputGrams}g in {timeSeconds}s" + 
               (rating.HasValue ? $", rated {rating}/4" : "");
    }

    [Description("Creates a new coffee bean entry")]
    private async Task<string> AddBeanTool(
        [Description("Name of the coffee bean")] string name,
        [Description("Roaster company name")] string? roaster = null)
    {
        _logger.LogInformation("AddBean tool called: {Name} from {Roaster}", name, roaster);
        
        // TODO: Call _beanService.CreateAsync() with parameters
        // _dataChangeNotifier.NotifyDataChanged(DataChangeType.BeanCreated, newBean);
        // This will trigger ShotLoggingPage to refresh its bag picker!
        
        return $"Added bean: {name}" + (roaster != null ? $" from {roaster}" : "");
    }

    [Description("Creates a new bag of an existing coffee bean")]
    private async Task<string> AddBagTool(
        [Description("Name of the bean")] string beanName,
        [Description("Roast date (YYYY-MM-DD or 'today', 'yesterday')")] string? roastDate = null)
    {
        _logger.LogInformation("AddBag tool called: {Bean}, roasted {Date}", beanName, roastDate);
        
        // TODO: Find bean by name, then call _bagService.CreateAsync()
        // _dataChangeNotifier.NotifyDataChanged(DataChangeType.BagCreated, newBag);
        // This will trigger ShotLoggingPage to refresh its bag picker!
        
        return $"Added bag of {beanName}" + (roastDate != null ? $" roasted {roastDate}" : "");
    }

    [Description("Rates the most recently logged shot")]
    private async Task<string> RateLastShotTool(
        [Description("Rating from 0-4")] int rating)
    {
        _logger.LogInformation("RateLastShot tool called: {Rating}", rating);
        
        // TODO: Get most recent shot, update rating
        // _dataChangeNotifier.NotifyDataChanged(DataChangeType.ShotUpdated, updatedShot);
        
        return $"Rated your last shot {rating}/4";
    }

    [Description("Creates new coffee equipment")]
    private async Task<string> AddEquipmentTool(
        [Description("Equipment name")] string name,
        [Description("Type: 'machine', 'grinder', or 'other'")] string type)
    {
        _logger.LogInformation("AddEquipment tool called: {Name} ({Type})", name, type);
        
        // TODO: Call _equipmentService.CreateAsync()
        // _dataChangeNotifier.NotifyDataChanged(DataChangeType.EquipmentCreated, newEquipment);
        // This will trigger ShotLoggingPage to refresh its equipment picker!
        
        return $"Added {type}: {name}";
    }

    [Description("Creates a new user profile")]
    private async Task<string> AddProfileTool(
        [Description("Person's name")] string name)
    {
        _logger.LogInformation("AddProfile tool called: {Name}", name);
        
        // TODO: Call _userProfileService.CreateAsync()
        // _dataChangeNotifier.NotifyDataChanged(DataChangeType.ProfileCreated, newProfile);
        // This will trigger ShotLoggingPage to refresh its user picker!
        
        return $"Added profile for {name}";
    }

    [Description("Gets shot count for a time period")]
    private async Task<string> GetShotCountTool(
        [Description("Period: 'today', 'week', 'month'")] string period = "today")
    {
        _logger.LogInformation("GetShotCount tool called: {Period}", period);
        // Implementation will call _shotService to count
        return $"You've pulled X shots {period}";
    }

    public Task<VoiceToolResultDto> ExecuteCommandAsync(
        CommandIntent intent, 
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        // Tool execution happens automatically via UseFunctionInvocation()
        throw new NotImplementedException("Execution handled by AI tool calling");
    }
}
```

### Step 6: Register Services in DI

**File**: `MauiProgram.cs` (add to existing)

```csharp
// Add after existing service registrations

// Data change notification (singleton - shared across all pages)
builder.Services.AddSingleton<IDataChangeNotifier, DataChangeNotifier>();

// Speech recognition
builder.Services.AddSingleton<ISpeechToText>(SpeechToText.Default);
builder.Services.AddScoped<ISpeechRecognitionService, SpeechRecognitionService>();

// Voice commands (needs IDataChangeNotifier injected)
builder.Services.AddScoped<IVoiceCommandService, VoiceCommandService>();
```

### Step 7: Add Data Change Subscription to ShotLoggingPage

**Critical**: Pages must subscribe to `IDataChangeNotifier` to refresh when voice commands modify data.

```csharp
partial class ShotLoggingPage : Component<ShotLoggingState, ShotLoggingPageProps>
{
    // ... existing [Inject] services ...
    
    [Inject]
    IDataChangeNotifier _dataChangeNotifier;
    
    [Inject]
    ISpeechRecognitionService _speechRecognitionService;
    
    [Inject]
    IVoiceCommandService _voiceCommandService;

    protected override void OnMounted()
    {
        base.OnMounted();
        
        // Subscribe to data changes from voice commands
        _dataChangeNotifier.DataChanged += OnDataChanged;
        
        // ... existing code ...
    }

    protected override void OnWillUnmount()
    {
        // Unsubscribe to prevent memory leaks
        _dataChangeNotifier.DataChanged -= OnDataChanged;
        
        // ... existing code ...
    }

    /// <summary>
    /// Handle data changes from voice commands (or other sources).
    /// Refreshes relevant pickers when new beans/bags/equipment/profiles are created.
    /// </summary>
    private void OnDataChanged(object? sender, DataChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            switch (e.ChangeType)
            {
                case DataChangeType.BeanCreated:
                case DataChangeType.BagCreated:
                    // Refresh bag picker when beans or bags are added via voice
                    await RefreshBagsAndBeansAsync();
                    break;
                    
                case DataChangeType.EquipmentCreated:
                    // Refresh equipment picker when equipment added via voice
                    await RefreshEquipmentAsync();
                    break;
                    
                case DataChangeType.ProfileCreated:
                    // Refresh user picker when profile added via voice
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

    private async Task RefreshEquipmentAsync()
    {
        var equipment = await _equipmentService.GetAllActiveEquipmentAsync();
        SetState(s => s.AvailableEquipment = equipment.ToList());
    }

    private async Task RefreshUsersAsync()
    {
        var users = await _userProfileService.GetAllProfilesAsync();
        SetState(s => s.AvailableUsers = users);
    }
}
```

### Step 8: Add Voice UI to ShotLoggingPage

Add state for voice mode:

```csharp
private class State
{
    // ... existing state ...
    public bool IsVoiceActive { get; set; }
    public string VoiceTranscript { get; set; } = "";
    public SpeechRecognitionState VoiceState { get; set; }
}
```

Add ToolbarItem and voice overlay:

```csharp
public override VisualNode Render()
{
    return ContentPage(
        // ... existing content ...
        
        // Voice overlay when active
        State.IsVoiceActive 
            ? RenderVoiceOverlay()
            : null
    )
    .ToolbarItems(
        ToolbarItem()
            .IconImageSource(new FontImageSource
            {
                FontFamily = MaterialSymbolsFont.FontFamily,
                Glyph = MaterialSymbolsFont.Mic,
                Color = State.IsVoiceActive ? Colors.Red : AppColors.Primary
            })
            .OnClicked(OnVoiceToggle)
    );
}

VisualNode RenderVoiceOverlay()
{
    return Grid(
        Border(
            VStack(
                Label(State.VoiceState == SpeechRecognitionState.Listening 
                    ? "Listening..." 
                    : "Processing...")
                    .ThemeKey(ThemeKeys.HeadingMedium),
                Label(State.VoiceTranscript)
                    .ThemeKey(ThemeKeys.BodyMedium),
                Button("Cancel")
                    .OnClicked(OnVoiceCancelled)
            )
        )
        .BackgroundColor(AppColors.Surface)
    )
    .BackgroundColor(Colors.Black.WithAlpha(0.5f));
}
```

## Testing Strategy

### Unit Tests

1. **VoiceCommandService**: Test tool parameter extraction
2. **SpeechRecognitionService**: Test state transitions
3. **DTOs**: Test serialization/validation

### Integration Tests

1. Mock `ISpeechToText` to simulate recognition
2. Mock `IChatClient` to simulate AI responses with tool calls
3. Verify correct service calls (ShotService, BeanService, etc.)

### Manual Testing Scenarios

| Scenario | Input | Expected |
|----------|-------|----------|
| Log shot | "Log shot 18 in 36 out 28 seconds 4 stars" | Shot created with values |
| Add bean | "Add Ethiopia Yirgacheffe from Counter Culture" | Bean created |
| Rate shot | "Rate my last shot 5 stars" | Last shot rating updated |
| Ambiguous | "Log shot" | AI asks for dose/output/time |

### Cross-Page Voice Command Tests (Critical)

| Test | Page | Voice Command | Expected Result |
|------|------|---------------|-----------------|
| Add bean from shot page | ShotLoggingPage | "Add bean Ethiopia from Counter Culture" | Bean created, bag picker refreshes to show it |
| Add bag from shot page | ShotLoggingPage | "Add a bag of Ethiopia roasted today" | Bag created, bag picker refreshes to show it |
| Add equipment from shot page | ShotLoggingPage | "Add grinder Niche Zero" | Equipment created, equipment picker refreshes |
| Add profile from shot page | ShotLoggingPage | "Add profile Sarah" | Profile created, user picker refreshes |

## File Structure

```
BaristaNotes/
├── BaristaNotes.Core/
│   ├── DTOs/
│   │   └── VoiceCommandDtos.cs           # NEW
│   └── Services/
│       ├── IVoiceCommandService.cs        # NEW
│       ├── ISpeechRecognitionService.cs   # NEW
│       └── IDataChangeNotifier.cs         # NEW - data change events
├── BaristaNotes/
│   ├── Services/
│   │   ├── VoiceCommandService.cs         # NEW
│   │   ├── SpeechRecognitionService.cs    # NEW
│   │   └── DataChangeNotifier.cs          # NEW - broadcasts to pages
│   ├── Pages/
│   │   └── ShotLoggingPage.cs             # MODIFIED - voice UI + data change subscription
│   ├── Platforms/
│   │   ├── iOS/Info.plist                 # MODIFIED
│   │   └── Android/AndroidManifest.xml    # MODIFIED
│   └── MauiProgram.cs                     # MODIFIED
└── BaristaNotes.Tests/
    └── Services/
        ├── VoiceCommandServiceTests.cs    # NEW
        ├── SpeechRecognitionServiceTests.cs # NEW
        └── DataChangeNotifierTests.cs     # NEW
```

## Stretch Goal: iPhone Action Button

For iOS 17.2+ devices with Action Button:

1. Register custom URL scheme in Info.plist
2. Handle URL in App.xaml.cs
3. Trigger voice mode when app opened via action button

This requires further investigation and is marked as P5 priority.
