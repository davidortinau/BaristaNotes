# Tasks: Shot Maker, Recipient, and Preinfusion Tracking

**Feature Branch**: `001-shot-tracking`  
**Date**: 2025-12-03  
**Input**: Design documents from `/specs/001-shot-tracking/`

**Prerequisites**: 
- spec.md (user stories and requirements)
- research.md (technology decisions)
- data-model.md (database schema)
- contracts/service-contracts.md (DTOs and interfaces)

**Tests**: Tests are MANDATORY per Constitution Principle II (Test-First Development). Tests must be written FIRST, must FAIL before implementation, and require 80% minimum coverage (100% for critical paths).

**Quality Gates**: All tasks must pass constitution-mandated quality gates before merge: tests pass, static analysis clean, code review approved, performance baseline met, documentation updated.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

BaristaNotes uses a mobile + API architecture:
- **Core**: `BaristaNotes.Core/` - Shared models, interfaces, DTOs
- **Main**: `BaristaNotes/` - MauiReactor UI pages and components
- **Tests**: `BaristaNotes.Tests/` - Unit and integration tests

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project structure validation and dependency verification

- [X] T001 Verify BaristaNotes.Core, BaristaNotes, and BaristaNotes.Tests projects build successfully
- [X] T002 Verify EF Core, MauiReactor, and UXDivers.Popups dependencies are available
- [X] T003 [P] Review existing ShotRecord, UserProfile, and service patterns for consistency

**Checkpoint**: Environment validated - ready for foundational work

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Database Schema Updates

- [X] T004 Add MadeById (int?) field to ShotRecord entity in `BaristaNotes.Core/Models/ShotRecord.cs`
- [X] T005 Add MadeForId (int?) field to ShotRecord entity in `BaristaNotes.Core/Models/ShotRecord.cs`
- [X] T006 Add PreinfusionTime (decimal?) field to ShotRecord entity in `BaristaNotes.Core/Models/ShotRecord.cs`
- [X] T007 Add MadeBy navigation property to ShotRecord entity in `BaristaNotes.Core/Models/ShotRecord.cs`
- [X] T008 Add MadeFor navigation property to ShotRecord entity in `BaristaNotes.Core/Models/ShotRecord.cs`
- [X] T009 Configure MadeBy FK relationship with ReferentialAction.Restrict in `BaristaNotes.Core/Data/BaristaNotesContext.cs`
- [X] T010 Configure MadeFor FK relationship with ReferentialAction.Restrict in `BaristaNotes.Core/Data/BaristaNotesContext.cs`
- [X] T011 Add index for MadeById in `BaristaNotes.Core/Data/BaristaNotesContext.cs`
- [X] T012 Add index for MadeForId in `BaristaNotes.Core/Data/BaristaNotesContext.cs`
- [X] T013 Create EF Core migration: `dotnet ef migrations add AddShotMakerRecipientPreinfusion` from BaristaNotes.Core directory
- [X] T014 Review generated migration SQL to verify nullable columns, FKs with Restrict, and indexes
- [X] T015 Apply migration: `dotnet ef database update` from BaristaNotes.Core directory
- [X] T016 Verify database schema changes in SQLite database

### DTO Updates

- [X] T017 [P] Create SimpleUserDto class in `BaristaNotes.Core/DTOs/SimpleUserDto.cs` with Id, Name, AvatarPath
- [X] T018 Add MadeById (int?) property to CreateShotDto in `BaristaNotes.Core/DTOs/CreateShotDto.cs`
- [X] T019 Add MadeForId (int?) property to CreateShotDto in `BaristaNotes.Core/DTOs/CreateShotDto.cs`
- [X] T020 Add PreinfusionTime (decimal?) property with [Range(0, 60)] to CreateShotDto in `BaristaNotes.Core/DTOs/CreateShotDto.cs`
- [X] T021 Add MadeById (int?) property to UpdateShotDto in `BaristaNotes.Core/DTOs/UpdateShotDto.cs`
- [X] T022 Add MadeForId (int?) property to UpdateShotDto in `BaristaNotes.Core/DTOs/UpdateShotDto.cs`
- [X] T023 Add PreinfusionTime (decimal?) property with [Range(0, 60)] to UpdateShotDto in `BaristaNotes.Core/DTOs/UpdateShotDto.cs`
- [X] T024 Add MadeBy (SimpleUserDto?) property to ShotRecordDto in `BaristaNotes.Core/DTOs/ShotRecordDto.cs`
- [X] T025 Add MadeFor (SimpleUserDto?) property to ShotRecordDto in `BaristaNotes.Core/DTOs/ShotRecordDto.cs`
- [X] T026 Add PreinfusionTime (decimal?) property to ShotRecordDto in `BaristaNotes.Core/DTOs/ShotRecordDto.cs`

