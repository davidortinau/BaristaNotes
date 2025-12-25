# Tasks: AI Bean Recommendations

**Input**: Design documents from `/specs/001-ai-bean-recommendations/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Tests are included per Constitution Principle II (Test-First Development). Tests must be written FIRST, must FAIL before implementation, and require 80% minimum coverage.

**Quality Gates**: All tasks must pass constitution-mandated quality gates before merge: tests pass, static analysis clean, code review approved, performance baseline met.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Include exact file paths in descriptions

## Path Conventions

- **Mobile app**: `BaristaNotes/` (main app), `BaristaNotes.Core/` (services/models), `BaristaNotes.Tests/` (tests)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create DTOs and extend service interfaces required by all user stories

- [x] T001 [P] Create RecommendationType enum in BaristaNotes/DTOs/RecommendationType.cs
- [x] T002 [P] Create AIRecommendationDto class in BaristaNotes/DTOs/AIRecommendationDto.cs
- [x] T003 [P] Create BeanRecommendationContextDto class in BaristaNotes/DTOs/BeanRecommendationContextDto.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Extend existing services with methods that BOTH user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 Add GetMostRecentBeanIdAsync method signature to BaristaNotes.Core/Services/IShotService.cs
- [x] T005 Implement GetMostRecentBeanIdAsync in BaristaNotes.Core/Services/ShotService.cs
- [x] T006 Add BeanHasHistoryAsync method signature to BaristaNotes.Core/Services/IShotService.cs
- [x] T007 Implement BeanHasHistoryAsync in BaristaNotes.Core/Services/ShotService.cs
- [x] T008 Add GetBeanRecommendationContextAsync method signature to BaristaNotes.Core/Services/IShotService.cs
- [x] T009 Implement GetBeanRecommendationContextAsync in BaristaNotes.Core/Services/ShotService.cs
- [x] T010 Add GetRecommendationsForBeanAsync method signature to BaristaNotes/Services/IAIAdviceService.cs

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - New Bean AI Recommendations (Priority: P1) 🎯 MVP

**Goal**: When user selects a bean with no shot history, AI recommends starting extraction parameters (dose, grind, output, duration) and displays them with appropriate toast message.

**Independent Test**: Create a new bag for a bean with zero shot history, select it from picker, verify loading bar appears, fields populate with AI values, and toast shows "We didn't have any shots for this bean..."

### Tests for User Story 1 (REQUIRED per Constitution) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T011 [P] [US1] Unit test for BeanHasHistoryAsync returns false for new bean in BaristaNotes.Tests/Services/ShotServiceTests.cs
- [x] T012 [P] [US1] Unit test for GetBeanRecommendationContextAsync builds context without history in BaristaNotes.Tests/Services/ShotServiceTests.cs
- [x] T013 [P] [US1] Unit test for GetRecommendationsForBeanAsync returns NewBean type when no history in BaristaNotes.Tests/Services/AIAdviceServiceTests.cs
- [x] T014 [P] [US1] Unit test for new bean toast message format contains all four parameters in BaristaNotes.Tests/Services/AIAdviceServiceTests.cs

### Implementation for User Story 1

- [x] T015 [P] [US1] Add BuildNewBeanPrompt method to BaristaNotes/Helpers/AIPromptBuilder.cs for beans without history
- [x] T016 [P] [US1] Add ParseRecommendationResponse method to BaristaNotes/Services/AIAdviceService.cs to parse AI JSON response
- [x] T017 [US1] Implement GetRecommendationsForBeanAsync for new beans (no history path) in BaristaNotes/Services/AIAdviceService.cs
- [x] T018 [US1] Add private _recommendationCts CancellationTokenSource field to BaristaNotes/Pages/ShotLoggingPage.cs
- [x] T019 [US1] Add RequestAIRecommendationsAsync method to BaristaNotes/Pages/ShotLoggingPage.cs with loading state, field population, and new bean toast
- [x] T020 [US1] Modify bag picker OnSelectedIndexChanged handler in BaristaNotes/Pages/ShotLoggingPage.cs to detect new bean and call RequestAIRecommendationsAsync
- [x] T021 [US1] Add error handling for AI service failures with error toast in BaristaNotes/Pages/ShotLoggingPage.cs

**Checkpoint**: User Story 1 complete - new beans get AI recommendations with "We didn't have any shots..." toast

---

## Phase 4: User Story 2 - Bean Switching AI Recommendations (Priority: P1)

**Goal**: When user switches to a bean they have history with (but isn't most recent), AI recommends parameters based on their best past shots and displays them with appropriate toast message.

**Independent Test**: Log shots with Bean A, switch to Bean B and log shots, switch back to Bean A, verify loading bar appears, fields populate with AI values based on history, and toast shows "I see you're switching beans..."

### Tests for User Story 2 (REQUIRED per Constitution) ⚠️

- [x] T022 [P] [US2] Unit test for GetMostRecentBeanIdAsync returns correct bean ID in BaristaNotes.Tests/Services/ShotServiceTests.cs
- [x] T023 [P] [US2] Unit test for GetBeanRecommendationContextAsync includes historical shots (up to 10, sorted by rating) in BaristaNotes.Tests/Services/ShotServiceTests.cs
- [x] T024 [P] [US2] Unit test for GetRecommendationsForBeanAsync returns ReturningBean type when history exists in BaristaNotes.Tests/Services/AIAdviceServiceTests.cs
- [x] T025 [P] [US2] Unit test for returning bean toast message format contains all four parameters in BaristaNotes.Tests/Services/AIAdviceServiceTests.cs

### Implementation for User Story 2

- [x] T026 [P] [US2] Add BuildReturningBeanPrompt method to BaristaNotes/Helpers/AIPromptBuilder.cs for beans with history
- [x] T027 [US2] Extend GetRecommendationsForBeanAsync for returning beans (with history path) in BaristaNotes/Services/AIAdviceService.cs
- [x] T028 [US2] Update bag picker OnSelectedIndexChanged to detect bean switch (selectedBeanId != mostRecentBeanId) in BaristaNotes/Pages/ShotLoggingPage.cs
- [x] T029 [US2] Update RequestAIRecommendationsAsync to show returning bean toast when RecommendationType is ReturningBean in BaristaNotes/Pages/ShotLoggingPage.cs

**Checkpoint**: User Story 2 complete - bean switching gets AI recommendations with "I see you're switching beans..." toast

---

## Phase 5: User Story 3 - Loading State Consistency (Priority: P2)

**Goal**: AI recommendation loading experience is consistent with existing AI advice feature, using same animated loading bar.

**Independent Test**: Trigger AI recommendations and verify the same animated loading bar appears as when requesting AI advice on an existing shot.

### Tests for User Story 3 (REQUIRED per Constitution) ⚠️

- [x] T030 [P] [US3] Unit test for IsLoadingAdvice state is set true when RequestAIRecommendationsAsync starts in BaristaNotes.Tests/Pages/ShotLoggingPageTests.cs
- [x] T031 [P] [US3] Unit test for IsLoadingAdvice state is set false when RequestAIRecommendationsAsync completes in BaristaNotes.Tests/Pages/ShotLoggingPageTests.cs

### Implementation for User Story 3

- [x] T032 [US3] Verify RequestAIRecommendationsAsync reuses existing IsLoadingAdvice state (same as RenderAnimatedLoadingBar) in BaristaNotes/Pages/ShotLoggingPage.cs
- [x] T033 [US3] Add cancellation handling in RequestAIRecommendationsAsync to cancel pending request on bean re-selection in BaristaNotes/Pages/ShotLoggingPage.cs
- [x] T034 [US3] Verify loading bar disappears simultaneously with field population on success in BaristaNotes/Pages/ShotLoggingPage.cs

**Checkpoint**: User Story 3 complete - loading experience matches existing AI advice pattern

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Edge cases, error handling, and final quality gates

- [x] T035 [P] Add handling for partial AI responses (populate available values, default others) in BaristaNotes/Services/AIAdviceService.cs
- [x] T036 [P] Add handling for low-confidence recommendations (few historical shots) in toast message in BaristaNotes/Pages/ShotLoggingPage.cs
- [x] T037 [P] Add timeout handling (10-second max) matching existing AI advice timeout in BaristaNotes/Services/AIAdviceService.cs
- [x] T038 Add unit tests for edge cases (AI timeout, partial response, cancellation) in BaristaNotes.Tests/Services/AIAdviceServiceTests.cs
- [x] T039 Verify 80% code coverage for AI recommendation logic in BaristaNotes.Tests/
- [x] T040 Run dotnet build to verify no compilation errors
- [x] T041 Constitution compliance verification: Code quality, test-first, UX consistency, performance requirements

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-5)**: All depend on Foundational phase completion
  - US1 and US2 can proceed in parallel (both P1 priority)
  - US3 can proceed after US1 or US2 (depends on RequestAIRecommendationsAsync existing)
- **Polish (Phase 6)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P1)**: Can start after Foundational (Phase 2) - Shares some code with US1 but independently testable
- **User Story 3 (P2)**: Can start after US1 implementation tasks (depends on RequestAIRecommendationsAsync)

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- DTOs/prompts before service implementation
- Service implementation before page integration
- Core implementation before edge case handling

### Parallel Opportunities

- T001, T002, T003 can run in parallel (different DTO files)
- T011-T014 (US1 tests) can run in parallel
- T015, T016 can run in parallel (different helper/service files)
- T022-T025 (US2 tests) can run in parallel
- T030, T031 (US3 tests) can run in parallel
- T035, T036, T037 (polish) can run in parallel

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together:
Task: "T011 Unit test for BeanHasHistoryAsync returns false for new bean"
Task: "T012 Unit test for GetBeanRecommendationContextAsync builds context without history"
Task: "T013 Unit test for GetRecommendationsForBeanAsync returns NewBean type"
Task: "T014 Unit test for new bean toast message format"

# Launch parallel implementation tasks:
Task: "T015 Add BuildNewBeanPrompt method"
Task: "T016 Add ParseRecommendationResponse method"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T003)
2. Complete Phase 2: Foundational (T004-T010)
3. Complete Phase 3: User Story 1 (T011-T021)
4. **STOP and VALIDATE**: Test new bean recommendations independently
5. Deploy/demo if ready - users can get AI recommendations for new beans

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy (MVP!)
3. Add User Story 2 → Test independently → Deploy (adds bean switching)
4. Add User Story 3 → Test independently → Deploy (UX polish)
5. Add Polish phase → Final quality gates → Release

### Parallel Team Strategy

With multiple developers:
1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1 (new beans)
   - Developer B: User Story 2 (returning beans)
3. Either developer: User Story 3 (loading consistency)
4. Team: Polish phase

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- US1 and US2 are both P1 priority - equally important for full feature
- US3 is P2 - enhances UX but not blocking for core functionality
- Reuse existing IsLoadingAdvice state - do NOT create new loading state
- Match existing 10-second AI timeout pattern
- Use FeedbackService.ShowInfoAsync for success toasts, ShowErrorAsync for failures
