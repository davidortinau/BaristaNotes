```markdown
# Tasks: AI Shot Improvement Advice

**Input**: Design documents from `/specs/001-ai-shot-advice/`
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/ ✓, quickstart.md ✓

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add NuGet packages and create foundational DTOs used across all stories

- [X] T001 Add Microsoft.Extensions.AI and Microsoft.Extensions.AI.OpenAI package references to BaristaNotes/BaristaNotes.csproj
- [X] T002 [P] Create ShotContextDto in BaristaNotes.Core/Services/DTOs/ShotContextDto.cs
- [X] T003 [P] Create BeanContextDto in BaristaNotes.Core/Services/DTOs/BeanContextDto.cs
- [X] T004 [P] Create EquipmentContextDto in BaristaNotes.Core/Services/DTOs/EquipmentContextDto.cs
- [X] T005 [P] Create AIAdviceRequestDto in BaristaNotes.Core/Services/DTOs/AIAdviceRequestDto.cs
- [X] T006 [P] Create AIAdviceResponseDto in BaristaNotes.Core/Services/DTOs/AIAdviceResponseDto.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T007 Create IAIAdviceService interface in BaristaNotes/Services/IAIAdviceService.cs per contracts/service-contracts.md
- [X] T008 Add GetShotContextForAIAsync method to existing IShotService interface in BaristaNotes.Core/Services/IShotService.cs
- [X] T009 Implement GetShotContextForAIAsync in ShotService (BaristaNotes.Core/Services/ShotService.cs) to build AIAdviceRequestDto with shot, bean, equipment, and historical shots
- [X] T010 Configure IConfiguration for API key loading (appsettings.json structure, gitignored appsettings.Development.json for local dev) in BaristaNotes/
- [X] T011 Register IAIAdviceService as singleton in BaristaNotes/MauiProgram.cs DI container with IConfiguration injection

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Request Deep Advice from Shot Detail (Priority: P1) 🎯 MVP

**Goal**: Users can explicitly request detailed AI advice from the shot detail page by tapping "Get Advice"

**Independent Test**: View any logged shot, tap "Get Advice", receive detailed adjustment suggestions

### Tests for User Story 1

- [X] T012 [P] [US1] Create unit test for prompt building (verify all shot parameters included) in BaristaNotes.Tests/Unit/AIPromptBuilderTests.cs
- [X] T013 [P] [US1] Create unit test for AIAdviceService.GetAdviceForShotAsync with mock IChatClient in BaristaNotes.Tests/Unit/AIAdviceServiceTests.cs
- [X] T014 [P] [US1] Create MockChatClient helper class in BaristaNotes.Tests/Mocks/MockChatClient.cs

### Implementation for User Story 1

- [X] T015 [US1] Implement AIAdviceService.GetOrCreateClient with IChatClient creation from IConfiguration API key in BaristaNotes/Services/AIAdviceService.cs
- [X] T016 [US1] Implement AIAdviceService.BuildPrompt method with system prompt (espresso expertise) and user message (shot context) in BaristaNotes/Services/AIAdviceService.cs
- [X] T017 [US1] Implement AIAdviceService.GetAdviceForShotAsync with timeout (10s), error handling, and AIAdviceResponseDto return in BaristaNotes/Services/AIAdviceService.cs
- [X] T018 [US1] Implement AIAdviceService.IsConfiguredAsync to check if API key is present in configuration in BaristaNotes/Services/AIAdviceService.cs
- [X] T019 [US1] Create ShotDetailPage component with shot info display and "Get Advice" button in BaristaNotes/Pages/ShotDetailPage.cs
- [X] T020 [US1] Add navigation route to ShotDetailPage in AppShell.cs and wire tap-to-detail from ActivityFeedPage shot cards
- [X] T021 [US1] Create AIAdviceDisplay component to show advice text with loading state using MauiReactor patterns in BaristaNotes/Components/AIAdviceDisplay.cs
- [X] T022 [US1] Wire ShotDetailPage "Get Advice" button to AIAdviceService with loading indicator and error handling via IFeedbackService

**Checkpoint**: User Story 1 complete - users can get AI advice from shot detail page

---

## Phase 4: User Story 2 - Passive AI Assessment After Logging (Priority: P2)

**Goal**: System passively shows brief insight after logging shots that deviate from successful history

**Independent Test**: Log a shot with parameters differing from best-rated shots, see brief insight appear

### Tests for User Story 2

- [X] T023 [P] [US2] Create unit test for GetPassiveInsightAsync deviation detection logic in BaristaNotes.Tests/Unit/AIAdviceServiceTests.cs

### Implementation for User Story 2

- [X] T024 [US2] Implement AIAdviceService.GetPassiveInsightAsync to detect significant parameter deviation from best shots in BaristaNotes/Services/AIAdviceService.cs
- [ ] T025 [US2] Modify ShotLoggingPage to call GetPassiveInsightAsync after save (fire-and-forget) and display insight if returned in BaristaNotes/Pages/ShotLoggingPage.cs
- [ ] T026 [US2] Create PassiveInsightBanner component to display brief insight with tap-to-detail action in BaristaNotes/Components/PassiveInsightBanner.cs

**Checkpoint**: User Story 2 complete - users see passive insights after logging deviant shots

---

## Phase 5: User Story 3 - AI Advice Informed by Rating History (Priority: P2)

**Goal**: AI advice incorporates user's rating history to identify successful parameter patterns

**Independent Test**: Have 5+ rated shots with varying ratings, request advice, verify AI references rating patterns

### Tests for User Story 3

- [X] T027 [P] [US3] Create unit test verifying prompt includes best-rated shots summary in BaristaNotes.Tests/Unit/AIPromptBuilderTests.cs

### Implementation for User Story 3

- [X] T028 [US3] Enhance BuildPrompt to include summary of best-rated shots (rating >= 3) for same bag in BaristaNotes/Services/AIAdviceService.cs
- [X] T029 [US3] Update GetShotContextForAIAsync to include up to 10 historical shots sorted by rating desc in BaristaNotes.Core/Services/ShotService.cs

**Checkpoint**: User Story 3 complete - AI advice leverages rating history for personalized suggestions

---

## Phase 6: User Story 4 - Optional Tasting Notes Field (Priority: P3)

**Goal**: Users can optionally add tasting notes when logging shots for AI to incorporate

**Independent Test**: Log shot with tasting notes "sour, thin", request advice, see notes referenced in suggestions

### Tests for User Story 4

- [X] T030 [P] [US4] Create unit test verifying prompt includes tasting notes when present in BaristaNotes.Tests/Unit/AIPromptBuilderTests.cs

### Implementation for User Story 4

- [X] T031 [US4] Add TastingNotes nullable string property to ShotRecord model in BaristaNotes.Core/Models/ShotRecord.cs
- [X] T032 [US4] Create EF Core migration AddTastingNotesToShotRecord in BaristaNotes.Core/Migrations/
- [X] T033 [US4] Add "Tasting Notes" optional text field to ShotLoggingPage form in BaristaNotes/Pages/ShotLoggingPage.cs
- [X] T034 [US4] Update ShotContextDto to include TastingNotes and ensure BuildPrompt incorporates them in BaristaNotes/Services/AIAdviceService.cs

**Checkpoint**: User Story 4 complete - users can add tasting notes that inform AI advice

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final quality gates and documentation

- [X] T035 [P] Update docs/SERVICES.md with IAIAdviceService documentation
- [X] T036 [P] Add AI configuration section to docs/GETTING_STARTED.md (developer setup with appsettings.Development.json)
- [X] T037 Verify all tests pass and meet 80% coverage for AI service code
- [ ] T038 Run manual test of quickstart.md scenarios
- [ ] T039 Constitution compliance verification: code quality, test coverage, UX consistency, performance (<10s response)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies - can start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 - BLOCKS all user stories
- **User Stories (Phase 3-6)**: All depend on Phase 2 completion
  - US1 (P1) can proceed independently
  - US2 (P2) depends on US1 for ShotDetailPage navigation target
  - US3 (P2) can run parallel with US2, enhances US1
  - US4 (P3) can run after US1, adds data field
- **Phase 7 (Polish)**: Depends on all desired user stories being complete

### User Story Dependencies

- **US1 (P1)**: Can start after Phase 2 - No dependencies on other stories - **MVP**
- **US2 (P2)**: Can start after Phase 2 - Needs ShotDetailPage from US1 for navigation
- **US3 (P2)**: Can start after Phase 2 - Enhances US1's prompt building
- **US4 (P3)**: Can start after Phase 2 - Adds database field, enhances prompt

### Within Each User Story

- Tests MUST be written first and FAIL before implementation
- DTOs/Models before services
- Services before pages/components
- Core logic before UI integration

---

## Parallel Opportunities

### Phase 1 Parallelization

```
All DTO creation tasks (T002-T006) can run in parallel
```

### Phase 3 (US1) Parallelization

```
T012, T013, T014 (tests + mock) can run in parallel
```

### Cross-Phase Parallelization (after Phase 2)

```
US3 (T027-T029) and US4 (T030-T034) can run in parallel
US2 depends on US1's ShotDetailPage
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T006)
2. Complete Phase 2: Foundational (T007-T010)
3. Complete Phase 3: User Story 1 (T011-T022)
4. **STOP and VALIDATE**: Test US1 independently
5. Deploy/demo if ready - users can get AI advice from shot detail

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 → Test independently → Deploy (MVP!)
3. Add US3 → Test independently → Enhanced advice
4. Add US2 → Test independently → Passive insights
5. Add US4 → Test independently → Tasting notes support

### Task Count Summary

| Phase | Tasks | Parallelizable |
|-------|-------|----------------|
| Setup | 6 | 5 |
| Foundational | 5 | 0 |
| US1 (P1) | 11 | 3 |
| US2 (P2) | 4 | 1 |
| US3 (P2) | 3 | 1 |
| US4 (P3) | 5 | 1 |
| Polish | 5 | 2 |
| **Total** | **39** | **13** |

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Verify tests fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Avoid: vague tasks, same file conflicts, cross-story dependencies that break independence

```