### Service Interfaces

- [X] T027 [P] Create IPreferencesService interface in `BaristaNotes.Core/Interfaces/IPreferencesService.cs` with SaveLastUsedShotValues, LoadLastUsedShotValues, ClearAllPreferences, HasPreference methods
- [X] T028 [P] Create PreferencesService implementation in `BaristaNotes.Core/Services/PreferencesService.cs` with constants for preference keys
- [X] T029 Implement SaveLastUsedShotValues in PreferencesService using Preferences.Set for BeanId, MadeById, MadeForId, GrinderId, MachineId, DoseIn, ExpectedTime, ExpectedOutput, GrindSetting, DrinkType
- [X] T030 Implement LoadLastUsedShotValues in PreferencesService using Preferences.Get with null defaults
- [X] T031 Implement ClearAllPreferences in PreferencesService using Preferences.Clear
- [X] T032 Implement HasPreference in PreferencesService using Preferences.ContainsKey
- [X] T033 Register PreferencesService in DI container in `BaristaNotes/MauiProgram.cs`

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Record Shot with Maker and Recipient (Priority: P1) 🎯 MVP

**Goal**: Enable baristas to record who pulled the shot and who it was made for, with data persisting across app restarts via preferences

**Independent Test**: Log a shot, select maker and recipient from user list, save, verify both displayed in activity feed, restart app, log another shot, verify preferences loaded

### Tests for User Story 1 (REQUIRED per Constitution) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T034 [P] [US1] Unit test PreferencesService.SaveLastUsedShotValues stores MadeById and MadeForId in `BaristaNotes.Tests/Services/PreferencesServiceTests.cs`
- [ ] T035 [P] [US1] Unit test PreferencesService.LoadLastUsedShotValues retrieves MadeById and MadeForId in `BaristaNotes.Tests/Services/PreferencesServiceTests.cs`
- [ ] T036 [P] [US1] Unit test PreferencesService.LoadLastUsedShotValues returns null for missing keys in `BaristaNotes.Tests/Services/PreferencesServiceTests.cs`
- [ ] T037 [P] [US1] Unit test ShotService.CreateShotAsync saves MadeById and MadeForId correctly in `BaristaNotes.Tests/Services/ShotServiceTests.cs`
- [ ] T038 [P] [US1] Unit test ShotService.CreateShotAsync throws EntityNotFoundException for invalid MadeById in `BaristaNotes.Tests/Services/ShotServiceTests.cs`
- [ ] T039 [P] [US1] Unit test ShotService.CreateShotAsync throws EntityNotFoundException for invalid MadeForId in `BaristaNotes.Tests/Services/ShotServiceTests.cs`
- [ ] T040 [P] [US1] Unit test ShotService.UpdateShotAsync updates MadeById and MadeForId in `BaristaNotes.Tests/Services/ShotServiceTests.cs`
- [ ] T041 [P] [US1] Unit test ShotService.GetShotByIdAsync includes MadeBy and MadeFor in result in `BaristaNotes.Tests/Services/ShotServiceTests.cs`
- [ ] T042 [US1] Integration test: Create users, create shot with maker/recipient, verify saved, load preferences, create second shot, verify preferences applied in `BaristaNotes.Tests/Integration/ShotCreationFlowTests.cs`

### Implementation for User Story 1

#### Service Layer

