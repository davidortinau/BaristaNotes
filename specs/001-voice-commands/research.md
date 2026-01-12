# Research: Voice Commands Feature

**Date**: 2026-01-09  
**Feature Branch**: `001-voice-commands`  
**Status**: Complete

## Research Tasks

### 1. On-Device Speech-to-Text Solution

**Decision**: CommunityToolkit.Maui `ISpeechToText` / `IOfflineSpeechToText`

**Rationale**:
- User explicitly requested on-device processing (no audio sent to OpenAI)
- CommunityToolkit.Maui already has `SpeechToText` service built-in
- Supports both online (`ISpeechToText`) and offline (`IOfflineSpeechToText`) recognition
- Cross-platform: iOS, Android, Windows, macOS
- `OfflineSpeechToText` requires iOS 13+ or Android 33+
- Already integrated with MAUI DI patterns

**Alternatives Considered**:
- **OpenAI Whisper** (rejected): Requires sending audio to cloud, violates user's on-device requirement
- **Plugin.Maui.Audio + custom STT** (rejected): More complex, CommunityToolkit already solves this
- **ONNX Runtime with local Whisper** (rejected): Higher complexity, larger app size, CommunityToolkit is simpler

**Implementation Notes**:
```csharp
// Register in MauiProgram.cs
builder.Services.AddSingleton<ISpeechToText>(SpeechToText.Default);
// Or for offline:
builder.Services.AddSingleton<IOfflineSpeechToText>(OfflineSpeechToText.Default);

// Usage pattern
await speechToText.StartListenAsync(new SpeechToTextOptions 
{
    Culture = CultureInfo.CurrentCulture,
    ShouldReportPartialResults = true
}, cancellationToken);
```

**Required Permissions**:
- iOS: `NSSpeechRecognitionUsageDescription`, `NSMicrophoneUsageDescription` in Info.plist
- Android: `android.permission.RECORD_AUDIO` in AndroidManifest.xml

---

### 2. AI Command Interpretation via Tool Calling

**Decision**: Microsoft.Extensions.AI `IChatClient` with `AIFunctionFactory` tool calling

**Rationale**:
- User explicitly requested tool calling pattern for natural language commands
- Microsoft.Extensions.AI already in use in the codebase (`AIAdviceService`)
- `AIFunctionFactory.Create()` allows exposing app capabilities as tools
- `ChatClientBuilder.UseFunctionInvocation()` handles automatic function calling
- Clean separation: speech-to-text (on-device) → text-to-intent (cloud AI)

**Alternatives Considered**:
- **Custom NLP/regex parsing** (rejected): Brittle, doesn't handle natural language variations
- **On-device LLM via ONNX** (rejected): User only specified on-device for audio, text can go to cloud
- **Semantic Kernel** (rejected): Heavier framework, M.E.AI already in use

**Implementation Pattern**:
```csharp
// Define app capabilities as tool functions
[Description("Logs a new espresso shot with the specified parameters")]
public async Task<string> LogShot(
    [Description("Dose in grams")] double doseGrams,
    [Description("Output/yield in grams")] double outputGrams,
    [Description("Extraction time in seconds")] int timeSeconds,
    [Description("Rating from 0-4")] int? rating = null,
    [Description("Tasting notes")] string? tastingNotes = null)
{
    // Call ShotService to create shot
}

// Register tools with ChatOptions
var chatOptions = new ChatOptions
{
    Tools = [
        AIFunctionFactory.Create(LogShot),
        AIFunctionFactory.Create(AddBean),
        AIFunctionFactory.Create(AddBag),
        AIFunctionFactory.Create(RateLastShot),
        // ... more tools
    ]
};

// Build client with function invocation
IChatClient client = new ChatClientBuilder(baseClient)
    .UseFunctionInvocation()
    .Build();
```

---

### 3. Voice Activation Trigger

**Decision**: ToolbarItem on ShotLoggingPage (primary), iPhone Action Button (stretch goal)

