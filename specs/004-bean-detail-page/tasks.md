# Tasks: Bean Detail Page

**Input**: Design documents from `/specs/004-bean-detail-page/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅, quickstart.md ✅

**Tests**: Tests are NOT explicitly requested in the feature specification. Test tasks are omitted per instructions.

**Quality Gates**: All tasks must pass constitution-mandated quality gates before merge: static analysis clean, code review approved, performance baseline met (<2s page load, <500ms shot history load).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and route registration

- [X] T001 Register "bean-detail" route in BaristaNotes/MauiProgram.cs
- [X] T002 [P] Create BeanDetailPageProps class in BaristaNotes/Pages/BeanDetailPage.cs
- [X] T003 [P] Create BeanDetailPageState class in BaristaNotes/Pages/BeanDetailPage.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core BeanDetailPage component skeleton that all user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 Create BeanDetailPage component skeleton with dependency injection in BaristaNotes/Pages/BeanDetailPage.cs
- [X] T005 Implement OnMounted lifecycle method with props-to-state initialization in BaristaNotes/Pages/BeanDetailPage.cs
- [X] T006 Implement base Render method with loading state in BaristaNotes/Pages/BeanDetailPage.cs
- [X] T007 Implement RenderForm method with all form fields (Name, Roaster, Origin, TrackRoastDate, RoastDate, Notes) in BaristaNotes/Pages/BeanDetailPage.cs

**Checkpoint**: Foundation ready - BeanDetailPage displays form fields but does not save or load data yet ✅

---

## Phase 3: User Story 1 - View and Edit Bean Details with Shot History (Priority: P1) 🎯 MVP

**Goal**: Enable viewing and editing existing beans with shot history display

**Independent Test**: Navigate to an existing bean from the bean list, verify the bean details are displayed in an editable form, verify the shot history list shows shots using that bean in reverse chronological order, edit bean details and save, confirm changes persist.

### Implementation for User Story 1

- [X] T008 [US1] Implement LoadBeanAsync method to fetch bean by ID in BaristaNotes/Pages/BeanDetailPage.cs
- [X] T009 [US1] Implement LoadShotsAsync method for initial shot history fetch in BaristaNotes/Pages/BeanDetailPage.cs
- [X] T010 [US1] Implement LoadMoreShotsAsync method for pagination in BaristaNotes/Pages/BeanDetailPage.cs
- [X] T011 [US1] Implement RenderShotHistory section with CollectionView in BaristaNotes/Pages/BeanDetailPage.cs
- [X] T012 [US1] Implement RenderShotItem method using ShotRecordCard component in BaristaNotes/Pages/BeanDetailPage.cs
- [X] T013 [US1] Implement RenderEmptyShots method for empty state display in BaristaNotes/Pages/BeanDetailPage.cs
- [X] T014 [US1] Implement ValidateForm method (name required, roast date not future) in BaristaNotes/Pages/BeanDetailPage.cs
- [X] T015 [US1] Implement SaveBeanAsync method for update flow in BaristaNotes/Pages/BeanDetailPage.cs
- [X] T016 [US1] Add error message display in form for validation and save errors in BaristaNotes/Pages/BeanDetailPage.cs
- [X] T017 [US1] Update BeanManagementPage to navigate to BeanDetailPage on bean item tap in BaristaNotes/Pages/BeanManagementPage.cs
- [X] T018 [US1] Remove ShowEditBeanSheet method from BeanManagementPage in BaristaNotes/Pages/BeanManagementPage.cs

**Checkpoint**: At this point, User Story 1 should be fully functional - users can view existing beans, see shot history, edit and save changes ✅

---

## Phase 4: User Story 2 - Add New Bean via Detail Page (Priority: P1)

**Goal**: Enable adding new beans using the full-page form instead of bottom sheet

**Independent Test**: Tap "Add" on the bean management page, verify navigation to empty bean detail page, enter bean details, save and confirm bean appears in the list.

### Implementation for User Story 2

- [X] T019 [US2] Extend SaveBeanAsync to handle create flow (no BeanId) in BaristaNotes/Pages/BeanDetailPage.cs
- [X] T020 [US2] Add Cancel button with navigation back in BaristaNotes/Pages/BeanDetailPage.cs
- [X] T021 [US2] Conditionally hide shot history section for new beans (BeanId null/0) in BaristaNotes/Pages/BeanDetailPage.cs
- [X] T022 [US2] Update BeanManagementPage toolbar "+ Add" to navigate to bean-detail route in BaristaNotes/Pages/BeanManagementPage.cs
- [X] T023 [US2] Remove ShowAddBeanSheet method from BeanManagementPage in BaristaNotes/Pages/BeanManagementPage.cs
- [X] T024 [US2] Remove OnBeanSaved method from BeanManagementPage in BaristaNotes/Pages/BeanManagementPage.cs

**Checkpoint**: At this point, User Stories 1 AND 2 should both work - users can create new beans and edit existing beans from full-page form ✅

---

## Phase 5: User Story 3 - Delete Bean from Detail Page (Priority: P2)

**Goal**: Enable deleting beans from the detail page with confirmation dialog

**Independent Test**: Navigate to an existing bean, tap Delete, confirm in the dialog, verify bean is removed from the list.

### Implementation for User Story 3

- [X] T025 [US3] Implement DeleteBeanAsync method with DisplayAlert confirmation in BaristaNotes/Pages/BeanDetailPage.cs
- [X] T026 [US3] Add Delete button conditionally rendered for edit mode only in BaristaNotes/Pages/BeanDetailPage.cs
- [X] T027 [US3] Remove inline delete button from bean list items in BaristaNotes/Pages/BeanManagementPage.cs

**Checkpoint**: At this point, Users can delete beans from the detail page with confirmation ✅

---

## Phase 6: User Story 4 - Navigate to Shot Detail from Bean Page (Priority: P3)

**Goal**: Enable navigation from shot history items to shot detail/edit view

**Independent Test**: On a bean detail page with shot history, tap a shot card, verify navigation to the shot logging page for that shot in edit mode.

### Implementation for User Story 4

- [X] T028 [US4] Implement NavigateToShotAsync method in BaristaNotes/Pages/BeanDetailPage.cs
- [X] T029 [US4] Add TapGestureRecognizer to RenderShotItem for shot navigation in BaristaNotes/Pages/BeanDetailPage.cs

**Checkpoint**: All user stories should now be independently functional ✅

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final cleanup and quality verification

- [X] T030 [P] Remove unused BeanFormSheet.cs import from BeanManagementPage if present in BaristaNotes/Pages/BeanManagementPage.cs
- [ ] T031 [P] Verify page load performance (<2s) by testing with existing bean with shots
- [ ] T032 [P] Verify shot history pagination works with bean having >20 shots
- [X] T033 Code cleanup - remove any unused methods or dead code in BaristaNotes/Pages/BeanManagementPage.cs
- [ ] T034 Run quickstart.md validation checklist
- [ ] T035 Constitution compliance verification (all 4 principles checked)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-6)**: All depend on Foundational phase completion
  - User Story 1 (P1): Can start after Foundational
  - User Story 2 (P1): Can start after Foundational (parallel with US1 possible)
  - User Story 3 (P2): Depends on US1 or US2 (needs edit mode to exist)
  - User Story 4 (P3): Depends on US1 (needs shot history to exist)
- **Polish (Phase 7)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2)
- **User Story 2 (P1)**: Can start after Foundational (Phase 2) - mostly parallel with US1
- **User Story 3 (P2)**: Requires BeanDetailPage to exist with edit mode
- **User Story 4 (P3)**: Requires shot history from US1 to be implemented

### Within Each User Story

- Form/state changes before service calls
- Service calls before navigation changes
- BeanDetailPage changes before BeanManagementPage changes

### Parallel Opportunities

- T002 and T003 can run in parallel (separate class definitions)
- T030, T031, T032 can run in parallel in Phase 7 (independent verifications)
- User Stories 1 and 2 are largely parallel (different parts of BeanDetailPage)

---

## Parallel Example: Setup Phase

```bash
# Launch these together:
Task T002: "Create BeanDetailPageProps class in BaristaNotes/Pages/BeanDetailPage.cs"
Task T003: "Create BeanDetailPageState class in BaristaNotes/Pages/BeanDetailPage.cs"
```

## Parallel Example: User Stories 1 & 2 (after Foundational)

```bash
# Can work on simultaneously (different methods, minimal overlap):
Task T008-T016: User Story 1 - Load/Edit/Shot History
Task T019-T024: User Story 2 - Create/Cancel/Navigation
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2)

1. Complete Phase 1: Setup (T001-T003)
2. Complete Phase 2: Foundational (T004-T007)
3. Complete Phase 3: User Story 1 (T008-T018)
4. Complete Phase 4: User Story 2 (T019-T024)
5. **STOP and VALIDATE**: Test view/edit/create flows
6. Deploy/demo MVP

### Incremental Delivery

1. Complete Setup + Foundational → Form displays
2. Add User Story 1 → View/Edit/Shot History works
3. Add User Story 2 → Create new beans works (MVP Complete!)
4. Add User Story 3 → Delete from detail page works
5. Add User Story 4 → Shot navigation works
6. Polish phase → Final quality checks

---

## Files Summary

| File | Action | Tasks |
|------|--------|-------|
| `BaristaNotes/MauiProgram.cs` | MODIFY | T001 |
| `BaristaNotes/Pages/BeanDetailPage.cs` | CREATE | T002-T016, T019-T021, T025-T026, T028-T029 |
| `BaristaNotes/Pages/BeanManagementPage.cs` | MODIFY | T017-T018, T022-T024, T027, T030, T033 |

---

## Notes

- [P] tasks = different files or independent operations
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- BeanFormSheet.cs can be retained for reference but is no longer used after this feature