- [X] T043 [US1] Update ShotService.CreateShotAsync to map MadeById and MadeForId from CreateShotDto in `BaristaNotes.Core/Services/ShotService.cs`
- [X] T04- [ ] T044 [US1] Add FK validation in ShotService.CreateShotAsync to throw EntityNotFoundException if MadeById or MadeForId reference non-existent users in `BaristaNotes.Core/Services/ShotService.cs`
- [X] T04- [ ] T045 [US1] Update ShotService.UpdateShotAsync to map and update MadeById and MadeForId in `BaristaNotes.Core/Services/ShotService.cs`
- [X] T04- [ ] T046 [US1] Update ShotService.GetShotByIdAsync to include `.Include(s => s.MadeBy)` and `.Include(s => s.MadeFor)` in `BaristaNotes.Core/Services/ShotService.cs`
- [X] T04- [ ] T047 [US1] Update ShotService.GetAllShotsAsync to include `.Include(s => s.MadeBy)` and `.Include(s => s.MadeFor)` in `BaristaNotes.Core/Services/ShotService.cs`
- [X] T04- [ ] T048 [US1] Update ShotService MapToDto method to map MadeBy and MadeFor to SimpleUserDto in `BaristaNotes.Core/Services/ShotService.cs`

#### UI Layer - ShotLoggingPage State

- [X] T04- [ ] T049 [US1] Add `List<SimpleUserDto> Users` property to ShotLoggingPageState in `BaristaNotes/Pages/ShotLoggingPage.cs`
- [X] T050 [US1] Add `SimpleUserDto? SelectedMaker` property to ShotLoggingPageState in `BaristaNotes/Pages/ShotLoggingPage.cs`
- [X] T051 [US1] Add `SimpleUserDto? SelectedRecipient` property to ShotLoggingPageState in `BaristaNotes/Pages/ShotLoggingPage.cs`

#### UI Layer - ShotLoggingPage Lifecycle

- [X] T052 [US1] Inject IUserService and IPreferencesService in ShotLoggingPage constructor in `BaristaNotes/Pages/ShotLoggingPage.cs`
- [X] T053 [US1] Load users via UserService.GetAllUsersAsync in OnMounted in `BaristaNotes/Pages/ShotLoggingPage.cs`
- [X] T054 [US1] Check if editing existing shot (Props.ShotId != null) in OnMounted in `BaristaNotes/Pages/ShotLoggingPage.cs`
- [X] T055 [US1] If new shot: load preferences via PreferencesService.LoadLastUsedShotValues and set SelectedMaker/SelectedRecipient based on IDs in OnMounted in `BaristaNotes/Pages/ShotLoggingPage.cs`
- [X] T056 [US1] If editing: load shot data via ShotService.GetShotByIdAsync and set SelectedMaker/SelectedRecipient from shot in OnMounted in `BaristaNotes/Pages/ShotLoggingPage.cs`

#### UI Layer - ShotLoggingPage Form Controls

- [X] T057 [US1] Add "Made By" label and Picker control below bean selection in Render method in `BaristaNotes/Pages/ShotLoggingPage.cs`
- [X] T058 [US1] Configure "Made By" Picker with ItemsSource=State.Users, ItemDisplayBinding=user.Name, SelectedItem=State.SelectedMaker in `BaristaNotes/Pages/ShotLoggingPage.cs`
- [X] T059 [US1] Handle OnSelectedItemChanged for "Made By" Picker to update State.SelectedMaker in `BaristaNotes/Pages/ShotLoggingPage.cs`
- [X] T060 [US1] Add "Made For" label and Picker control below "Made By" in Render method in `BaristaNotes/Pages/ShotLoggingPage.cs`
- [X] T061 [US1] Configure "Made For" Picker with ItemsSource=State.Users, ItemDisplayBinding=user.Name, SelectedItem=State.SelectedRecipient in `BaristaNotes/Pages/ShotLoggingPage.cs`
- [X] T062 [US1] Handle OnSelectedItemChanged for "Made For" Picker to update State.SelectedRecipient in `BaristaNotes/Pages/ShotLoggingPage.cs`

#### UI Layer - ShotLoggingPage Save Logic

