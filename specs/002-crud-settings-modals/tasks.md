# Tasks: CRUD Settings with Modal Bottom Sheets

**Input**: Design documents from `/specs/002-crud-settings-modals/`  
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅

**Quality Gates**: All tasks must pass constitution-mandated quality gates before merge: tests pass, static analysis clean, code review approved, performance baseline met (<300ms modals, <500ms lists, <1s saves).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Include exact file paths in descriptions

## Path Conventions

- **MAUI App**: `BaristaNotes/` (main app project)
- **Core Library**: `BaristaNotes.Core/` (services, models, data)
- **Tests**: `BaristaNotes.Tests/` (unit and integration tests)

---

## Phase 1: Setup (Project Dependencies)

**Purpose**: Add Plugin.Maui.BottomSheet package and configure infrastructure

- [X] T001 Add Plugin.Maui.BottomSheet NuGet package to BaristaNotes/BaristaNotes.csproj
- [X] T002 Register `.UseBottomSheet()` in BaristaNotes/MauiProgram.cs

---

## Phase 2: Foundational (Bottom Sheet Infrastructure)

**Purpose**: Core bottom sheet infrastructure that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T003 Create Components/Modals directory in BaristaNotes/Components/Modals/
- [X] T004 Create BottomSheet scaffold wrapper in BaristaNotes/Components/Modals/BottomSheet.cs
- [X] T005 [P] Create BottomSheetExtensions in BaristaNotes/Components/Modals/BottomSheetExtensions.cs
- [X] T006 [P] Create ConfirmDeleteComponent in BaristaNotes/Components/Modals/ConfirmDeleteComponent.cs
- [ ] T007 Create Components/Forms directory in BaristaNotes/Components/Forms/
- [ ] T008 Verify bottom sheet opens and closes correctly (manual test)

**Checkpoint**: Bottom sheet infrastructure ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Primary Activity Feed Navigation (Priority: P1) 🎯 MVP

**Goal**: Activity Feed is the primary landing page when app launches

**Independent Test**: Launch app and verify Activity Feed displays as primary view

### Implementation for User Story 1

- [X] T009 [US1] Modify AppShell.cs to make ActivityFeedPage the first/primary tab in BaristaNotes/AppShell.cs
- [X] T010 [US1] Reduce tab bar to 2 tabs (History/Activity Feed, Shot Log) in BaristaNotes/AppShell.cs
- [X] T011 [US1] Update tab icons and labels for simplified navigation in BaristaNotes/AppShell.cs
- [ ] T012 [US1] Verify Activity Feed loads as primary view on app launch (manual test)

**Checkpoint**: User Story 1 complete - Activity Feed is primary page, 2-tab navigation working

---

## Phase 4: User Story 2 - Access Settings via Toolbar (Priority: P1) 🎯 MVP

**Goal**: Settings accessible via toolbar item on Activity Feed, not tab bar

**Independent Test**: Tap settings toolbar item and verify Settings page opens

### Implementation for User Story 2

- [X] T013 [P] [US2] Create SettingsPage.cs in BaristaNotes/Pages/SettingsPage.cs
- [X] T014 [US2] Add toolbar item (settings icon) to ActivityFeedPage in BaristaNotes/Pages/ActivityFeedPage.cs
- [X] T015 [US2] Register "settings" route in Shell for SettingsPage navigation in BaristaNotes/AppShell.cs
- [ ] T016 [P] [US2] Add settings.png icon to BaristaNotes/Resources/Images/ (if not exists)
- [X] T017 [US2] Implement navigation from toolbar item to SettingsPage in BaristaNotes/Pages/ActivityFeedPage.cs
- [X] T018 [US2] Add navigation options on SettingsPage for Equipment, Beans, Profiles in BaristaNotes/Pages/SettingsPage.cs
- [X] T019 [US2] Register routes for equipment, beans, profiles pages in BaristaNotes/AppShell.cs
- [ ] T020 [US2] Verify settings navigation flow works end-to-end (manual test)

**Checkpoint**: User Story 2 complete - Settings accessible via toolbar, navigation to management pages works

---

## Phase 5: User Story 3 - Manage Equipment via Bottom Sheet Modal (Priority: P2)

**Goal**: Full CRUD operations for Equipment using bottom sheet modals

**Independent Test**: Navigate to Settings → Equipment, perform add/edit/delete operations

### Implementation for User Story 3

- [X] T021 [P] [US3] Create EquipmentFormComponent in BaristaNotes/Components/Forms/EquipmentFormComponent.cs (implemented inline in page)
- [X] T022 [US3] Implement form state and validation in EquipmentFormComponent
- [X] T023 [US3] Implement create equipment flow in EquipmentFormComponent (call IEquipmentService.CreateEquipmentAsync)
- [X] T024 [US3] Implement update equipment flow in EquipmentFormComponent (call IEquipmentService.UpdateEquipmentAsync)
- [X] T025 [US3] Modify EquipmentManagementPage to inject IEquipmentService in BaristaNotes/Pages/EquipmentManagementPage.cs
- [X] T026 [US3] Add "Add Equipment" button to EquipmentManagementPage that opens bottom sheet
- [X] T027 [US3] Add tap handler on equipment list items to open edit bottom sheet
- [X] T028 [US3] Implement delete flow with ConfirmDelete in EquipmentManagementPage
- [X] T029 [US3] Add loading state and error handling to EquipmentManagementPage
- [ ] T030 [US3] Verify full Equipment CRUD cycle works (manual test)

