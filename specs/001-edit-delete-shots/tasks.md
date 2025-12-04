# Tasks: Edit and Delete Shots from Activity Page

**Input**: Design documents from `/specs/001-edit-delete-shots/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/IShotService.md

**Tests**: MANDATORY per Constitution Principle II (Test-First Development). Tests must be written FIRST, must FAIL before implementation, and require 100% coverage for critical paths (UpdateShotAsync, DeleteShotAsync).

**Quality Gates**: All tasks must pass constitution-mandated quality gates before merge: tests pass, static analysis clean, code review approved, performance baseline met, documentation updated.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

This is a .NET MAUI project with the following structure:
- **Core library**: `BaristaNotes.Core/` (business logic, services, data models)
- **MAUI app**: `BaristaNotes/` (UI components, pages, MVU pattern)
- **Tests**: `BaristaNotes.Tests/` (xUnit test project)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure verification

- [X] T001 Verify existing project structure matches plan.md requirements
- [X] T002 Verify UXDivers.Popups.Maui 0.9.0 is installed and scaffolded
- [X] T003 Verify Reactor.Maui 4.0.3-beta is properly configured

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core service layer and DTOs that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 Create UpdateShotDto.cs in BaristaNotes.Core/Services/DTOs/ with ActualTime, ActualOutput, Rating, DrinkType fields
- [X] T005 Add UpdateShotAsync method signature to IShotService.cs in BaristaNotes.Core/Services/
- [X] T006 Add DeleteShotAsync method signature to IShotService.cs in BaristaNotes.Core/Services/
- [X] T007 Implement private ValidateUpdateShot method in ShotService.cs with validation rules per contracts/IShotService.md
- [X] T008 Implement UpdateShotAsync in ShotService.cs (validate, get shot, update fields, persist, return DTO)
- [X] T009 Implement DeleteShotAsync in ShotService.cs (get shot, set IsDeleted=true, update LastModifiedAt, persist)

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Delete Shot from Activity (Priority: P1) 🎯 MVP

**Goal**: Enable users to delete shot notes from activity page with confirmation to remove mistakes or duplicate entries

**Independent Test**: Create shot note → navigate to activity page → swipe to delete → confirm deletion → verify shot removed from list and database

### Tests for User Story 1 (REQUIRED per Constitution) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T010 [P] [US1] Unit test: DeleteShotAsync with valid ID soft deletes shot in BaristaNotes.Tests/Services/ShotServiceTests.cs
- [X] T011 [P] [US1] Unit test: DeleteShotAsync throws NotFoundException for invalid ID in BaristaNotes.Tests/Services/ShotServiceTests.cs
- [X] T012 [P] [US1] Unit test: DeleteShotAsync updates LastModifiedAt timestamp in BaristaNotes.Tests/Services/ShotServiceTests.cs
- [X] T013 [P] [US1] Integration test: Deleted shots do not appear in GetShotHistoryAsync results in BaristaNotes.Tests/Integration/ShotDatabaseTests.cs
- [X] T014 [P] [US1] Integration test: DeleteShotAsync persists IsDeleted flag to database in BaristaNotes.Tests/Integration/ShotDatabaseTests.cs

### Implementation for User Story 1

- [ ] T015 [US1] Add ShowDeleteConfirmation state fields (ShotToDelete, ShowDeleteConfirmation) to ActivityFeedPage.cs state in BaristaNotes/Pages/
- [ ] T016 [US1] Add DeleteRequested, DeleteConfirmed, DeleteCanceled messages to ActivityFeedPage.cs message enum in BaristaNotes/Pages/
- [ ] T017 [US1] Create RenderDeleteConfirmationPopup method in ActivityFeedPage.cs using UXDivers RxPopupPage in BaristaNotes/Pages/
- [ ] T018 [US1] Implement ShowDeleteConfirmation method in ActivityFeedPage.cs to display popup in BaristaNotes/Pages/
- [ ] T019 [US1] Implement DeleteShot async method in ActivityFeedPage.cs (call DeleteShotAsync, show feedback, refresh) in BaristaNotes/Pages/
- [ ] T020 [US1] Add SwipeItem for Delete action to shot card rendering in ActivityFeedPage.cs in BaristaNotes/Pages/
- [ ] T021 [US1] Wire up SwipeItem Delete OnInvoked to ShowDeleteConfirmation in ActivityFeedPage.cs in BaristaNotes/Pages/
- [ ] T022 [US1] Handle DeleteConfirmed message in OnMessageAsync to call DeleteShot in ActivityFeedPage.cs in BaristaNotes/Pages/
- [ ] T023 [US1] Add visual feedback (toast) on successful delete using IFeedbackService in ActivityFeedPage.cs in BaristaNotes/Pages/
- [ ] T024 [US1] Add error handling for NotFoundException in DeleteShot method in ActivityFeedPage.cs in BaristaNotes/Pages/

**Checkpoint**: At this point, User Story 1 should be fully functional - users can delete shots with confirmation

---

## Phase 4: User Story 2 - Edit Shot from Activity (Priority: P2)

**Goal**: Enable users to edit shot note details (time, output, rating, drink type) from activity page to correct mistakes without deleting and recreating

**Independent Test**: Create shot note with specific values → navigate to activity page → swipe to edit → modify fields → save → verify updated values persist correctly

### Tests for User Story 2 (REQUIRED per Constitution) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T025 [P] [US2] Unit test: UpdateShotAsync with valid DTO updates all fields in BaristaNotes.Tests/Services/ShotServiceTests.cs
- [X] T026 [P] [US2] Unit test: UpdateShotAsync with partial DTO updates only provided fields in BaristaNotes.Tests/Services/ShotServiceTests.cs
- [X] T027 [P] [US2] Unit test: UpdateShotAsync throws NotFoundException for invalid ID in BaristaNotes.Tests/Services/ShotServiceTests.cs
- [X] T028 [P] [US2] Unit test: UpdateShotAsync throws ValidationException for invalid ActualTime in BaristaNotes.Tests/Services/ShotServiceTests.cs
- [X] T029 [P] [US2] Unit test: UpdateShotAsync throws ValidationException for invalid ActualOutput in BaristaNotes.Tests/Services/ShotServiceTests.cs
- [X] T030 [P] [US2] Unit test: UpdateShotAsync throws ValidationException for invalid Rating in BaristaNotes.Tests/Services/ShotServiceTests.cs
- [X] T031 [P] [US2] Unit test: UpdateShotAsync throws ValidationException for empty DrinkType in BaristaNotes.Tests/Services/ShotServiceTests.cs
- [X] T032 [P] [US2] Unit test: UpdateShotAsync updates LastModifiedAt timestamp in BaristaNotes.Tests/Services/ShotServiceTests.cs
- [X] T033 [P] [US2] Unit test: UpdateShotAsync preserves immutable fields (Timestamp, BeanId, GrindSetting, DoseIn) in BaristaNotes.Tests/Services/ShotServiceTests.cs
- [X] T034 [P] [US2] Integration test: UpdateShotAsync persists changes to database in BaristaNotes.Tests/Integration/ShotDatabaseTests.cs
- [X] T035 [P] [US2] Integration test: Updated shots appear correctly in GetShotHistoryAsync results in BaristaNotes.Tests/Integration/ShotDatabaseTests.cs

### Implementation for User Story 2

- [ ] T036 [P] [US2] Create EditShotPage.cs component in BaristaNotes/Pages/ with MVU structure
- [ ] T037 [P] [US2] Define EditShotPageState class with ShotId, IsLoading, IsSaving, Timestamp, BeanName, GrindSetting, DoseIn, ActualTime, ActualOutput, Rating, DrinkType, ValidationErrors in BaristaNotes/Pages/EditShotPage.cs
- [ ] T038 [P] [US2] Define EditShotMessage enum with Load, Loaded, ActualTimeChanged, ActualOutputChanged, RatingChanged, DrinkTypeChanged, Save, Saved, Cancel, ValidationFailed in BaristaNotes/Pages/EditShotPage.cs
- [ ] T039 [US2] Implement OnMounted to initialize state with ShotId prop in BaristaNotes/Pages/EditShotPage.cs
- [ ] T040 [US2] Implement LoadShotData async method to call GetShotByIdAsync and populate state in BaristaNotes/Pages/EditShotPage.cs
- [ ] T041 [US2] Implement Render method with ContentPage, ScrollView, VStack layout in BaristaNotes/Pages/EditShotPage.cs
- [ ] T042 [US2] Add readonly fields display (Timestamp, BeanName, GrindSetting, DoseIn) in Render method in BaristaNotes/Pages/EditShotPage.cs
- [ ] T043 [US2] Add Entry for ActualTime with numeric keyboard and OnTextChanged handler in BaristaNotes/Pages/EditShotPage.cs
- [ ] T044 [US2] Add Entry for ActualOutput with numeric keyboard and OnTextChanged handler in BaristaNotes/Pages/EditShotPage.cs
- [ ] T045 [US2] Add Picker for Rating (1-5 stars) with OnSelectedIndexChanged handler in BaristaNotes/Pages/EditShotPage.cs
- [ ] T046 [US2] Add Entry for DrinkType with OnTextChanged handler in BaristaNotes/Pages/EditShotPage.cs
- [ ] T047 [US2] Implement RenderValidationErrors method to display validation error messages in BaristaNotes/Pages/EditShotPage.cs
- [ ] T048 [US2] Add Cancel button with navigation back to activity page in BaristaNotes/Pages/EditShotPage.cs
- [ ] T049 [US2] Add Save button with IsSaving state handling in BaristaNotes/Pages/EditShotPage.cs
- [ ] T050 [US2] Implement SaveChanges async method (create DTO, call UpdateShotAsync, handle success/errors) in BaristaNotes/Pages/EditShotPage.cs
- [ ] T051 [US2] Implement OnMessageAsync to handle Save and Cancel messages in BaristaNotes/Pages/EditShotPage.cs
- [ ] T052 [US2] Add ValidationException catch handler to display inline error messages in BaristaNotes/Pages/EditShotPage.cs
- [ ] T053 [US2] Add success toast notification using IFeedbackService after successful save in BaristaNotes/Pages/EditShotPage.cs
- [ ] T054 [US2] Add error toast notification using IFeedbackService for save failures in BaristaNotes/Pages/EditShotPage.cs
- [X] T055 [US2] Add NavigateToEdit method to ActivityFeedPage.cs to push EditShotPage with ShotId prop in BaristaNotes/Pages/
- [X] T056 [US2] Add SwipeItem for Edit action to shot card rendering in ActivityFeedPage.cs in BaristaNotes/Pages/
- [X] T057 [US2] Wire up SwipeItem Edit OnInvoked to NavigateToEdit in ActivityFeedPage.cs in BaristaNotes/Pages/
- [X] T058 [US2] Add RefreshAfterEdit message handling to reload shots after edit in ActivityFeedPage.cs in BaristaNotes/Pages/

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently - users can delete and edit shots

---

## Phase 5: User Story 3 - Access Edit/Delete Actions Quickly (Priority: P3)

**Goal**: Provide intuitive, efficient access to edit and delete actions via swipe gestures with clear visual feedback

**Independent Test**: Interact with shot entries in activity list → verify edit/delete accessible within 1-2 taps → verify visual feedback on interaction → test accessibility with touch targets

### Tests for User Story 3 (REQUIRED per Constitution) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T059 [P] [US3] UI test: SwipeView displays edit and delete actions on swipe in BaristaNotes.Tests/UI/ActivityFeedPageTests.cs
- [ ] T060 [P] [US3] UI test: Edit SwipeItem navigates to EditShotPage with correct ShotId in BaristaNotes.Tests/UI/ActivityFeedPageTests.cs
- [ ] T061 [P] [US3] UI test: Delete SwipeItem shows confirmation popup in BaristaNotes.Tests/UI/ActivityFeedPageTests.cs
- [ ] T062 [P] [US3] Accessibility test: SwipeItem touch targets are minimum 44x44px in BaristaNotes.Tests/UI/AccessibilityTests.cs
- [ ] T063 [P] [US3] Accessibility test: Screen reader announces swipe actions availability in BaristaNotes.Tests/UI/AccessibilityTests.cs

### Implementation for User Story 3

- [ ] T064 [P] [US3] Wrap shot card content with SwipeView in ActivityFeedPage.cs RenderShotCard method in BaristaNotes/Pages/
- [ ] T065 [P] [US3] Configure SwipeView with SwipeMode.Reveal for platform-appropriate behavior in BaristaNotes/Pages/ActivityFeedPage.cs
- [ ] T066 [P] [US3] Add SwipeItems container with Edit and Delete SwipeItem entries in BaristaNotes/Pages/ActivityFeedPage.cs
- [ ] T067 [US3] Set Edit SwipeItem BackgroundColor to theme blue color in BaristaNotes/Pages/ActivityFeedPage.cs
- [ ] T068 [US3] Set Delete SwipeItem BackgroundColor to theme red color in BaristaNotes/Pages/ActivityFeedPage.cs
- [ ] T069 [US3] Add accessibility labels to SwipeItems for screen reader support in BaristaNotes/Pages/ActivityFeedPage.cs
- [ ] T070 [US3] Verify SwipeItem touch targets meet 44x44px minimum requirement in BaristaNotes/Pages/ActivityFeedPage.cs
- [ ] T071 [US3] Add visual feedback animation/highlight on SwipeItem tap (if supported by platform) in BaristaNotes/Pages/ActivityFeedPage.cs

**Checkpoint**: All user stories should now be independently functional - complete, intuitive edit and delete workflow

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories and final quality gates

- [ ] T072 [P] Performance test: Verify edit form load <500ms per quickstart.md in BaristaNotes.Tests/Performance/
- [ ] T073 [P] Performance test: Verify save operation <2s per quickstart.md in BaristaNotes.Tests/Performance/
- [ ] T074 [P] Performance test: Verify delete operation <1s per quickstart.md in BaristaNotes.Tests/Performance/
- [ ] T075 [P] Performance test: Verify activity list refresh <1s per quickstart.md in BaristaNotes.Tests/Performance/
- [ ] T076 [P] Add XML documentation comments to UpdateShotAsync in ShotService.cs
- [ ] T077 [P] Add XML documentation comments to DeleteShotAsync in ShotService.cs
- [ ] T078 [P] Add XML documentation comments to UpdateShotDto.cs properties
- [ ] T079 Test edge case: Delete the only shot in activity list (verify empty state)
- [ ] T080 Test edge case: Navigate away from EditShotPage without saving (verify no changes)
- [ ] T081 Test edge case: Database operation fails during delete (verify error handling)
- [ ] T082 Test edge case: Database operation fails during update (verify error handling)
- [ ] T083 Test edge case: Validation errors display correctly on EditShotPage
- [ ] T084 Test on iOS device: Verify swipe gestures work correctly with platform conventions
- [ ] T085 Test on Android device: Verify swipe gestures work correctly with platform conventions
- [ ] T086 Accessibility audit: Test with iOS VoiceOver for screen reader support
- [ ] T087 Accessibility audit: Test with Android TalkBack for screen reader support
- [ ] T088 Code review: Verify MVU pattern consistency across EditShotPage and ActivityFeedPage
- [ ] T089 Code review: Verify error handling patterns follow existing conventions
- [ ] T090 Static analysis: Run .NET analyzers and resolve any warnings
- [ ] T091 Update IMPLEMENTATION_STATUS.md to mark edit/delete feature as complete
- [ ] T092 Run quickstart.md validation checklist for all phases
- [ ] T093 Constitution compliance verification: Code Quality Standards (Principle I)
- [ ] T094 Constitution compliance verification: Test-First Development (Principle II) - confirm 100% coverage
- [ ] T095 Constitution compliance verification: User Experience Consistency (Principle III)
- [ ] T096 Constitution compliance verification: Performance Requirements (Principle IV) - confirm all targets met

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-5)**: All depend on Foundational phase completion
  - User Story 1 (Delete): Can start after Foundational - No dependencies on other stories
  - User Story 2 (Edit): Can start after Foundational - No dependencies on other stories  
  - User Story 3 (UI Polish): Depends on US1 and US2 being complete (refines their UI)
- **Polish (Phase 6)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 3 (P3)**: Depends on US1 and US2 completion - Enhances their UI interaction patterns

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Service layer methods before UI components
- State/message enums before render logic
- Core implementation before integration with existing pages
- Story complete before moving to next priority

### Parallel Opportunities

- All Setup tasks (T001-T003) can run in parallel
- All Foundational tasks (T004-T009) can run in parallel within Phase 2
- Once Foundational phase completes, User Story 1 and User Story 2 can start in parallel (if team capacity allows)
- All tests for User Story 1 marked [P] (T010-T014) can run in parallel
- All tests for User Story 2 marked [P] (T025-T035) can run in parallel
- Several implementation tasks within US2 marked [P] (T036-T038) can run in parallel (different file sections)
- All Polish phase tasks marked [P] (T072-T078) can run in parallel

---

## Parallel Example: User Story 1 Tests

```bash
# Launch all tests for User Story 1 together (Red-Green-Refactor):
Task T010: "Unit test: DeleteShotAsync with valid ID soft deletes shot"
Task T011: "Unit test: DeleteShotAsync throws NotFoundException for invalid ID"
Task T012: "Unit test: DeleteShotAsync updates LastModifiedAt timestamp"
Task T013: "Integration test: Deleted shots do not appear in GetShotHistoryAsync results"
Task T014: "Integration test: DeleteShotAsync persists IsDeleted flag to database"