- [X] T063 [US1] Map State.SelectedMaker.Id to CreateShotDto.MadeById in save logic in `BaristaNotes/Pages/ShotLoggingPage.cs`
- [X] T064 [US1] Map State.SelectedRecipient.Id to CreateShotDto.MadeForId in save logic in `BaristaNotes/Pages/ShotLoggingPage.cs`
- [X] T065 [US1] Call PreferencesService.SaveLastUsedShotValues after successful ShotService.CreateShotAsync in `BaristaNotes/Pages/ShotLoggingPage.cs`
- [X] T066 [US1] Show success toast via FeedbackService.ShowToast after save (NON-NEGOTIABLE: must use UXDivers.Popups) in `BaristaNotes/Pages/ShotLoggingPage.cs`

#### UI Layer - Activity Feed Display

- [X] T067 [US1] Add maker display section to ShotRecordCard: HStack with avatar image and "By: {name}" label in `BaristaNotes/Components/ShotRecordCard.cs`
- [X] T068 [US1] Use `.When(() => Props.Shot.MadeBy != null)` to conditionally render maker section in `BaristaNotes/Components/ShotRecordCard.cs`
- [X] T069 [US1] Add recipient display section to ShotRecordCard: HStack with avatar image and "For: {name}" label in `BaristaNotes/Components/ShotRecordCard.cs`
- [X] T07- [ ] T070 [US1] Use `.When(() => Props.Shot.MadeFor != null)` to conditionally render recipient section in `BaristaNotes/Components/ShotRecordCard.cs`
- [X] T07- [ ] T071 [US1] Style avatar images: 24x24px, AspectFill, RoundRectangle clip with 12px radius in `BaristaNotes/Components/ShotRecordCard.cs`
- [X] T07- [ ] T072 [US1] Style maker/recipient labels: 12px font, Gray color, consistent spacing in `BaristaNotes/Components/ShotRecordCard.cs`

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently. Users can select maker/recipient, save shots, see them in activity feed, and preferences persist across app restarts.

---

## Phase 4: User Story 2 - Track Preinfusion Time (Priority: P2)

**Goal**: Enable baristas to record preinfusion time for each shot to correlate with taste outcomes

**Independent Test**: Log a shot, enter preinfusion time (e.g., 5.5), save, verify displayed in shot record, edit shot, update preinfusion time, verify change persisted

### Tests for User Story 2 (REQUIRED per Constitution) ⚠️

- [ ] T073 [P] [US2] Unit test ShotService.CreateShotAsync saves PreinfusionTime correctly in `BaristaNotes.Tests/Services/ShotServiceTests.cs`
- [ ] T074 [P] [US2] Unit test ShotService.CreateShotAsync validates PreinfusionTime range (0-60) in `BaristaNotes.Tests/Services/ShotServiceTests.cs`
- [ ] T075 [P] [US2] Unit test ShotService.CreateShotAsync accepts null PreinfusionTime in `BaristaNotes.Tests/Services/ShotServiceTests.cs`
- [ ] T076 [P] [US2] Unit test ShotService.UpdateShotAsync updates PreinfusionTime in `BaristaNotes.Tests/Services/ShotServiceTests.cs`
- [ ] T077 [P] [US2] Unit test client-side validation rejects negative PreinfusionTime in `BaristaNotes.Tests/Pages/ShotLoggingPageTests.cs`
- [ ] T078 [P] [US2] Unit test client-side validation rejects PreinfusionTime > 60 in `BaristaNotes.Tests/Pages/ShotLoggingPageTests.cs`
- [ ] T079 [US2] Integration test: Create shot with PreinfusionTime, verify saved, update PreinfusionTime, verify changed in `BaristaNotes.Tests/Integration/PreinfusionTrackingTests.cs`

### Implementation for User Story 2

#### Service Layer Validation