**Rationale**:
- User specified ToolbarItem as primary trigger
- iPhone Action Button is explicitly marked as stretch goal by user
- ToolbarItem pattern already exists in codebase

**Primary Implementation**:
```csharp
ToolbarItem()
    .IconImageSource(MaterialSymbolsFont.Mic)
    .Order(ToolbarItemOrder.Primary)
    .OnClicked(OnVoiceActivated)
```

**Stretch Goal - iPhone Action Button**:
- Requires iOS 17.2+ with Action Button hardware
- Uses `UIAction` API with `UIScene.OpenExternalURLOptionsKey`
- App must register custom URL scheme
- Investigation needed for background app state handling

---

### 4. Existing Codebase Integration Points

**Decision**: Follow established patterns from AIAdviceService and ShotLoggingPage

**Key Integration Points**:

| Component | Pattern | Location |
|-----------|---------|----------|
| Service Interface | `IVoiceCommandService` | `BaristaNotes.Core/Services/` |
| Service Implementation | `VoiceCommandService` | `BaristaNotes/Services/` |
| DI Registration | `AddScoped<>` | `MauiProgram.cs` |
| State Management | `Component<State, Props>` | Page components |
| Feedback | `IFeedbackService` | Toast notifications |
| AI Client | `IChatClient` via DI | ChatClientBuilder pattern |

**Existing Dependencies to Leverage**:
- `Microsoft.Extensions.AI` (10.1.1) - already installed
- `Microsoft.Extensions.AI.OpenAI` - already installed
- `CommunityToolkit.Maui` (9.1.1) - already installed, includes SpeechToText

---

### 5. Tool Functions to Expose

Based on spec requirements and existing services, these tools will be exposed to the AI:

| Tool Name | Description | Parameters | Maps To |
|-----------|-------------|------------|---------|
| `LogShot` | Log a new espresso shot | dose, output, time, rating?, notes? | `IShotService.CreateAsync` |
| `AddBean` | Create a new coffee bean | name, roaster, origin?, notes? | `IBeanService.CreateAsync` |
| `AddBag` | Create a bag for a bean | beanName, roastDate | `IBagService.CreateAsync` |
| `RateLastShot` | Rate the most recent shot | rating | `IShotService.UpdateAsync` |
| `AddTastingNotes` | Add notes to last shot | notes | `IShotService.UpdateAsync` |
| `AddEquipment` | Create machine/grinder | name, type | `IEquipmentService.CreateAsync` |
| `AddProfile` | Create user profile | name | `IUserProfileService.CreateAsync` |
| `NavigateTo` | Navigate to a page | pageName | Shell navigation |
| `GetShotCount` | Count shots for period | period (today/week/month) | `IShotService.GetPagedAsync` |
| `FilterShots` | Show filtered shots | bean?, equipment?, person?, dateRange? | Activity page navigation |

---

### 6. System Prompt Design

**Decision**: Use a barista-focused system prompt that understands coffee terminology

**System Prompt**:
```text
You are BaristaNotes voice assistant. Help users log espresso shots and manage their coffee data using natural language.

CONTEXT:
- Rating scale is 0-4 (0=terrible, 1=bad, 2=average, 3=good, 4=excellent)
- Common terms: dose (coffee in), yield/output (coffee out), pull time, extraction
- "Pretty good" = rating 3, "excellent/amazing" = rating 4, "not great/meh" = rating 2

RULES:
1. Always use available tools to complete actions
2. For shots, if dose/output/time not specified, ask for them
3. Use smart defaults: current bag, default equipment
4. Confirm actions with brief natural response
5. If ambiguous, ask one clarifying question
```

---

## Platform Requirements Summary

### iOS
- `Info.plist`:
  ```xml
  <key>NSSpeechRecognitionUsageDescription</key>
  <string>BaristaNotes uses speech recognition to let you log shots hands-free while brewing.</string>
  <key>NSMicrophoneUsageDescription</key>
  <string>BaristaNotes needs microphone access to hear your voice commands.</string>
  ```