# All should FAIL initially - then implement T015-T024 to make them pass
```

---

## Parallel Example: User Story 2 Component Setup

```bash
# Launch component scaffolding tasks together:
Task T036: "Create EditShotPage.cs component file"
Task T037: "Define EditShotPageState class structure"
Task T038: "Define EditShotMessage enum"

# These can be done simultaneously by different developers or as parallel file edits
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T003)
2. Complete Phase 2: Foundational (T004-T009) - CRITICAL blocking phase
3. Complete Phase 3: User Story 1 (T010-T024)
4. **STOP and VALIDATE**: Test User Story 1 independently - delete shots with confirmation
5. Deploy/demo if ready - users can now correct mistakes by deleting shots

**MVP Deliverable**: Users can delete shot notes from activity page with confirmation dialog - addresses core error correction need

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready (T001-T009)
2. Add User Story 1 → Test independently → Deploy/Demo (MVP!) (T010-T024)
3. Add User Story 2 → Test independently → Deploy/Demo (T025-T058)
4. Add User Story 3 → Test independently → Deploy/Demo (T059-T071)
5. Add Polish → Final validation and quality gates (T072-T096)
6. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together (T001-T009)
2. Once Foundational is done:
   - Developer A: User Story 1 - Delete (T010-T024)
   - Developer B: User Story 2 - Edit (T025-T058)