- [ ] T080 [US2] Add PreinfusionTime range validation (0-60) to CreateShotDto validation in ShotService in `BaristaNotes.Core/Services/ShotService.cs`
- [ ] T081 [US2] Update MapToDto to include PreinfusionTime in `BaristaNotes.Core/Services/ShotService.cs`
- [ ] T082 [US2] Add PreinfusionTime to PreferencesService.SaveLastUsedShotValues in `BaristaNotes.Core/Services/PreferencesService.cs`
- [ ] T083 [US2] Add PreinfusionTime to PreferencesService.LoadLastUsedShotValues in `BaristaNotes.Core/Services/PreferencesService.cs`

#### UI Layer - ShotLoggingPage State

- [ ] T084 [US2] Add `decimal? PreinfusionTime` property to ShotLoggingPageState in `BaristaNotes/Pages/ShotLoggingPage.cs`

#### UI Layer - ShotLoggingPage Form Control

- [ ] T085 [US2] Add "Preinfusion Time (seconds)" label below extraction time field in Render in `BaristaNotes/Pages/ShotLoggingPage.cs`
- [ ] T086 [US2] Add Entry control for preinfusion time with Keyboard.Numeric, Placeholder="e.g., 5.5" in `BaristaNotes/Pages/ShotLoggingPage.cs`
- [ ] T087 [US2] Bind Entry.Text to State.PreinfusionTime with decimal parsing in TextChanged handler in `BaristaNotes/Pages/ShotLoggingPage.cs`
- [ ] T088 [US2] Add client-side validation for PreinfusionTime range (0-60) in save logic in `BaristaNotes/Pages/ShotLoggingPage.cs`
- [ ] T089 [US2] Show error toast via FeedbackService.ShowError if PreinfusionTime validation fails in `BaristaNotes/Pages/ShotLoggingPage.cs`

#### UI Layer - ShotLoggingPage Save/Load Logic

- [ ] T090 [US2] Map State.PreinfusionTime to CreateShotDto.PreinfusionTime in save logic in `BaristaNotes/Pages/ShotLoggingPage.cs`
- [ ] T091 [US2] Load PreinfusionTime from preferences in OnMounted for new shots in `BaristaNotes/Pages/ShotLoggingPage.cs`
- [ ] T092 [US2] Load PreinfusionTime from shot data in OnMounted when editing in `BaristaNotes/Pages/ShotLoggingPage.cs`

#### UI Layer - Activity Feed Display

- [ ] T093 [US2] Add preinfusion display to ShotRecordCard: "Preinfusion: {time:F1}s" label in `BaristaNotes/Components/ShotRecordCard.cs`
- [ ] T094 [US2] Use `.When(() => Props.Shot.PreinfusionTime.HasValue)` to conditionally render preinfusion section in `BaristaNotes/Components/ShotRecordCard.cs`
- [ ] T095 [US2] Style preinfusion label: 12px font, Gray color, consistent with other metadata in `BaristaNotes/Components/ShotRecordCard.cs`

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently. Users can track maker, recipient, and preinfusion time with all data persisting.

---

## Phase 5: User Story 3 - Filter and Analyze by Maker and Recipient (Priority: P3)

**Goal**: Enable cafe owners to filter shots by barista and customer for performance analysis and customer preference tracking

**Independent Test**: Log multiple shots with different makers and recipients, apply filter to view shots by specific barista, verify only matching shots shown, clear filter, verify all shots displayed

### Tests for User Story 3 (REQUIRED per Constitution) ⚠️

- [ ] T096 [P] [US3] Unit test ShotService.GetShotsByMakerAsync returns only shots by specified maker in `BaristaNotes.Tests/Services/ShotServiceTests.cs`
- [ ] T097 [P] [US3] Unit test ShotService.GetShotsByRecipientAsync returns only shots for specified recipient in `BaristaNotes.Tests/Services/ShotServiceTests.cs`
- [ ] T098 [P] [US3] Unit test ShotService.GetShotsByMakerAsync excludes soft-deleted shots in `BaristaNotes.Tests/Services/ShotServiceTests.cs`
- [ ] T099 [P] [US3] Unit test ShotService.GetShotsByRecipientAsync excludes soft-deleted shots in `BaristaNotes.Tests/Services/ShotServiceTests.cs`
- [ ] T100 [US3] Integration test: Create shots with multiple makers, filter by maker, verify results, clear filter, verify all shots in `BaristaNotes.Tests/Integration/ShotFilteringTests.cs`

