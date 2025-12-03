# Tasks: CRUD Operation Visual Feedback

**Feature**: 001-crud-feedback  
**Date**: 2025-12-02  
**Input**: Design documents from `/specs/001-crud-feedback/`

**Tests**: Tests are MANDATORY per Constitution Principle II (Test-First Development). Tests must be written FIRST, must FAIL before implementation, and require 80% minimum coverage.

**Quality Gates**: All tasks must pass constitution-mandated quality gates before merge: tests pass, static analysis clean, code review approved, performance baseline met, documentation updated.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `- [ ] [ID] [P?] [Story?] Description`

- **Checkbox**: ALWAYS start with `- [ ]`
- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and dependency installation

- [X] T001 Install System.Reactive NuGet package (v6.0+) in BaristaNotes/BaristaNotes.csproj
- [X] T002 Verify MauiReactor and CommunityToolkit.Maui packages are installed

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core models and service infrastructure that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T003 [P] Create FeedbackType enum in BaristaNotes/Models/FeedbackType.cs
- [X] T004 [P] Create FeedbackMessage model in BaristaNotes/Models/FeedbackMessage.cs
- [X] T005 [P] Create OperationResult<T> generic wrapper in BaristaNotes/Models/OperationResult.cs
- [X] T006 Create IFeedbackService interface in BaristaNotes/Services/IFeedbackService.cs
- [X] T007 Implement FeedbackService in BaristaNotes/Services/FeedbackService.cs
- [X] T008 Register IFeedbackService as singleton in BaristaNotes/MauiProgram.cs

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Successful Operation Confirmation (Priority: P1) 🎯 MVP

**Goal**: Display immediate visual confirmation when CRUD operations succeed

**Independent Test**: Perform any save operation and verify success indicator appears within 100ms with correct message

### Tests for User Story 1 (REQUIRED per Constitution) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T009 [P] [US1] Create FeedbackServiceTests.cs in BaristaNotes.Tests/Unit/FeedbackServiceTests.cs
- [ ] T010 [P] [US1] Write unit test: ShowSuccess publishes message with Success type
- [ ] T011 [P] [US1] Write unit test: ShowSuccess message has correct duration (2000ms default)
- [ ] T012 [P] [US1] Write unit test: ShowSuccess validates message length (1-200 chars)
- [ ] T013 [P] [US1] Create OperationResultTests.cs in BaristaNotes.Tests/Unit/OperationResultTests.cs
- [ ] T014 [P] [US1] Write unit test: OperationResult.Ok requires non-null data
- [ ] T015 [P] [US1] Write unit test: OperationResult.Ok sets Success=true and ErrorMessage=null

### Implementation for User Story 1