3. Once US1 and US2 complete:
   - Developer C: User Story 3 - UI Polish (T059-T071)
4. Final phase: Team collaborates on Polish tasks (T072-T096)

---

## Validation Checklist

Before marking feature complete, verify:

- [ ] All 96 tasks completed and checked off
- [ ] All unit tests pass with 100% coverage for UpdateShotAsync and DeleteShotAsync
- [ ] All integration tests pass with database persistence verified
- [ ] All UI tests pass on iOS and Android
- [ ] Performance targets met: Edit load <500ms, Save <2s, Delete <1s, Refresh <1s
- [ ] Accessibility: Touch targets ≥44x44px, screen reader compatible
- [ ] Constitution compliance: All 4 principles verified (T093-T096)
- [ ] Static analysis clean: No warnings or errors
- [ ] Code review approved by peer
- [ ] Manual testing complete per quickstart.md
- [ ] Documentation updated (IMPLEMENTATION_STATUS.md)

---

## Notes

- [P] tasks = different files/sections, no dependencies on incomplete work
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Tests written first per TDD - must FAIL before implementation
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Performance targets are strict: measure with Stopwatch and verify
- Soft delete pattern maintains sync compatibility - never hard delete

---

## Summary

- **Total Tasks**: 96 tasks
- **MVP Scope**: Phase 1 + Phase 2 + Phase 3 (User Story 1) = 24 tasks
- **Full Feature**: All phases = 96 tasks
- **Parallel Opportunities**: 34 tasks marked [P] can run in parallel
- **User Stories**: 3 independent stories (Delete, Edit, UI Polish)
- **Test Coverage**: 28 test tasks (unit, integration, UI, accessibility, performance)
- **Estimated Time**: 4-6 hours per quickstart.md for experienced developer
- **Branch**: `001-edit-delete-shots`
- **Generated**: 2025-12-03

**Independent Test Criteria**:
- **US1**: Create shot → delete with confirmation → verify removed from list/database
- **US2**: Create shot → edit values → save → verify updated values persist
- **US3**: Swipe shot card → verify edit/delete accessible within 1-2 taps with visual feedback

**MVP First Strategy**: Implement just US1 (delete) for quickest value delivery, then incrementally add US2 (edit) and US3 (UI polish).