### Implementation for User Story 3

#### Service Layer

- [ ] T101 [P] [US3] Implement ShotService.GetShotsByMakerAsync with query filtering by MadeById in `BaristaNotes.Core/Services/ShotService.cs`
- [ ] T102 [P] [US3] Implement ShotService.GetShotsByRecipientAsync with query filtering by MadeForId in `BaristaNotes.Core/Services/ShotService.cs`
- [ ] T103 [US3] Add Include statements for MadeBy, MadeFor, Bean, Grinder, Machine in both filter methods in `BaristaNotes.Core/Services/ShotService.cs`
- [ ] T104 [US3] Add OrderByDescending(s => s.Timestamp) to both filter methods in `BaristaNotes.Core/Services/ShotService.cs`

#### UI Layer - Activity Feed Page State

- [ ] T105 [US3] Add `SimpleUserDto? FilterByMaker` property to ActivityFeedPageState in `BaristaNotes/Pages/ActivityFeedPage.cs`
- [ ] T106 [US3] Add `SimpleUserDto? FilterByRecipient` property to ActivityFeedPageState in `BaristaNotes/Pages/ActivityFeedPage.cs`
- [ ] T107 [US3] Add `List<SimpleUserDto> Users` property to ActivityFeedPageState in `BaristaNotes/Pages/ActivityFeedPage.cs`

#### UI Layer - Activity Feed Page Filter Controls

- [ ] T108 [US3] Inject IUserService in ActivityFeedPage constructor in `BaristaNotes/Pages/ActivityFeedPage.cs`
- [ ] T109 [US3] Load users via UserService.GetAllUsersAsync in OnMounted in `BaristaNotes/Pages/ActivityFeedPage.cs`
- [ ] T110 [US3] Add filter controls section at top of Render method in `BaristaNotes/Pages/ActivityFeedPage.cs`
- [ ] T111 [US3] Add "Filter by Maker" Picker with ItemsSource=State.Users in `BaristaNotes/Pages/ActivityFeedPage.cs`
- [ ] T112 [US3] Add "Filter by Recipient" Picker with ItemsSource=State.Users in `BaristaNotes/Pages/ActivityFeedPage.cs`
- [ ] T113 [US3] Add "Clear Filters" button in `BaristaNotes/Pages/ActivityFeedPage.cs`

#### UI Layer - Activity Feed Page Filter Logic

- [ ] T114 [US3] Implement OnFilterByMakerChanged handler to call ShotService.GetShotsByMakerAsync when maker selected in `BaristaNotes/Pages/ActivityFeedPage.cs`
- [ ] T115 [US3] Implement OnFilterByRecipientChanged handler to call ShotService.GetShotsByRecipientAsync when recipient selected in `BaristaNotes/Pages/ActivityFeedPage.cs`
- [ ] T116 [US3] Implement OnClearFilters handler to reset filters and call ShotService.GetAllShotsAsync in `BaristaNotes/Pages/ActivityFeedPage.cs`
- [ ] T117 [US3] Update State.Shots with filtered results in all filter handlers in `BaristaNotes/Pages/ActivityFeedPage.cs`
- [ ] T118 [US3] Show loading indicator while filtering in `BaristaNotes/Pages/ActivityFeedPage.cs`

**Checkpoint**: All user stories should now be independently functional. Users can track shots with maker/recipient/preinfusion, and filter/analyze by maker or recipient.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories and final quality gates