**Checkpoint**: User Story 3 complete - Equipment CRUD via bottom sheets working

---

## Phase 6: User Story 4 - Manage Beans via Bottom Sheet Modal (Priority: P2)

**Goal**: Full CRUD operations for Beans using bottom sheet modals

**Independent Test**: Navigate to Settings → Beans, perform add/edit/delete operations

### Implementation for User Story 4

- [X] T031 [P] [US4] Create BeanFormComponent in BaristaNotes/Components/Forms/BeanFormComponent.cs (implemented inline in page)
- [X] T032 [US4] Implement form state and validation in BeanFormComponent (including DatePicker for RoastDate)
- [X] T033 [US4] Implement create bean flow in BeanFormComponent (call IBeanService.CreateBeanAsync)
- [X] T034 [US4] Implement update bean flow in BeanFormComponent (call IBeanService.UpdateBeanAsync)
- [X] T035 [US4] Modify BeanManagementPage to inject IBeanService in BaristaNotes/Pages/BeanManagementPage.cs
- [X] T036 [US4] Add "Add Bean" button to BeanManagementPage that opens bottom sheet
- [X] T037 [US4] Add tap handler on bean list items to open edit bottom sheet
- [X] T038 [US4] Implement delete flow with ConfirmDelete in BeanManagementPage
- [X] T039 [US4] Add loading state and error handling to BeanManagementPage
- [ ] T040 [US4] Verify full Bean CRUD cycle works (manual test)

**Checkpoint**: User Story 4 complete - Bean CRUD via bottom sheets working

---

## Phase 7: User Story 5 - Manage User Profiles via Bottom Sheet Modal (Priority: P2)

**Goal**: Full CRUD operations for User Profiles using bottom sheet modals

**Independent Test**: Navigate to Settings → User Profiles, perform add/edit/delete operations

### Implementation for User Story 5

- [X] T041 [P] [US5] Create UserProfileFormComponent in BaristaNotes/Components/Forms/UserProfileFormComponent.cs (implemented inline in page)
- [X] T042 [US5] Implement form state and validation in UserProfileFormComponent
- [X] T043 [US5] Implement create profile flow in UserProfileFormComponent (call IUserProfileService.CreateProfileAsync)
- [X] T044 [US5] Implement update profile flow in UserProfileFormComponent (call IUserProfileService.UpdateProfileAsync)
- [X] T045 [US5] Modify UserProfileManagementPage to inject IUserProfileService in BaristaNotes/Pages/UserProfileManagementPage.cs
- [X] T046 [US5] Add "Add Profile" button to UserProfileManagementPage that opens bottom sheet
- [X] T047 [US5] Add tap handler on profile list items to open edit bottom sheet
- [X] T048 [US5] Implement "last profile" protection - prevent delete when only 1 profile exists
- [X] T049 [US5] Implement delete flow with ConfirmDelete in UserProfileManagementPage
- [X] T050 [US5] Add loading state and error handling to UserProfileManagementPage
- [ ] T051 [US5] Verify full UserProfile CRUD cycle works including last-profile protection (manual test)

**Checkpoint**: User Story 5 complete - User Profile CRUD via bottom sheets working with protection

---

## Phase 8: User Story 6 - Simplified Tab Navigation (Priority: P3)

**Goal**: Clean tab bar with only essential navigation items

**Independent Test**: View tab bar and confirm only Shot Log and History tabs are present

### Implementation for User Story 6

- [X] T052 [US6] Remove Equipment, Beans, Profiles from tab bar in BaristaNotes/AppShell.cs (already done in T010)
- [ ] T053 [US6] Verify tab bar shows only 2 items with correct icons and labels (manual test)
- [X] T054 [US6] Update any deep links or navigation references that assumed old tab structure

**Checkpoint**: User Story 6 complete - Simplified 2-tab navigation confirmed

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Final quality improvements and validation

- [ ] T055 [P] Add unit tests for EquipmentFormComponent validation logic in BaristaNotes.Tests/Unit/
- [ ] T056 [P] Add unit tests for BeanFormComponent validation logic in BaristaNotes.Tests/Unit/
- [ ] T057 [P] Add unit tests for UserProfileFormComponent validation logic in BaristaNotes.Tests/Unit/
- [ ] T058 Add integration test for Equipment CRUD flow in BaristaNotes.Tests/Integration/EquipmentCrudTests.cs
- [ ] T059 [P] Add integration test for Bean CRUD flow in BaristaNotes.Tests/Integration/BeanCrudTests.cs
- [ ] T060 [P] Add integration test for UserProfile CRUD flow in BaristaNotes.Tests/Integration/UserProfileCrudTests.cs
- [ ] T061 Performance validation: verify modals open <300ms, lists load <500ms, saves complete <1s
- [ ] T062 Accessibility audit: verify touch targets ≥44x44px, screen reader support
- [ ] T063 Code cleanup and ensure consistent styling across all form components
- [ ] T064 Run quickstart.md validation checklist
- [ ] T065 Constitution compliance verification (all 4 principles checked)

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1: Setup ─────────────────────────────────────────┐
                                                         │
