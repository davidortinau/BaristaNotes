# Tasks: Voice Commands

**Input**: Design documents from `/specs/001-voice-commands/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅, quickstart.md ✅

**Tests**: Tests are included per Constitution Principle II (Test-First Development). Tests must be written FIRST, must FAIL before implementation, and require 80% minimum coverage (100% for command parsing logic).

**Quality Gates**: All tasks must pass constitution-mandated quality gates before merge: tests pass, static analysis clean, code review approved, performance baseline met, documentation updated.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

Based on plan.md structure:
- **Core**: `BaristaNotes.Core/` (interfaces, DTOs)
- **App**: `BaristaNotes/` (implementations, pages)
- **Tests**: `BaristaNotes.Tests/` (unit/integration tests)

---

## Phase 1: Setup (Platform Configuration)

**Purpose**: Platform permissions and project initialization for voice features

- [x] T001 [P] Add speech recognition permission to iOS in `BaristaNotes/Platforms/iOS/Info.plist` (NSSpeechRecognitionUsageDescription)
- [x] T002 [P] Add microphone permission to iOS in `BaristaNotes/Platforms/iOS/Info.plist` (NSMicrophoneUsageDescription)
- [x] T003 [P] Add audio recording permission to Android in `BaristaNotes/Platforms/Android/AndroidManifest.xml` (RECORD_AUDIO)
- [x] T004 Verify CommunityToolkit.Maui is properly configured in `BaristaNotes/MauiProgram.cs` (UseMauiCommunityToolkit)

---

## Phase 2: Foundational (Core Infrastructure)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### DTOs and Enums

- [x] T005 [P] Create `CommandIntent` enum in `BaristaNotes.Core/Models/Enums/CommandIntent.cs` (Unknown, LogShot, AddBean, AddBag, RateShot, AddTastingNotes, AddEquipment, AddProfile, Navigate, Query, Cancel, Help)
- [x] T006 [P] Create `CommandStatus` enum in `BaristaNotes.Core/Models/Enums/CommandStatus.cs` (Listening, Processing, AwaitingConfirmation, Executing, Completed, Failed, Cancelled)
- [x] T007 [P] Create `SpeechRecognitionState` enum in `BaristaNotes.Core/Models/Enums/SpeechRecognitionState.cs` (Idle, RequestingPermission, Listening, Processing, Error)
- [x] T008 [P] Create `DataChangeType` enum in `BaristaNotes.Core/Models/Enums/DataChangeType.cs` (BeanCreated, BagCreated, ShotCreated, ShotUpdated, EquipmentCreated, ProfileCreated, etc.)
- [x] T009 Create `VoiceCommandRequestDto` record in `BaristaNotes.Core/Services/DTOs/VoiceCommandDtos.cs` (Transcript, Confidence, ActiveBagId?, ActiveEquipmentId?, ActiveUserId?)
- [x] T010 Create `VoiceCommandResponseDto` record in `BaristaNotes.Core/Services/DTOs/VoiceCommandDtos.cs` (Intent, Parameters, ConfirmationMessage, RequiresConfirmation, ErrorMessage?)
- [x] T011 Create `VoiceToolResultDto` record in `BaristaNotes.Core/Services/DTOs/VoiceCommandDtos.cs` (Success, Message, CreatedEntity?, EntityId?)
- [x] T012 Create `SpeechRecognitionResultDto` record in `BaristaNotes.Core/Services/DTOs/VoiceCommandDtos.cs` (Text, Confidence, IsSuccessful, Error?)

### Service Interfaces

- [x] T013 [P] Create `IDataChangeNotifier` interface in `BaristaNotes.Core/Services/IDataChangeNotifier.cs` with DataChanged event and NotifyDataChanged method
- [x] T014 [P] Create `DataChangedEventArgs` class in `BaristaNotes.Core/Services/IDataChangeNotifier.cs` (ChangeType, Entity?)
- [x] T015 [P] Create `ISpeechRecognitionService` interface in `BaristaNotes.Core/Services/ISpeechRecognitionService.cs` (CurrentState, PartialResultReceived, RecognitionCompleted, StateChanged events; RequestPermissionsAsync, StartListeningAsync, StopListeningAsync methods)
- [x] T016 [P] Create `IVoiceCommandService` interface in `BaristaNotes.Core/Services/IVoiceCommandService.cs` (InterpretCommandAsync, ExecuteCommandAsync, ProcessCommandAsync methods)

### Service Implementations

- [x] T017 Implement `DataChangeNotifier` class in `BaristaNotes.Core/Services/DataChangeNotifier.cs` (singleton, broadcasts events to subscribers)
- [x] T018 Implement `SpeechRecognitionService` class in `BaristaNotes/Services/SpeechRecognitionService.cs` (wraps CommunityToolkit.Maui ISpeechToText, manages state transitions)

### Dependency Injection

- [x] T019 Register `IDataChangeNotifier` as singleton in `BaristaNotes/MauiProgram.cs`
- [x] T020 Register `ISpeechToText` from CommunityToolkit.Maui in `BaristaNotes/MauiProgram.cs`
- [x] T021 Register `ISpeechRecognitionService` as singleton in `BaristaNotes/MauiProgram.cs`

### Tests for Foundation

- [ ] T022 [P] Create unit tests for `DataChangeNotifier` in `BaristaNotes.Tests/Services/DataChangeNotifierTests.cs` (event subscription, event broadcast, multiple subscribers)
- [ ] T023 [P] Create unit tests for `SpeechRecognitionService` state transitions in `BaristaNotes.Tests/Services/SpeechRecognitionServiceTests.cs` (Idle→Listening→Processing→Idle, permission handling)

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Log Shot by Voice (Priority: P1) 🎯 MVP

**Goal**: Enable users to log espresso shots by speaking shot details (dose, output, time, rating)

**Independent Test**: Speak "Log shot 18 in 36 out 28 seconds 4 stars" and verify shot appears in activity feed with correct values

### Tests for User Story 1 (REQUIRED per Constitution) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T024 [P] [US1] Create unit tests for LogShotTool parameter extraction in `BaristaNotes.Tests/Services/VoiceCommandServiceTests.cs` (dose parsing, output parsing, time parsing, rating parsing including "pretty good" → 3)
- [ ] T025 [P] [US1] Create integration test for voice shot logging flow in `BaristaNotes.Tests/Services/VoiceCommandServiceTests.cs` (transcript → intent → tool execution → shot created)

### Implementation for User Story 1

- [x] T026 [US1] Create `VoiceCommandService` class in `BaristaNotes/Services/VoiceCommandService.cs` with constructor injection (IChatClient, IShotService, IDataChangeNotifier, ILogger)
- [x] T027 [US1] Implement `LogShotTool` method in `VoiceCommandService` with AIFunctionFactory attributes (Description for AI, parameters: doseGrams, outputGrams, timeSeconds, rating?, tastingNotes?)
- [x] T028 [US1] Implement AI system prompt in `VoiceCommandService` with coffee terminology context (rating scale 0-4, "pretty good"=3, dose/yield/time terminology)
- [x] T029 [US1] Implement `InterpretCommandAsync` method using ChatClientBuilder with UseFunctionInvocation() for automatic tool calling
- [ ] T030 [US1] Add DataChangeNotifier call in LogShotTool after shot creation (NotifyDataChanged(ShotCreated, shot)) - stub exists, needs real implementation
- [x] T031 [US1] Register `IVoiceCommandService` as scoped in `BaristaNotes/MauiProgram.cs`

### UI for User Story 1

- [x] T032 [US1] Add voice state properties to `ShotLoggingState` class in `BaristaNotes/Pages/ShotLoggingPage.cs` (IsVoiceActive, VoiceTranscript, VoiceState)
- [x] T033 [US1] Add `IDataChangeNotifier` injection to `ShotLoggingPage` and subscribe to DataChanged event in OnMounted
- [x] T034 [US1] Implement `OnDataChanged` handler in `ShotLoggingPage` to refresh pickers when data changes
- [x] T035 [US1] Add voice ToolbarItem to `ShotLoggingPage.Render()` using MaterialSymbolsFont.Mic icon
- [x] T036 [US1] Implement `OnVoiceToggle` handler to start/stop speech recognition
- [x] T037 [US1] Create `RenderVoiceOverlay()` method showing listening indicator, transcript, and cancel button
- [x] T038 [US1] Implement voice recognition event handlers (PartialResultReceived → update transcript, RecognitionCompleted → call VoiceCommandService)
- [x] T039 [US1] Add visual and audio feedback for successful shot logging (IFeedbackService.ShowSuccessAsync)

**Checkpoint**: User Story 1 complete - users can log shots by voice on ShotLoggingPage

---

## Phase 4: User Story 2 - Add Bean/Bag by Voice (Priority: P2)

**Goal**: Enable users to add new beans or bags by speaking (e.g., "Add bean Ethiopia from Counter Culture")

**Independent Test**: Speak "Add a new bean Ethiopia Yirgacheffe from Counter Culture" and verify bean appears in beans list

### Tests for User Story 2 (REQUIRED per Constitution) ⚠️

- [ ] T040 [P] [US2] Create unit tests for AddBeanTool in `BaristaNotes.Tests/Services/VoiceCommandServiceTests.cs` (name extraction, roaster extraction)
- [ ] T041 [P] [US2] Create unit tests for AddBagTool in `BaristaNotes.Tests/Services/VoiceCommandServiceTests.cs` (bean name matching, relative date parsing: "today", "yesterday")
- [ ] T042 [P] [US2] Create integration test for cross-page bean creation in `BaristaNotes.Tests/Services/VoiceCommandServiceTests.cs` (add bean via voice on ShotLoggingPage → bag picker refreshes)

### Implementation for User Story 2

- [x] T043 [US2] Implement `AddBeanTool` method in `VoiceCommandService` (name, roaster?, origin?, tastingNotes?)
- [x] T044 [US2] Add IBeanService injection to `VoiceCommandService` constructor
- [x] T045 [US2] Add DataChangeNotifier call in AddBeanTool (NotifyDataChanged(BeanCreated, bean))
- [x] T046 [US2] Implement `AddBagTool` method in `VoiceCommandService` (beanName, roastDate?)
- [x] T047 [US2] Add IBagService injection to `VoiceCommandService` constructor
- [x] T048 [US2] Implement relative date parsing helper for roast dates ("today" → today, "yesterday" → yesterday, "last Tuesday" → calculate)
- [x] T049 [US2] Add DataChangeNotifier call in AddBagTool (NotifyDataChanged(BagCreated, bag))
- [x] T050 [US2] Implement bean name matching logic in AddBagTool (find existing bean by name, case-insensitive)
- [x] T051 [US2] Add `RefreshBagsAndBeansAsync()` method to `ShotLoggingPage` for DataChanged handler

**Checkpoint**: User Story 2 complete - users can add beans/bags by voice, pickers auto-refresh

---

## Phase 5: User Story 3 - Rate and Update Shots by Voice (Priority: P3)

**Goal**: Enable users to rate or add tasting notes to the most recent shot by voice

**Independent Test**: Log a shot, then speak "Rate my last shot 5 stars" and verify rating is updated

### Tests for User Story 3 (REQUIRED per Constitution) ⚠️

- [ ] T052 [P] [US3] Create unit tests for RateLastShotTool in `BaristaNotes.Tests/Services/VoiceCommandServiceTests.cs` (rating extraction, "last shot" reference handling)
- [ ] T053 [P] [US3] Create unit tests for AddTastingNotesTool in `BaristaNotes.Tests/Services/VoiceCommandServiceTests.cs` (notes extraction, append to existing notes)

### Implementation for User Story 3

- [x] T054 [US3] Implement `RateLastShotTool` method in `VoiceCommandService` (rating)
- [x] T055 [US3] Implement logic to get most recent shot in RateLastShotTool (IShotService.GetMostRecentShotAsync)
- [x] T056 [US3] Add DataChangeNotifier call in RateLastShotTool (NotifyDataChanged(ShotUpdated, shot))
- [x] T057 [US3] Implement `AddTastingNotesTool` method in `VoiceCommandService` (notes)
- [x] T058 [US3] Add DataChangeNotifier call in AddTastingNotesTool (NotifyDataChanged(ShotUpdated, shot))

**Checkpoint**: User Story 3 complete - users can rate and add notes to shots by voice

---

## Phase 6: User Story 4 - Manage Equipment and Profiles by Voice (Priority: P4)

**Goal**: Enable users to add equipment and profiles by voice

**Independent Test**: Speak "Add a new grinder called Niche Zero" and verify equipment appears in list

### Tests for User Story 4 (REQUIRED per Constitution) ⚠️

- [ ] T059 [P] [US4] Create unit tests for AddEquipmentTool in `BaristaNotes.Tests/Services/VoiceCommandServiceTests.cs` (name extraction, type parsing: "grinder"→Grinder, "machine"→Machine)
- [ ] T060 [P] [US4] Create unit tests for AddProfileTool in `BaristaNotes.Tests/Services/VoiceCommandServiceTests.cs` (name extraction)

### Implementation for User Story 4

- [x] T061 [US4] Implement `AddEquipmentTool` method in `VoiceCommandService` (name, type)
- [x] T062 [US4] Add IEquipmentService injection to `VoiceCommandService` constructor
- [x] T063 [US4] Implement equipment type parsing in AddEquipmentTool ("grinder" → EquipmentType.Grinder, "machine" → EquipmentType.Machine)
- [x] T064 [US4] Add DataChangeNotifier call in AddEquipmentTool (NotifyDataChanged(EquipmentCreated, equipment))
- [x] T065 [US4] Implement `AddProfileTool` method in `VoiceCommandService` (name)
- [x] T066 [US4] Add IUserProfileService injection to `VoiceCommandService` constructor
- [x] T067 [US4] Add DataChangeNotifier call in AddProfileTool (NotifyDataChanged(ProfileCreated, profile))
- [x] T068 [US4] Add `RefreshEquipmentAsync()` and `RefreshUsersAsync()` methods to `ShotLoggingPage` for DataChanged handler

**Checkpoint**: User Story 4 complete - users can add equipment and profiles by voice

---

## Phase 7: User Story 5 - Query and Navigate by Voice (Priority: P5)

**Goal**: Enable users to navigate and query data by voice

**Independent Test**: Speak "Go to beans" and verify app navigates to beans page

### Tests for User Story 5 (REQUIRED per Constitution) ⚠️

- [ ] T069 [P] [US5] Create unit tests for NavigateToTool in `BaristaNotes.Tests/Services/VoiceCommandServiceTests.cs` (page name parsing: "beans", "equipment", "activity")
- [ ] T070 [P] [US5] Create unit tests for GetShotCountTool in `BaristaNotes.Tests/Services/VoiceCommandServiceTests.cs` (period parsing: "today", "week", "month")
- [ ] T071 [P] [US5] Create unit tests for FilterShotsTool in `BaristaNotes.Tests/Services/VoiceCommandServiceTests.cs` (bean filter, date range filter)

### Implementation for User Story 5

- [x] T072 [US5] Implement `NavigateToTool` method in `VoiceCommandService` (pageName)
- [x] T073 [US5] Implement navigation logic using Shell.Current.GoToAsync for page routing
- [x] T074 [US5] Implement `GetShotCountTool` method in `VoiceCommandService` (period)
- [x] T075 [US5] Implement period-based filtering logic (today → today's shots, week → last 7 days)
- [x] T076 [US5] Implement `FilterShotsTool` method in `VoiceCommandService` (beanName?, equipmentName?, period?)
- [x] T077 [US5] Implement filter navigation to ActivityFeedPage with query parameters

**Checkpoint**: User Story 5 complete - users can navigate and query by voice

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories and final quality gates

### Error Handling & Edge Cases

- [x] T078 [P] Implement microphone permission denied handling in `SpeechRecognitionService` (show user-friendly message with settings link)
- [x] T079 [P] Implement speech recognition timeout handling (return to Idle after 60s safety timeout)
- [ ] T080 [P] Implement unrecognized command handling in `VoiceCommandService` (helpful suggestions, retry option)
- [ ] T081 [P] Implement ambiguous entity handling (e.g., multiple beans with similar names → present options)

### Accessibility & UX

- [x] T082 [P] Ensure voice button meets 44x44px minimum touch target in `ShotLoggingPage` (ToolbarItem uses platform-native sizing)
- [ ] T083 [P] Add screen reader announcements for voice state changes (VoiceOver/TalkBack)
- [x] T084 [P] Add visual listening indicator (pulsing animation) during speech recognition

### Performance & Monitoring

- [x] T085 [P] Add ILogger calls throughout `VoiceCommandService` for debugging and monitoring
- [ ] T086 [P] Add timing metrics for speech recognition start latency (<500ms target)
- [ ] T087 [P] Add timing metrics for command execution latency (<2s target)

### Documentation & Cleanup

- [ ] T088 [P] Update quickstart.md with actual file paths and any implementation changes
- [ ] T089 Code cleanup and refactoring in voice services
- [ ] T090 Run static analysis and fix any warnings in new voice files
- [x] T091 Constitution compliance verification (verified rating scale 0-4, MaterialSymbolsFont icons, MauiReactor patterns)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phases 3-7)**: All depend on Foundational phase completion
  - User stories can proceed in priority order (P1 → P2 → P3 → P4 → P5)
  - Or in parallel if team capacity allows
- **Polish (Phase 8)**: Depends on User Story 1 (MVP) minimum; ideally all stories complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational - MVP, no dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational - Independent, tests cross-page refresh pattern
- **User Story 3 (P3)**: Can start after Foundational - Independent
- **User Story 4 (P4)**: Can start after Foundational - Independent
- **User Story 5 (P5)**: Can start after Foundational - Independent

### Within Each User Story

1. Tests MUST be written and FAIL before implementation
2. Tool implementation before UI integration
3. DataChangeNotifier calls for any data modifications
4. Story complete before moving to next priority (for sequential execution)

### Parallel Opportunities

- All Setup tasks (T001-T004) can run in parallel
- All DTO/Enum tasks (T005-T012) can run in parallel
- All Interface tasks (T013-T016) can run in parallel
- All Test tasks within a story marked [P] can run in parallel
- Different user stories can be worked on in parallel by different team members (after Foundational)

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together:
Task: T024 "Unit tests for LogShotTool parameter extraction"
Task: T025 "Integration test for voice shot logging flow"

# After tests fail, implement in dependency order:
# 1. Service core (T026-T031) - sequential due to dependencies
# 2. UI (T032-T039) - mostly sequential but some parallel possible
```

