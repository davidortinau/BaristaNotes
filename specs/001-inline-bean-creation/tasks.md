# Tasks: Inline Bean Creation During Shot Logging

**Input**: Design documents from `/specs/001-inline-bean-creation/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, quickstart.md

**Tests**: Tests are OPTIONAL for this feature per user decision. Integration tests planned for Phase 5 (Polish).

**Quality Gates**: All tasks must pass constitution-mandated quality gates before merge: static analysis clean, code review approved, performance baseline met.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **MAUI App**: `BaristaNotes/` for UI components
- **Core**: `BaristaNotes.Core/` for services and models
- **Tests**: `BaristaNotes.Tests/` for test projects

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add state tracking for beans in ShotLoggingPage

- [X] T001 Add `AvailableBeans` property to `ShotLoggingState` class in BaristaNotes/Pages/ShotLoggingPage.cs
- [X] T002 Load beans in `LoadDataAsync` method using existing `_beanService.GetAllActiveBeansAsync()` in BaristaNotes/Pages/ShotLoggingPage.cs

**Checkpoint**: ShotLoggingPage now tracks available beans alongside bags

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Create the popup components that all user stories depend on

**⚠️ CRITICAL**: No user story work can begin until popup components exist

- [X] T003 [P] Create `BeanCreationPopup` class with form fields (Name, Roaster, Origin, Notes) in BaristaNotes/Integrations/Popups/BeanCreationPopup.cs
- [X] T004 [P] Create `BagCreationPopup` class with form fields (BeanId, BeanName display, RoastDate, Notes) in BaristaNotes/Integrations/Popups/BagCreationPopup.cs
- [X] T005 Add `OnBeanCreated` callback (Action<BeanDto>) to BeanCreationPopup in BaristaNotes/Integrations/Popups/BeanCreationPopup.cs
- [X] T006 Add `OnBagCreated` callback (Action<BagSummaryDto>) to BagCreationPopup in BaristaNotes/Integrations/Popups/BagCreationPopup.cs
- [X] T007 Implement save logic in BeanCreationPopup using IBeanService.CreateBeanAsync in BaristaNotes/Integrations/Popups/BeanCreationPopup.cs
- [X] T008 Implement save logic in BagCreationPopup using IBagService.CreateBagAsync in BaristaNotes/Integrations/Popups/BagCreationPopup.cs
- [X] T009 Add inline validation error display in BeanCreationPopup (name required) in BaristaNotes/Integrations/Popups/BeanCreationPopup.cs
- [X] T010 Add inline validation error display in BagCreationPopup (roast date not in future) in BaristaNotes/Integrations/Popups/BagCreationPopup.cs

**Checkpoint**: Foundation ready - popup components can be used by all user stories

---

## Phase 3: User Story 1 - Create Bean When No Beans Exist (Priority: P1) 🎯 MVP

**Goal**: New users with no beans see an empty state with "Create Bean" CTA, which opens a modal form to create their first bean, then automatically prompts to create a bag

**Independent Test**: Launch app with empty database, navigate to shot logging, verify user can create bean and bag inline

### Implementation for User Story 1

- [X] T011 [US1] Add empty state detection logic in `Render()` method: check if `AvailableBeans.Count == 0` in BaristaNotes/Pages/ShotLoggingPage.cs
- [X] T012 [US1] Create `RenderNoBeanEmptyState()` method with "No beans configured" message and "Create Bean" button in BaristaNotes/Pages/ShotLoggingPage.cs
- [X] T013 [US1] Implement `ShowBeanCreationPopup()` async method to push BeanCreationPopup via IPopupService in BaristaNotes/Pages/ShotLoggingPage.cs
- [X] T014 [US1] Implement `HandleBeanCreated(BeanDto)` callback method in BaristaNotes/Pages/ShotLoggingPage.cs
- [X] T015 [US1] In HandleBeanCreated: dismiss bean popup and immediately show BagCreationPopup with new BeanId in BaristaNotes/Pages/ShotLoggingPage.cs
- [X] T016 [US1] Implement `HandleBagCreated(BagSummaryDto)` callback method in BaristaNotes/Pages/ShotLoggingPage.cs
- [X] T017 [US1] In HandleBagCreated: call LoadDataAsync and SetState to auto-select new bag in BaristaNotes/Pages/ShotLoggingPage.cs

**Checkpoint**: User Story 1 complete - new users can create bean → bag inline and proceed to log shots

---

## Phase 4: User Story 2 - Create Bag When Beans Exist But No Active Bags (Priority: P2)

**Goal**: Users with beans but no active bags can create a bag with bean selection from shot logging page

**Independent Test**: Have beans in system but mark all bags complete, verify user can create new bag with bean picker

### Implementation for User Story 2

- [X] T018 [US2] Update existing "No active bags" empty state to show enhanced message in BaristaNotes/Pages/ShotLoggingPage.cs
- [X] T019 [US2] Modify "Add New Bag" button click handler to show BagCreationPopup with bean picker mode in BaristaNotes/Pages/ShotLoggingPage.cs
- [X] T020 [US2] Add `AvailableBeans` prop and bean picker (Picker control) to BagCreationPopup for multi-bean mode in BaristaNotes/Integrations/Popups/BagCreationPopup.cs
- [X] T021 [US2] Conditionally show bean picker in BagCreationPopup when BeanId is not pre-set in BaristaNotes/Integrations/Popups/BagCreationPopup.cs
- [X] T022 [US2] Wire HandleBagCreated callback for existing "Add New Bag" flow in BaristaNotes/Pages/ShotLoggingPage.cs

**Checkpoint**: User Story 2 complete - users with beans can create bags via modal

---

## Phase 5: User Story 3 - Seamless Flow Continuation After Creation (Priority: P3)

**Goal**: After inline creation, the newly created bag is automatically selected in the bag picker

**Independent Test**: Create bean + bag inline, verify bag picker shows and has new bag selected without manual interaction

### Implementation for User Story 3

- [X] T023 [US3] Ensure `LoadDataAsync` refreshes both AvailableBeans and AvailableBags lists in BaristaNotes/Pages/ShotLoggingPage.cs
- [X] T024 [US3] In HandleBagCreated: find new bag index in refreshed list and set SelectedBagIndex in BaristaNotes/Pages/ShotLoggingPage.cs
- [X] T025 [US3] Verify bag picker scrolls to/highlights selected bag after auto-selection in BaristaNotes/Pages/ShotLoggingPage.cs

**Checkpoint**: All user stories complete - full inline creation flow with auto-selection

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories and final quality gates

- [X] T026 [P] Add loading state (IsSaving) to BeanCreationPopup and disable button during save in BaristaNotes/Integrations/Popups/BeanCreationPopup.cs
- [X] T027 [P] Add loading state (IsSaving) to BagCreationPopup and disable button during save in BaristaNotes/Integrations/Popups/BagCreationPopup.cs
- [X] T028 [P] Ensure Cancel button works correctly in both popups (dismisses without action) in BaristaNotes/Integrations/Popups/
- [X] T029 [P] Apply ThemeKeys styling to popup form fields for consistency in BaristaNotes/Integrations/Popups/
- [X] T030 Verify 44x44px touch targets on all popup buttons in BaristaNotes/Integrations/Popups/
- [X] T031 Build and verify no compilation errors
- [ ] T032 Manual test: complete inline bean → bag flow on iOS simulator
- [ ] T033 Manual test: complete "Add New Bag" flow when beans exist

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-5)**: All depend on Foundational phase completion
  - User stories should proceed sequentially (P1 → P2 → P3) as they build on each other
- **Polish (Phase 6)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - Core MVP flow
- **User Story 2 (P2)**: Depends on US1 completion (reuses HandleBagCreated, popup components)
- **User Story 3 (P3)**: Depends on US1 + US2 completion (auto-selection polish)

### Within Each Phase

- Tasks marked [P] can run in parallel (different files)
- Complete popup components before wiring to ShotLoggingPage
- Create callback signatures before implementing callback handlers

### Parallel Opportunities

Within Foundational Phase:
```bash
# Can run in parallel (different files):
T003: BeanCreationPopup.cs
T004: BagCreationPopup.cs
```

Within Polish Phase:
```bash
# Can run in parallel (different files):
T026: BeanCreationPopup loading state
T027: BagCreationPopup loading state
T028: Cancel button handling
T029: ThemeKeys styling
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T002)
2. Complete Phase 2: Foundational (T003-T010)
3. Complete Phase 3: User Story 1 (T011-T017)
4. **STOP and VALIDATE**: Test bean → bag flow manually
5. Build and deploy if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test manually → **MVP complete!**
3. Add User Story 2 → Test manually → Enhanced bag creation
4. Add User Story 3 → Test manually → Polish complete
5. Complete Phase 6 → Quality gates met

---

## Notes

- All popup classes extend UXDivers.Popups.Maui base classes
- Use `IPopupService.Current.PushAsync()` / `PopAsync()` for popup management
- Callbacks use `Action<T>` pattern (not events) per existing codebase patterns
- BagCreationPopup has two modes: preset BeanId (from US1) or bean picker (from US2)
- Commit after each completed task
- Stop at any checkpoint to validate functionality
