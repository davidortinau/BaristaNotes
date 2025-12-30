# Tasks: Shot History Filter

**Input**: Design documents from `/specs/001-shot-history-filter/`  
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Test tasks included per Constitution Principle II (Test-First Development). Tests must be written FIRST, must FAIL before implementation, and require 80% minimum coverage.

**Quality Gates**: All tasks must pass constitution-mandated quality gates before merge: tests pass, static analysis clean, code review approved, performance baseline met.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Core library**: `BaristaNotes.Core/`
- **App**: `BaristaNotes/`
- **Tests**: `BaristaNotes.Tests/`

---

## Phase 1: Setup

**Purpose**: Create new files and establish filter infrastructure

- [x] T001 [P] Create ShotFilterCriteriaDto record in BaristaNotes.Core/DTOs/ShotFilterCriteriaDto.cs
- [x] T002 [P] Create ShotFilterCriteria UI model in BaristaNotes/Models/ShotFilterCriteria.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core repository and service infrastructure that MUST be complete before UI work

**⚠️ CRITICAL**: No user story UI work can begin until this phase is complete

- [x] T003 Add GetFilteredAsync method signature to IShotRecordRepository in BaristaNotes.Core/Data/Repositories/IShotRecordRepository.cs
- [x] T004 Add GetFilteredCountAsync method signature to IShotRecordRepository in BaristaNotes.Core/Data/Repositories/IShotRecordRepository.cs
- [x] T005 Add GetBeanIdsWithShotsAsync method signature to IShotRecordRepository in BaristaNotes.Core/Data/Repositories/IShotRecordRepository.cs
- [x] T006 Add GetMadeForIdsWithShotsAsync method signature to IShotRecordRepository in BaristaNotes.Core/Data/Repositories/IShotRecordRepository.cs
- [x] T007 Implement GetFilteredAsync in ShotRecordRepository in BaristaNotes.Core/Data/Repositories/ShotRecordRepository.cs
- [x] T008 Implement GetFilteredCountAsync in ShotRecordRepository in BaristaNotes.Core/Data/Repositories/ShotRecordRepository.cs
- [x] T009 Implement GetBeanIdsWithShotsAsync in ShotRecordRepository in BaristaNotes.Core/Data/Repositories/ShotRecordRepository.cs
- [x] T010 Implement GetMadeForIdsWithShotsAsync in ShotRecordRepository in BaristaNotes.Core/Data/Repositories/ShotRecordRepository.cs
- [x] T011 Add GetFilteredShotHistoryAsync method signature to IShotService in BaristaNotes/Services/IShotService.cs
- [x] T012 Add GetBeansWithShotsAsync method signature to IShotService in BaristaNotes/Services/IShotService.cs
- [x] T013 Add GetPeopleWithShotsAsync method signature to IShotService in BaristaNotes/Services/IShotService.cs
- [x] T014 Implement GetFilteredShotHistoryAsync in ShotService in BaristaNotes/Services/ShotService.cs
- [x] T015 Implement GetBeansWithShotsAsync in ShotService in BaristaNotes/Services/ShotService.cs
- [x] T016 Implement GetPeopleWithShotsAsync in ShotService in BaristaNotes/Services/ShotService.cs

**Checkpoint**: Repository and service infrastructure ready - UI implementation can now begin

---

## Phase 3: User Story 1 - Filter by Bean (Priority: P1) 🎯 MVP

**Goal**: Users can filter shot history by selecting one or more beans

**Independent Test**: Select a bean filter, verify only shots with that bean appear in the list

### Tests for User Story 1

- [ ] T017 [P] [US1] Create integration test GetFilteredAsync_ByBean_ReturnsOnlyMatchingShots in BaristaNotes.Tests/Integration/ShotRecordRepositoryFilterTests.cs
- [ ] T018 [P] [US1] Create integration test GetFilteredAsync_ByBean_NoMatches_ReturnsEmpty in BaristaNotes.Tests/Integration/ShotRecordRepositoryFilterTests.cs
- [ ] T019 [P] [US1] Create unit test GetFilteredShotHistoryAsync_ByBean_MapsToDto in BaristaNotes.Tests/Unit/ShotFilterTests.cs

### Implementation for User Story 1