- [ ] T016 [P] [US1] Create ToastComponent.cs Reactor component in BaristaNotes/Components/Feedback/ToastComponent.cs
- [ ] T017 [P] [US1] Create FeedbackOverlay.cs Reactor component in BaristaNotes/Components/Feedback/FeedbackOverlay.cs
- [ ] T018 [US1] Implement ToastComponent with slide-in animation (TranslationY -50 to 0)
- [ ] T019 [US1] Implement auto-dismiss logic in ToastComponent using Task.Delay
- [ ] T020 [US1] Implement FeedbackOverlay to subscribe to IFeedbackService.FeedbackMessages
- [ ] T021 [US1] Add coffee-themed success colors (green #8BC34A dark, #689F38 light)
- [ ] T022 [US1] Add checkmark icon to success toasts
- [ ] T023 [US1] Add SemanticProperties.Announce for screen reader support
- [ ] T024 [US1] Update BeanService.CreateAsync to return OperationResult<Bean> in BaristaNotes.Core/Services/BeanService.cs
- [ ] T025 [US1] Update BeanForm component to call FeedbackService.ShowSuccess in BaristaNotes/Components/Beans/BeanForm.cs
- [ ] T026 [US1] Add FeedbackOverlay to MainPage.cs layout in BaristaNotes/MainPage.cs
- [ ] T027 [US1] Integration test: Create bean triggers success feedback in BaristaNotes.Tests/Integration/BeanCrudFeedbackTests.cs

**Checkpoint**: User Story 1 complete - success feedback working for bean creation

---

## Phase 4: User Story 2 - Failed Operation Error Feedback (Priority: P1)

**Goal**: Display clear error messages when CRUD operations fail with actionable recovery guidance

**Independent Test**: Trigger validation error (save bean without name) and verify error message appears with recovery action

### Tests for User Story 2 (REQUIRED per Constitution) ⚠️

- [ ] T028 [P] [US2] Write unit test: ShowError publishes message with Error type
- [ ] T029 [P] [US2] Write unit test: ShowError includes RecoveryAction in message
- [ ] T030 [P] [US2] Write unit test: ShowError has longer duration (5000ms default)
- [ ] T031 [P] [US2] Write unit test: Only one error message visible at a time
- [ ] T032 [P] [US2] Write unit test: OperationResult.Fail requires error message
- [ ] T033 [P] [US2] Write unit test: OperationResult.Fail sets Success=false and Data=null

### Implementation for User Story 2

- [ ] T034 [P] [US2] Add coffee-themed error colors (warm red #E57373 dark, #C62828 light)
- [ ] T035 [P] [US2] Add warning icon to error toasts in ToastComponent
- [ ] T036 [US2] Implement error queue logic in FeedbackService (max 1 error visible)
- [ ] T037 [US2] Update ToastComponent to display RecoveryAction text below main message
- [ ] T038 [US2] Update BeanService.CreateAsync to return Fail result on validation errors
- [ ] T039 [US2] Update BeanService to return Fail result on DbUpdateException with recovery action
- [ ] T040 [US2] Update BeanForm to call FeedbackService.ShowError with recovery action
- [ ] T041 [US2] Update EquipmentService CRUD methods to return OperationResult<Equipment> in BaristaNotes.Core/Services/EquipmentService.cs
- [ ] T042 [US2] Update EquipmentForm to handle error feedback in BaristaNotes/Components/Equipment/EquipmentForm.cs
- [ ] T043 [US2] Integration test: Validation error triggers error feedback with recovery action

**Checkpoint**: User Stories 1 AND 2 complete - success and error feedback working

---

## Phase 5: User Story 3 - Operation In-Progress Indicators (Priority: P2)

**Goal**: Display loading overlays during CRUD operations to prevent duplicate submissions

**Independent Test**: Trigger long-running operation (with network delay) and verify loading overlay appears immediately

### Tests for User Story 3 (REQUIRED per Constitution) ⚠️

- [ ] T044 [P] [US3] Write unit test: ShowLoading publishes loading state (IsLoading=true)
- [ ] T045 [P] [US3] Write unit test: HideLoading publishes loading state (IsLoading=false)
- [ ] T046 [P] [US3] Write unit test: ShowLoading replaces previous loading message
- [ ] T047 [P] [US3] Write unit test: Multiple ShowLoading calls don't stack

### Implementation for User Story 3

- [ ] T048 [P] [US3] Create LoadingOverlay.cs Reactor component in BaristaNotes/Components/Feedback/LoadingOverlay.cs
- [ ] T049 [US3] Implement LoadingOverlay with spinner (ActivityIndicator) and message
- [ ] T050 [US3] Subscribe LoadingOverlay to IFeedbackService.LoadingState observable
- [ ] T051 [US3] Add semi-transparent background overlay (coffee brown #2D1F1A with 0.7 opacity)
- [ ] T052 [US3] Add LoadingOverlay to FeedbackOverlay component (stacks above toasts)
- [ ] T053 [US3] Update ShotService.CreateAsync to call ShowLoading/HideLoading in BaristaNotes.Core/Services/ShotService.cs
- [ ] T054 [US3] Update ShotForm to handle loading states in BaristaNotes/Components/Shots/ShotForm.cs
- [ ] T055 [US3] Update ProfileService CRUD methods to call ShowLoading/HideLoading in BaristaNotes.Core/Services/ProfileService.cs
- [ ] T056 [US3] Integration test: Long operation shows loading overlay and prevents duplicate submissions

**Checkpoint**: All 3 user stories complete - success, error, and loading feedback fully implemented

---

## Phase 6: Complete CRUD Coverage

**Purpose**: Extend feedback to all remaining CRUD operations across the app

- [ ] T057 [P] Update BeanService.UpdateAsync to return OperationResult<Bean> with feedback
- [ ] T058 [P] Update BeanService.DeleteAsync to return OperationResult<bool> with feedback
- [ ] T059 [P] Update EquipmentService.UpdateAsync to return OperationResult<Equipment> with feedback
- [ ] T060 [P] Update EquipmentService.DeleteAsync to return OperationResult<bool> with feedback
- [ ] T061 [P] Update ShotService.UpdateAsync to return OperationResult<Shot> with feedback
- [ ] T062 [P] Update ShotService.DeleteAsync to return OperationResult<bool> with feedback
- [ ] T063 [P] Update ProfileService.CreateAsync to return OperationResult<Profile> with feedback
- [ ] T064 [P] Update ProfileService.UpdateAsync to return OperationResult<Profile> with feedback
- [ ] T065 [P] Update ProfileService.DeleteAsync to return OperationResult<bool> with feedback
- [ ] T066 [P] Update BeanList component to show feedback on delete in BaristaNotes/Components/Beans/BeanList.cs
- [ ] T067 [P] Update EquipmentList component to show feedback on delete in BaristaNotes/Components/Equipment/EquipmentList.cs
- [ ] T068 [P] Update ShotActivityFeed component to show feedback on operations in BaristaNotes/Components/Shots/ShotActivityFeed.cs
- [ ] T069 Integration test: Equipment CRUD operations trigger appropriate feedback in BaristaNotes.Tests/Integration/EquipmentCrudFeedbackTests.cs
- [ ] T070 Integration test: Shot CRUD operations trigger appropriate feedback in BaristaNotes.Tests/Integration/ShotCrudFeedbackTests.cs
- [ ] T071 Integration test: Profile CRUD operations trigger appropriate feedback in BaristaNotes.Tests/Integration/ProfileCrudFeedbackTests.cs

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final quality, accessibility, and constitution compliance verification

- [ ] T072 [P] Add ShowInfo and ShowWarning method implementations in FeedbackService
- [ ] T073 [P] Add info color (#64B5F6 dark, #1976D2 light) and warning color (#FFB74D dark, #F57C00 light)
- [ ] T074 Implement toast stacking logic (max 3 messages, auto-dismiss oldest)
- [ ] T075 Add swipe-to-dismiss gesture to ToastComponent
- [ ] T076 Verify minimum touch target size 48x48dp for dismiss actions
- [ ] T077 Verify color contrast ratios meet WCAG 2.1 AA (4.5:1 text, 3:1 UI components)
- [ ] T078 Test screen reader announcements on iOS (VoiceOver) and Android (TalkBack)
- [ ] T079 Verify feedback appears within 100ms performance target
- [ ] T080 Verify animations run at 60fps on physical devices
- [ ] T081 Run all unit tests and verify 80%+ code coverage
- [ ] T082 Update quickstart.md with final implementation details
- [ ] T083 Constitution compliance verification: All 4 principles satisfied
- [ ] T084 Create demo recording showing all feedback types in action

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-5)**: All depend on Foundational phase completion
  - User stories can proceed in parallel (if multiple developers)
  - Or sequentially in priority order: US1 (P1) → US2 (P1) → US3 (P2)
- **Complete CRUD Coverage (Phase 6)**: Depends on Phase 3-5 completion
- **Polish (Phase 7)**: Depends on Phase 6 completion

### User Story Dependencies

- **User Story 1 (P1)**: Can start immediately after Foundational (Phase 2) - No dependencies
- **User Story 2 (P1)**: Can start after Foundational (Phase 2) - Builds on US1 ToastComponent but independently testable
- **User Story 3 (P2)**: Can start after Foundational (Phase 2) - Independent LoadingOverlay, testable separately

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Reactor components (ToastComponent, FeedbackOverlay, LoadingOverlay) before service integration
- Service updates before component integration
- Story complete before moving to next priority

### Parallel Opportunities

**Phase 1 - Setup:**
- T001 and T002 can run in parallel (independent verification tasks)

**Phase 2 - Foundational:**
- T003, T004, T005 can run in parallel (different model files)
- T006, T007, T008 must run sequentially (interface → implementation → registration)

**Phase 3 - User Story 1:**
- T009-T015 (all tests) can run in parallel (different test files/methods)
- T016, T017 can run in parallel (different component files)
- T024 (service update) can run parallel to T016-T023 (UI components)

**Phase 4 - User Story 2:**
- T028-T033 (all tests) can run in parallel
- T034, T035 can run in parallel (styling tasks)
- T041 (EquipmentService) can run parallel to T038-T040 (BeanService integration)

**Phase 5 - User Story 3:**
- T044-T047 (all tests) can run in parallel
- T048, T049, T050 can run parallel (LoadingOverlay implementation)
- T053, T055 can run in parallel (different service files)

**Phase 6 - Complete CRUD:**
- T057-T065 can run in parallel (different service methods, different files)
- T066-T068 can run in parallel (different component files)
- T069-T071 can run in parallel (different test files)

**Phase 7 - Polish:**
- T072, T073 can run in parallel (independent additions)
- T076, T077, T078 can run in parallel (different accessibility checks)
- T079, T080 can run in parallel (different performance metrics)

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together:
$ gh copilot task "Write FeedbackServiceTests.ShowSuccess test" &
$ gh copilot task "Write FeedbackServiceTests duration test" &
$ gh copilot task "Write OperationResult.Ok test" &

# Launch UI components in parallel:
$ gh copilot task "Create ToastComponent.cs" &
$ gh copilot task "Create FeedbackOverlay.cs" &
$ gh copilot task "Update BeanService to return OperationResult" &

# Wait for all to complete, then integrate
```

---

## Implementation Strategy

### MVP First (User Stories 1 & 2 Only - Both P1)

1. Complete Phase 1: Setup ✅
2. Complete Phase 2: Foundational ✅ (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1 ✅ (Success feedback)
4. Complete Phase 4: User Story 2 ✅ (Error feedback)
5. **STOP and VALIDATE**: Test success and error feedback independently
6. Deploy/demo core feedback functionality

### Incremental Delivery

1. **Foundation** (Phases 1-2): Models, service interface, service implementation
2. **MVP** (Phases 3-4): Success + Error feedback for beans → Demo ready
3. **Loading States** (Phase 5): User Story 3 → Full feedback suite
4. **Complete Coverage** (Phase 6): Extend to all CRUD operations
5. **Polish** (Phase 7): Accessibility, performance, final quality gates

### Parallel Team Strategy

With multiple developers:

1. **Week 1**: Everyone on Foundation (Phases 1-2)
2. **Week 2**: Split after Foundation complete
   - Developer A: User Story 1 (Success feedback)
   - Developer B: User Story 2 (Error feedback)
   - Developer C: User Story 3 (Loading states)
3. **Week 3**: Phase 6 (split CRUD coverage by entity)
4. **Week 4**: Phase 7 (polish together)

---

## Task Summary

**Total Tasks**: 84

**By Phase**:
- Phase 1 (Setup): 2 tasks
- Phase 2 (Foundational): 6 tasks
- Phase 3 (US1 - Success): 19 tasks (7 tests, 12 implementation)
- Phase 4 (US2 - Error): 16 tasks (6 tests, 10 implementation)
- Phase 5 (US3 - Loading): 13 tasks (4 tests, 9 implementation)
- Phase 6 (Complete CRUD): 15 tasks
- Phase 7 (Polish): 13 tasks

**By User Story**:
- US1 (Success feedback): 19 tasks
- US2 (Error feedback): 16 tasks
- US3 (Loading states): 13 tasks
- Infrastructure: 8 tasks
- Complete CRUD: 15 tasks
- Polish: 13 tasks

**Parallel Opportunities**: 42 tasks marked [P] can run in parallel (50% of total)

**Suggested MVP Scope**: Phases 1-4 (User Stories 1 & 2) = 43 tasks = Core success/error feedback

---

## Notes

- All tasks follow strict checklist format: `- [ ] [ID] [P?] [Story?] Description with file path`
- [P] tasks target different files with no dependencies
- Each user story is independently testable and deployable
- Tests written FIRST per Constitution Principle II
- 80% minimum code coverage required before merge
- Verify Constitution compliance at Phase 7 checkpoint
- Coffee-themed colors defined per research.md design system
- WCAG 2.1 AA accessibility mandatory per Constitution Principle III
