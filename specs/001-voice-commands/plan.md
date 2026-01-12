# Implementation Plan: Voice Commands

**Branch**: `001-voice-commands` | **Date**: 2026-01-09 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-voice-commands/spec.md`

## Summary

Enable hands-free voice control of BaristaNotes for logging espresso shots, adding beans/bags, rating shots, and managing equipment/profiles. Uses on-device speech-to-text (CommunityToolkit.Maui) for privacy, with Microsoft.Extensions.AI tool calling to interpret natural language commands and execute app actions.

## Technical Context

**Language/Version**: C# 12 / .NET 10.0  
**Primary Dependencies**: CommunityToolkit.Maui (SpeechToText), Microsoft.Extensions.AI (IChatClient, AIFunctionFactory), MauiReactor 4.0.3-beta  
**Storage**: N/A - voice data is transient, actions operate on existing SQLite entities via EF Core  
**Testing**: xUnit with FluentAssertions (existing pattern)  
**Target Platform**: iOS 13+, Android 33+ (for offline STT), macOS  
**Project Type**: Mobile (.NET MAUI)  
**Performance Goals**: Speech recognition starts <500ms, command execution <2s  
**Constraints**: On-device STT (no audio to cloud), AI text processing via OpenAI  
**Scale/Scope**: Single user, 10 voice tool functions

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

All features must demonstrate alignment with constitutional principles:

- [x] **Code Quality Standards**: Design enables single-responsibility services (ISpeechRecognitionService, IVoiceCommandService). Tool functions are isolated, testable units. Follows existing service patterns.
- [x] **Test-First Development**: Test scenarios defined in quickstart.md. Unit tests for tool parameter extraction, integration tests for service interactions. 80% coverage target.
- [x] **User Experience Consistency**: Uses MaterialSymbolsFont.Mic icon (no emoji). Visual feedback for listening state. Error messages are user-friendly. Follows MauiReactor patterns.
- [x] **Performance Requirements**: Speech recognition <500ms start, command execution <2s. On-device STT minimizes latency. Monitoring via ILogger.
- [x] **Technology Stack Consistency**: Uses MauiReactor (not XAML), CommunityToolkit.Maui (existing), Microsoft.Extensions.AI (existing pattern from AIAdviceService).
- [x] **Rating Scale Standard**: Tool function enforces 0-4 rating scale with clear descriptions.

**Violations requiring justification**: None - all principles can be met.

## Project Structure

### Documentation (this feature)

```text
specs/001-voice-commands/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Phase 0 - technology decisions
├── data-model.md        # Phase 1 - DTOs and enums (no DB changes)
├── quickstart.md        # Phase 1 - implementation guide
├── contracts/           # Phase 1 - service interfaces
│   └── voice-service-contract.md
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Phase 2 output (via /speckit.tasks)
```

### Source Code (repository root)

```text
BaristaNotes/
├── BaristaNotes.Core/
│   ├── DTOs/
│   │   └── VoiceCommandDtos.cs           # NEW: Command/response DTOs
│   └── Services/
│       ├── IVoiceCommandService.cs        # NEW: AI command interpretation
│       ├── ISpeechRecognitionService.cs   # NEW: Speech-to-text wrapper
│       └── IDataChangeNotifier.cs         # NEW: Cross-page data change events
│
├── BaristaNotes/
│   ├── Services/
│   │   ├── VoiceCommandService.cs         # NEW: Tool calling implementation
│   │   ├── SpeechRecognitionService.cs    # NEW: CommunityToolkit wrapper
│   │   └── DataChangeNotifier.cs          # NEW: Broadcasts data changes to pages
│   ├── Pages/
│   │   └── ShotLoggingPage.cs             # MODIFIED: Add voice toolbar item + data change subscription
│   ├── Platforms/
│   │   ├── iOS/Info.plist                 # MODIFIED: Speech permissions
│   │   └── Android/AndroidManifest.xml    # MODIFIED: Audio permission
│   └── MauiProgram.cs                     # MODIFIED: Register voice services + DataChangeNotifier
│
└── BaristaNotes.Tests/
    └── Services/
        ├── VoiceCommandServiceTests.cs    # NEW: Tool function tests
        ├── SpeechRecognitionServiceTests.cs # NEW: State transition tests
        └── DataChangeNotifierTests.cs     # NEW: Event broadcast tests
```

**Structure Decision**: Follows existing mobile app structure. New services follow established interface/implementation pattern. **IDataChangeNotifier** is a new singleton service that enables cross-page communication - voice commands can modify data from any page and the current page will refresh to reflect changes.

## Complexity Tracking

No constitutional violations. Design follows established patterns.

## Key Technical Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Speech-to-Text | CommunityToolkit.Maui ISpeechToText | On-device (user requirement), already installed, cross-platform |
| AI Integration | Microsoft.Extensions.AI + AIFunctionFactory | Tool calling pattern, already in codebase, clean abstraction |
| Command Execution | Direct tool invocation via UseFunctionInvocation() | AI handles parameter extraction and function dispatch |
| Voice Trigger | ToolbarItem on ShotLoggingPage | User specified, simple to implement, follows existing patterns |
| Voice Overlay | Modal overlay on active page | Provides clear visual feedback without navigation |
| Cross-Page Data Refresh | IDataChangeNotifier singleton | Voice commands work from any page; pages subscribe to refresh when data changes |

## Cross-Page Voice Command Design

**Key Requirement**: Voice commands are NOT limited to the current page's UI capabilities.

**Example**: User is on ShotLoggingPage (which has no "Add Bean" button) and says:
> "Add a new bean called Ethiopia Yirgacheffe from Counter Culture"

**Flow**:
1. Voice command executes `AddBeanTool` → calls `IBeanService.CreateAsync()`
2. Tool notifies `IDataChangeNotifier.NotifyDataChanged(BeanCreated, newBean)`
3. `ShotLoggingPage` (subscribed) receives event
4. Page calls `RefreshBagsAndBeansAsync()` to reload pickers
5. User sees new bean in the bag picker immediately

## Phase Outputs

- [x] **Phase 0**: [research.md](./research.md) - Technology decisions resolved
- [x] **Phase 1**: [data-model.md](./data-model.md) - DTOs and enums defined
- [x] **Phase 1**: [contracts/voice-service-contract.md](./contracts/voice-service-contract.md) - Service interfaces
- [x] **Phase 1**: [quickstart.md](./quickstart.md) - Implementation guide with code examples
- [x] **Phase 2**: [tasks.md](./tasks.md) - Implementation tasks generated

## Next Steps

Run `/speckit.implement` to begin task execution, starting with MVP (User Story 1).