- [x] T020 [US1] Create ShotFilterPopup base structure extending ActionModalPopup in BaristaNotes/Integrations/Popups/ShotFilterPopup.cs
- [x] T021 [US1] Implement bean selection section with toggle chips in ShotFilterPopup in BaristaNotes/Integrations/Popups/ShotFilterPopup.cs
- [x] T022 [US1] Add filter state (ActiveFilters, TotalShotCount, FilteredShotCount) to ActivityFeedState in BaristaNotes/Pages/ActivityFeedPage.cs
- [x] T023 [US1] Add Filter ToolbarItem with MaterialSymbolsFont.Filter_list icon to ActivityFeedPage in BaristaNotes/Pages/ActivityFeedPage.cs
- [x] T024 [US1] Implement OpenFilterPopup method to load beans and show popup in BaristaNotes/Pages/ActivityFeedPage.cs
- [x] T025 [US1] Implement OnFiltersApplied callback to update state and reload data in BaristaNotes/Pages/ActivityFeedPage.cs
- [x] T026 [US1] Update LoadShotsAsync to use GetFilteredShotHistoryAsync when filters active in BaristaNotes/Pages/ActivityFeedPage.cs
- [x] T027 [US1] Add result count display "Showing X of Y shots" when filtered in BaristaNotes/Pages/ActivityFeedPage.cs
- [x] T028 [US1] Add empty state for no matching shots with clear filters option in BaristaNotes/Pages/ActivityFeedPage.cs

**Checkpoint**: Bean filtering fully functional - can demo MVP with single filter type

---

## Phase 4: User Story 2 - Filter by Made For (Priority: P2)

**Goal**: Users can filter shot history by who the shot was made for

**Independent Test**: Select a person filter, verify only shots made for that person appear

### Tests for User Story 2

- [ ] T029 [P] [US2] Create integration test GetFilteredAsync_ByMadeFor_ReturnsOnlyMatchingShots in BaristaNotes.Tests/Integration/ShotRecordRepositoryFilterTests.cs
- [ ] T030 [P] [US2] Create unit test GetFilteredShotHistoryAsync_ByMadeFor_MapsToDto in BaristaNotes.Tests/Unit/ShotFilterTests.cs

### Implementation for User Story 2

- [x] T031 [US2] Implement "Made For" selection section with toggle chips in ShotFilterPopup in BaristaNotes/Integrations/Popups/ShotFilterPopup.cs
- [x] T032 [US2] Update OpenFilterPopup to load people via GetPeopleWithShotsAsync in BaristaNotes/Pages/ActivityFeedPage.cs

**Checkpoint**: Bean AND Made For filtering both work independently

---

## Phase 5: User Story 3 - Filter by Rating (Priority: P2)

**Goal**: Users can filter shot history by rating value (0-4)

**Independent Test**: Select a rating filter, verify only shots with that rating appear

### Tests for User Story 3

- [ ] T033 [P] [US3] Create integration test GetFilteredAsync_ByRating_ReturnsOnlyMatchingShots in BaristaNotes.Tests/Integration/ShotRecordRepositoryFilterTests.cs
- [ ] T034 [P] [US3] Create unit test GetFilteredShotHistoryAsync_ByRating_MapsToDto in BaristaNotes.Tests/Unit/ShotFilterTests.cs

### Implementation for User Story 3

- [x] T035 [US3] Implement rating selection section with sentiment icons (0-4) in ShotFilterPopup in BaristaNotes/Integrations/Popups/ShotFilterPopup.cs

**Checkpoint**: All three single-filter types work independently

---

## Phase 6: User Story 4 - Multiple Filters (Priority: P3)

**Goal**: Users can combine bean + made for + rating filters (AND logic)

**Independent Test**: Apply multiple filters, verify only shots matching ALL criteria appear

### Tests for User Story 4

- [ ] T036 [P] [US4] Create integration test GetFilteredAsync_MultipleFilters_AppliesAndLogic in BaristaNotes.Tests/Integration/ShotRecordRepositoryFilterTests.cs
- [ ] T037 [P] [US4] Create integration test GetFilteredAsync_AllThreeFilters_ReturnsCorrectResults in BaristaNotes.Tests/Integration/ShotRecordRepositoryFilterTests.cs

### Implementation for User Story 4

- [x] T038 [US4] Add visual indication of active filters in popup (highlight selected items) in BaristaNotes/Integrations/Popups/ShotFilterPopup.cs
- [x] T039 [US4] Add filter count badge/highlight to toolbar button when filters active in BaristaNotes/Pages/ActivityFeedPage.cs

**Checkpoint**: Multi-filter combinations work correctly with AND logic

---