---

## Parallel Example: Foundational Phase

```bash
# Launch all DTOs/Enums in parallel:
Task: T005 "Create CommandIntent enum"
Task: T006 "Create CommandStatus enum"
Task: T007 "Create SpeechRecognitionState enum"
Task: T008 "Create DataChangeType enum"

# Launch all interfaces in parallel:
Task: T013 "Create IDataChangeNotifier interface"
Task: T015 "Create ISpeechRecognitionService interface"
Task: T016 "Create IVoiceCommandService interface"

# Launch all foundation tests in parallel:
Task: T022 "Unit tests for DataChangeNotifier"
Task: T023 "Unit tests for SpeechRecognitionService"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (platform permissions)
2. Complete Phase 2: Foundational (DTOs, interfaces, core services)
3. Complete Phase 3: User Story 1 (Log Shot by Voice)
4. **STOP and VALIDATE**: Test voice shot logging end-to-end
5. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → **MVP Ready!** (primary use case)
3. Add User Story 2 → Test independently → Bean/Bag voice support
4. Add User Story 3 → Test independently → Rating voice support
5. Add User Story 4 → Test independently → Equipment/Profile voice support
6. Add User Story 5 → Test independently → Navigation/Query voice support
7. Each story adds value without breaking previous stories

### Single Developer Strategy (Recommended)

Execute phases sequentially in priority order:
1. Phase 1 → Phase 2 → Phase 3 (MVP) → Phase 4 → Phase 5 → Phase 6 → Phase 7 → Phase 8

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable
- Verify tests fail before implementing (Red-Green-Refactor)
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Rating scale is 0-4 per Constitution (not 1-5)
- Use MaterialSymbolsFont.Mic icon (not emoji) per Constitution