Phase 2: Foundational (Bottom Sheet Infrastructure) ◄───┘
         ⚠️ BLOCKS all user stories                      
                    │
                    ▼
    ┌───────────────┼───────────────┐
    │               │               │
    ▼               ▼               ▼
Phase 3: US1    Phase 4: US2    Phase 5-7: US3-5
(Primary Feed)  (Settings)      (CRUD Modals)
P1 - MVP        P1 - MVP        P2 - Post-MVP
    │               │               │
    └───────────────┼───────────────┘
                    ▼
            Phase 8: US6 (P3)
            (Tab Cleanup)
                    │
                    ▼
            Phase 9: Polish
```

### User Story Dependencies

| Story | Priority | Can Start After | Dependencies on Other Stories |
|-------|----------|-----------------|-------------------------------|
| US1 | P1 | Phase 2 complete | None |
| US2 | P1 | Phase 2 complete | None (but US1 provides context) |
| US3 | P2 | Phase 2 complete | US2 (needs Settings page) |
| US4 | P2 | Phase 2 complete | US2 (needs Settings page) |
| US5 | P2 | Phase 2 complete | US2 (needs Settings page) |
| US6 | P3 | US1 complete | US1 (verifies tab changes) |

### Parallel Opportunities by Phase

**Phase 1 (Setup)**: T001, T002 must be sequential

**Phase 2 (Foundational)**:
```bash
# After T003, T004:
T005 [P] BottomSheetExtensions.cs
T006 [P] ConfirmDeleteComponent.cs
```

**Phase 3-7 (User Stories)**: After Phase 2, these can run in parallel:
```bash
# US1 + US2 can run in parallel (both P1)
# US3, US4, US5 can run in parallel (all P2, all need forms)
```

**Phase 9 (Polish)**:
```bash
# All test tasks marked [P] can run in parallel
T055 [P] Equipment validation tests
T056 [P] Bean validation tests  
T057 [P] UserProfile validation tests
T059 [P] Bean CRUD integration test
T060 [P] UserProfile CRUD integration test
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2 Only)

1. Complete Phase 1: Setup (NuGet, MauiProgram)
2. Complete Phase 2: Foundational (BottomSheet infrastructure)
3. Complete Phase 3: User Story 1 (Activity Feed primary)
4. Complete Phase 4: User Story 2 (Settings toolbar)
5. **STOP and VALIDATE**: Test navigation flow independently
6. Deploy/demo if ready - basic navigation working!

### Full Feature Delivery

1. Complete MVP above
2. Add Phase 5: User Story 3 (Equipment CRUD) → Test → Demo
3. Add Phase 6: User Story 4 (Bean CRUD) → Test → Demo
4. Add Phase 7: User Story 5 (Profile CRUD) → Test → Demo
5. Add Phase 8: User Story 6 (Tab cleanup verification)
6. Complete Phase 9: Polish & Tests

---

## Summary

| Metric | Value |
|--------|-------|
| **Total Tasks** | 65 |
| **Setup Phase** | 2 tasks |
| **Foundational Phase** | 6 tasks |
| **US1 (Primary Feed)** | 4 tasks |
| **US2 (Settings Toolbar)** | 8 tasks |
| **US3 (Equipment CRUD)** | 10 tasks |
| **US4 (Bean CRUD)** | 10 tasks |
| **US5 (Profile CRUD)** | 11 tasks |
| **US6 (Tab Cleanup)** | 3 tasks |
| **Polish Phase** | 11 tasks |
| **Parallel Opportunities** | 17 tasks marked [P] |
| **MVP Scope** | US1 + US2 (14 tasks after foundational) |

### Independent Test Criteria per Story

| Story | Independent Test |
|-------|-----------------|
| US1 | Launch app → Activity Feed is primary view |
| US2 | Tap toolbar settings icon → Settings page opens |
| US3 | Settings → Equipment → Add/Edit/Delete equipment |
| US4 | Settings → Beans → Add/Edit/Delete beans |
| US5 | Settings → Profiles → Add/Edit/Delete profiles (last profile blocked) |
| US6 | Tab bar shows only 2 tabs |

### Format Validation ✅

All 65 tasks follow the required checklist format:
- ✅ Checkbox prefix `- [ ]`
- ✅ Task ID (T001-T065)
- ✅ `[P]` marker where applicable (17 tasks)
- ✅ `[Story]` label for user story tasks (US1-US6)
- ✅ File paths included in descriptions