- Offline STT requires iOS 13+

### Android
- `AndroidManifest.xml`:
  ```xml
  <uses-permission android:name="android.permission.RECORD_AUDIO" />
  ```
- Offline STT requires Android 33+

---

## Dependencies to Add

| Package | Version | Purpose |
|---------|---------|---------|
| `CommunityToolkit.Maui` | 9.1.1 | Already installed - SpeechToText |
| `Microsoft.Extensions.AI` | 10.1.1 | Already installed - IChatClient |

No new packages required - all dependencies are already present.

---

## Risk Assessment

| Risk | Mitigation |
|------|------------|
| Speech recognition accuracy | Use partial results for feedback, allow correction |
| AI misinterprets commands | Show what AI understood, allow cancel/retry |
| Network required for AI | Cache last successful response patterns, provide offline fallback messaging |
| Microphone permission denied | Clear permission rationale, graceful degradation to UI-only |
| Background noise interference | Visual feedback of what was heard, easy retry |

---

### 7. Cross-Page Voice Commands & UI Refresh Pattern

**Decision**: Voice commands execute independently of current page UI; data changes trigger reactive UI refresh

**Rationale**:
- User explicitly requires voice commands to work for ANY app capability, not just what the current page UI supports
- Example: On ShotLoggingPage, user can say "Add a new bean Ethiopia from Counter Culture" even though there's no "Add Bean" button on that page
- When a bean/bag is added via voice, the ShotLoggingPage's bag picker must update to show the new option

**Implementation Pattern**:

```csharp
// 1. Voice commands are NOT scoped to page UI - they can do anything
public class VoiceCommandService : IVoiceCommandService
{
    // All tools are always available regardless of current page
    private readonly AITool[] _allTools = [
        AIFunctionFactory.Create(LogShot),
        AIFunctionFactory.Create(AddBean),    // Works from ANY page
        AIFunctionFactory.Create(AddBag),     // Works from ANY page
        AIFunctionFactory.Create(RateShot),
        AIFunctionFactory.Create(AddEquipment),
        AIFunctionFactory.Create(AddProfile),
        // ... all tools always available
    ];
}

// 2. After voice command execution, notify pages to refresh
public interface IDataChangeNotifier
{
    event EventHandler<DataChangedEventArgs>? DataChanged;
    void NotifyDataChanged(DataChangeType changeType, object? entity = null);
}

public enum DataChangeType
{
    BeanCreated,
    BagCreated,
    ShotCreated,
    ShotUpdated,
    EquipmentCreated,
    ProfileCreated
}

// 3. Pages subscribe to data changes and reload relevant data
partial class ShotLoggingPage : Component<ShotLoggingState, ShotLoggingPageProps>
{
    [Inject]
    IDataChangeNotifier _dataChangeNotifier;

    protected override void OnMounted()
    {
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
        // Refresh relevant data based on what changed
        switch (e.ChangeType)
        {
            case DataChangeType.BeanCreated:
            case DataChangeType.BagCreated:
                // Reload bags and beans for picker
                _ = RefreshBagsAndBeansAsync();
                break;
            case DataChangeType.EquipmentCreated:
                _ = RefreshEquipmentAsync();
                break;
            case DataChangeType.ProfileCreated:
                _ = RefreshUsersAsync();
                break;
        }
    }
}
```

**Key Design Principles**:
1. **Voice is page-agnostic**: All voice tools are available from any page
2. **Reactive UI updates**: Pages subscribe to data change events
3. **Automatic refresh**: When voice creates/modifies data, affected pages refresh immediately
4. **No page navigation required**: User stays on current page, UI updates in place

**Existing Pattern to Follow**:
The ShotLoggingPage already reloads data in `OnPageAppearing()` - this handles new beans added from Settings. The data change notifier extends this to handle voice-triggered changes without requiring page navigation.