## Phase 7: User Story 5 - Clear/Reset Filters (Priority: P3)

**Goal**: Users can clear all filters with a single action

**Independent Test**: Apply filters, tap clear, verify all shots reappear

### Tests for User Story 5

- [ ] T040 [P] [US5] Create unit test ShotFilterCriteria_Clear_RemovesAllSelections in BaristaNotes.Tests/Unit/ShotFilterTests.cs

### Implementation for User Story 5

- [x] T041 [US5] Add "Clear All" button to ShotFilterPopup in BaristaNotes/Integrations/Popups/ShotFilterPopup.cs
- [x] T042 [US5] Implement OnFiltersCleared callback in ActivityFeedPage in BaristaNotes/Pages/ActivityFeedPage.cs
- [x] T043 [US5] Disable/hide Clear button when no filters are active in BaristaNotes/Integrations/Popups/ShotFilterPopup.cs

**Checkpoint**: Full filter lifecycle (apply, modify, clear) works correctly

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Final quality, accessibility, and performance verification

- [ ] T044 [P] Add accessibility labels to filter button ("Filter shots") in BaristaNotes/Pages/ActivityFeedPage.cs
- [ ] T045 [P] Add accessibility labels to filter chips (announce selected/unselected state) in BaristaNotes/Integrations/Popups/ShotFilterPopup.cs
- [ ] T046 [P] Verify touch targets are minimum 44x44px for all filter controls
- [ ] T047 Add loading indicator when filter options are loading in ShotFilterPopup in BaristaNotes/Integrations/Popups/ShotFilterPopup.cs
- [ ] T048 Verify filter popup works in both portrait and landscape orientations
- [ ] T049 Run dotnet build and verify zero compilation errors
- [ ] T050 Run all tests and verify 80%+ coverage on filter logic
- [ ] T051 Constitution compliance verification (Code Quality, Test-First, UX Consistency, Performance)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup - BLOCKS all user story UI work
- **User Story 1 (Phase 3)**: Depends on Foundational - MVP delivery
- **User Stories 2-3 (Phases 4-5)**: Can run in parallel after US1, or sequentially
- **User Stories 4-5 (Phases 6-7)**: Depend on US1-3 being complete (uses all filter types)
- **Polish (Phase 8)**: Depends on all user stories being complete

### User Story Dependencies

| Story | Depends On | Can Parallel With |
|-------|------------|-------------------|
| US1 (Bean Filter) | Foundational | - |
| US2 (Made For Filter) | Foundational | US1 (after popup base created) |
| US3 (Rating Filter) | Foundational | US1, US2 (after popup base created) |
| US4 (Multiple Filters) | US1, US2, US3 | - |
| US5 (Clear Filters) | US1 | US2, US3, US4 |

### Within Each User Story

1. Tests MUST be written and FAIL before implementation
2. Popup UI changes before page integration
3. Core implementation before accessibility polish

### Parallel Opportunities

**Phase 1 (all parallel)**:
```
T001: ShotFilterCriteriaDto.cs
T002: ShotFilterCriteria.cs
```

**Phase 3 Tests (all parallel)**:
```
T017: Integration test - bean filter
T018: Integration test - bean no matches
T019: Unit test - bean DTO mapping
```

**Phases 4-5 (can overlap after T020-T021)**:
```
US2 implementation (T031-T032)
US3 implementation (T035)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T002)
2. Complete Phase 2: Foundational (T003-T016)
3. Complete Phase 3: User Story 1 (T017-T028)
4. **STOP and VALIDATE**: Demo bean filtering
5. Deploy if ready

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 (Bean Filter) → Test → **MVP Demo**
3. Add US2 (Made For) → Test → Incremental release
4. Add US3 (Rating) → Test → Incremental release
5. Add US4 (Multi-filter) + US5 (Clear) → Test → Feature complete
6. Polish → Final release

---

## Summary

| Metric | Count |
|--------|-------|
| **Total Tasks** | 51 |
| **Setup Tasks** | 2 |
| **Foundational Tasks** | 14 |
| **US1 Tasks** | 12 |
| **US2 Tasks** | 4 |
| **US3 Tasks** | 3 |
| **US4 Tasks** | 4 |
| **US5 Tasks** | 4 |
| **Polish Tasks** | 8 |
| **Parallel Opportunities** | 18 tasks marked [P] |

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Verify tests fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Use ThemeKey system for all styling (no inline styles)
- Use MaterialSymbolsFont icons (no emojis per constitution)