- [ ] T119 [P] Update quickstart.md with any implementation deviations in `/specs/001-shot-tracking/quickstart.md`
- [ ] T120 [P] Add XML documentation comments to all new public methods and classes
- [ ] T121 Code review: Verify single responsibility principle across all new services and components
- [ ] T122 Code review: Verify no code duplication in user selection logic between ShotLoggingPage and ActivityFeedPage
- [ ] T123 [P] Run static analysis and fix any warnings in BaristaNotes.Core and BaristaNotes projects
- [ ] T124 Performance test: Verify ShotLoggingPage loads and pre-populates in <2 seconds with 100 users
- [ ] T125 Performance test: Verify user picker responds to selection within 100ms
- [ ] T126 Performance test: Verify preferences save/load completes in <10ms
- [ ] T127 [P] Accessibility audit: Verify picker controls are keyboard navigable
- [ ] T128 [P] Accessibility audit: Verify screen readers announce picker labels and values
- [ ] T129 [P] Accessibility audit: Verify touch targets are minimum 44x44px
- [ ] T130 Manual test: Verify maker/recipient avatars display correctly in activity feed
- [ ] T131 Manual test: Verify same user can be selected for both maker and recipient
- [ ] T132 Manual test: Verify shots with empty maker/recipient display correctly
- [ ] T133 Manual test: Verify deleted users still show in historical shot records
- [ ] T134 Manual test: Verify preferences persist after app restart on iOS
- [ ] T135 Manual test: Verify preferences persist after app restart on Android
- [ ] T136 Verify test coverage meets 80% minimum across all new code
- [ ] T137 Constitution compliance verification: All 4 principles (Code Quality, Test-First, UX Consistency, Performance) checked and documented

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3, 4, 5)**: All depend on Foundational phase completion
  - User Story 1 (P1): Can start after Foundational - No dependencies on other stories
  - User Story 2 (P2): Can start after Foundational - Extends US1 but independently testable
  - User Story 3 (P3): Can start after Foundational - Uses US1 data but independently testable
- **Polish (Phase 6)**: Depends on all desired user stories being complete

### Within Each User Story

- Tests MUST be written and FAIL before implementation (TDD)
- Service layer before UI layer (DTOs, service methods before pages/components)
- State properties before lifecycle methods (OnMounted)
- Lifecycle methods before render methods (data loading before display)
- Core implementation before integration

### Parallel Opportunities

- **Phase 1 Setup**: Tasks T001-T003 can run in parallel
- **Phase 2 Foundational**: 
  - DTO tasks T017-T026 can run in parallel
  - Service interface tasks T027-T028 can run in parallel
- **Phase 3 User Story 1 Tests**: Tasks T034-T041 can run in parallel (different test files)
- **Phase 4 User Story 2 Tests**: Tasks T073-T078 can run in parallel
- **Phase 5 User Story 3 Tests**: Tasks T096-T099 can run in parallel
- **Phase 5 User Story 3 Service**: Tasks T101-T102 can run in parallel (different methods)
- **Phase 6 Polish**: Tasks T119-T120, T123, T127-T129 can run in parallel

### Critical Path

1. T001-T003 (Setup)
2. T004-T016 (Database migration) - BLOCKING
3. T017-T033 (DTOs and services) - BLOCKING
4. User Story 1 (T034-T072) - MVP deliverable
5. User Story 2 (T073-T095) - Extends MVP
6. User Story 3 (T096-T118) - Analytics feature
7. T119-T137 (Polish and quality gates)

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T003)
2. Complete Phase 2: Foundational (T004-T033) - CRITICAL, blocks all stories
3. Complete Phase 3: User Story 1 (T034-T072)
4. **STOP and VALIDATE**: Test User Story 1 independently
5. Deploy/demo if ready (maker/recipient tracking functional)

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy/Demo (MVP: maker/recipient tracking!)
3. Add User Story 2 → Test independently → Deploy/Demo (Enhanced: preinfusion tracking!)
4. Add User Story 3 → Test independently → Deploy/Demo (Analytics: filtering!)
5. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together (T001-T033)
2. Once Foundational is done:
   - Developer A: User Story 1 (T034-T072)
   - Developer B: User Story 2 (T073-T095) - can start in parallel
   - Developer C: User Story 3 (T096-T118) - can start in parallel
3. Stories complete and integrate independently

---

## Notes

- [P] tasks = different files, no dependencies, safe to parallelize
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Write tests FIRST (TDD) - verify they fail before implementing
- Use UXDivers.Popups for all user feedback (NON-NEGOTIABLE per constitution)
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Maintain 80% test coverage minimum (100% for validation logic)
