# Tasks: Bean Rating Tracking and Bag Management

**Input**: Design documents from `/specs/001-bean-rating-tracking/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Tasks include TDD approach per Constitution Principle II (Test-First Development). Tests must be written FIRST, must FAIL before implementation, and require 80% minimum coverage (100% for rating calculations per NFR-Q1).

**Quality Gates**: All tasks must pass constitution-mandated quality gates before merge: tests pass, static analysis clean (no warnings), code review approved, performance baseline met (<500ms rating calculations), documentation updated.

**Organization**: Tasks are grouped by user story (P1, P2, P3) to enable independent implementation and testing of each story.

## Format: `- [ ] [ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

This is a .NET MAUI mobile project with three-project structure:
- **Core library**: `BaristaNotes.Core/` (Models, Services, Data, Migrations)
- **MAUI app**: `BaristaNotes/` (Pages, Components, Platform-specific)
- **Tests**: `BaristaNotes.Tests/` (Unit, Integration, Contract)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and database migration framework

- [X] T001 Review existing project structure and verify all dependencies are installed (dotnet restore)
- [X] T002 Create feature branch tracking: Add specs/001-bean-rating-tracking/implementation-log.md for progress tracking
- [X] T003 [P] Review existing Bean, ShotRecord entities in BaristaNotes.Core/Models/ to understand current schema

---

## Phase 2: Foundational (Database Schema Migration)

**Purpose**: Core data model changes that ALL user stories depend on. MUST be complete before any UI work.

**⚠️ CRITICAL**: This phase implements the Bean→Bag data migration. No user story work can begin until migration is tested and validated.

### Database Migration Tasks

- [X] T004 Create Bag entity model in BaristaNotes.Core/Models/Bag.cs with schema from data-model.md (Id, BeanId, RoastDate, Notes, IsComplete, IsActive, CreatedAt, SyncId, LastModifiedAt, IsDeleted)
- [X] T005 Modify Bean entity in BaristaNotes.Core/Models/Bean.cs: Remove RoastDate property, add Bags navigation property
- [X] T006 Modify ShotRecord entity in BaristaNotes.Core/Models/ShotRecord.cs: Change BeanId to BagId, update navigation property from Bean to Bag
- [X] T007 Update BaristaNotesContext in BaristaNotes.Core/Data/BaristaNotesContext.cs: Add DbSet<Bag> Bags, configure Bag entity in OnModelCreating (relationships, indexes, validation rules)
- [X] T008 Generate EF Core migration via `dotnet ef migrations add AddBagEntity --project BaristaNotes.Core` from Core directory
- [X] T009 Review generated migration file in BaristaNotes.Core/Migrations/[timestamp]_AddBagEntity.cs and manually add data seeding SQL in Up() method per research.md section 1 (create Bags from existing Beans, update ShotRecords.BagId)
- [X] T010 Add Down() migration logic to rollback schema changes (restore BeanId column, copy RoastDate back, drop Bags table)
- [X] T011 Test migration: Run `dotnet ef database update` in dev environment, verify Bags table created, existing data migrated, ShotRecords updated
- [X] T012 Test migration rollback: Run `dotnet ef database update [previous_migration]`, verify schema restored correctly
- [ ] T013 Create DataMigrationTests.cs in BaristaNotes.Tests/Integration/ to verify Up() and Down() migration data integrity (seed test data, run migration, assert Bags created, ShotRecords updated)

**Checkpoint**: Database schema updated, migration tested. User story implementation can now begin.

---

## Phase 3: User Story 1 - View Aggregate Bean Ratings (Priority: P1) 🎯 MVP

**Goal**: Display aggregate ratings (average + distribution) for beans to help users decide if they should reorder. This is the core value proposition of the feature.

**Independent Test**: View any bean with logged shots and see aggregate rating (e.g., "4.25 ★ (18 ratings)") and distribution bars (5★: 10, 4★: 5, 3★: 2, 2★: 1, 1★: 0). Delivers immediate value without needing P2/P3.

### Tests for User Story 1 (Write FIRST) ⚠️

- [ ] T014 [P] [US1] Create RatingServiceTests.cs in BaristaNotes.Tests/Unit/: Test GetBeanRatingAsync with multiple bags returns correct aggregate, test with no shots returns empty aggregate, test distribution calculation accuracy
- [ ] T015 [P] [US1] Create BeanServiceTests.cs in BaristaNotes.Tests/Unit/: Test GetWithRatingsAsync returns bean with rating aggregate
- [ ] T016 [P] [US1] Create BeanRepositoryTests.cs in BaristaNotes.Tests/Integration/: Test rating queries with proper indexes, test N+1 query prevention with .Include()

### DTOs and Service Interfaces for User Story 1

- [ ] T017 [P] [US1] Create RatingAggregateDto.cs in BaristaNotes.Core/Services/DTOs/ (copy from specs/001-bean-rating-tracking/contracts/DTOs/RatingAggregateDto.cs)
- [ ] T018 [P] [US1] Create IRatingService.cs interface in BaristaNotes.Core/Services/ (copy from specs/001-bean-rating-tracking/contracts/IRatingService.cs)

### Service Implementation for User Story 1

- [ ] T019 [US1] Implement RatingService.cs in BaristaNotes.Core/Services/: Implement GetBeanRatingAsync (query ShotRecords where Bag.BeanId = X, calculate average, build distribution dictionary), implement GetBagRatingAsync (same logic filtered by BagId)
- [ ] T020 [US1] Add composite index IX_ShotRecords_BagId_Rating in migration or OnModelCreating for performance (critical for <500ms target per NFR-P2)
- [ ] T021 [US1] Update IBeanService.cs in BaristaNotes.Core/Services/ to add GetWithRatingsAsync(int beanId) method signature
- [ ] T022 [US1] Implement GetWithRatingsAsync in BeanService.cs: Inject IRatingService, load bean, call GetBeanRatingAsync, return bean with ratings
- [ ] T023 [US1] Register IRatingService in MauiProgram.cs: Add builder.Services.AddScoped<IRatingService, RatingService>()

### UI Components for User Story 1

- [ ] T024 [P] [US1] Create RatingDisplayComponent.cs in BaristaNotes/Components/: Implement Reactor.Maui component to display average rating (stars), total shots, rating distribution bars (use ProgressBar for each rating level 5→1)
- [ ] T025 [US1] Modify BeanDetailPage.cs in BaristaNotes/Pages/: Inject IRatingService, load bean rating in OnMountedAsync, render RatingDisplayComponent with aggregate data, add empty state for no ratings ("No ratings yet")

### Validation for User Story 1

- [ ] T026 [US1] Run unit tests: `dotnet test --filter FullyQualifiedName~RatingService`, verify 100% coverage for rating calculations per NFR-Q1
- [ ] T027 [US1] Performance test: Seed database with 50 beans, 500 shots, measure GetBeanRatingAsync execution time (target <500ms per NFR-P2)
- [ ] T028 [US1] Manual acceptance test: Create bean with multiple shots at various ratings, view bean detail, verify aggregate rating and distribution displayed correctly per acceptance scenario US1-1 and US1-2
- [ ] T029 [US1] Manual accessibility test: Verify screen reader announces rating values ("Average 4.25 stars"), keyboard navigation works, touch targets ≥44px per NFR-A1-A4

**Checkpoint**: User Story 1 complete and independently testable. Bean ratings visible without P2/P3 functionality.

---

## Phase 4: User Story 2 - Bag-Based Shot Logging (Priority: P2)

**Goal**: Enable bag-first shot logging workflow where users select a bag directly (not bean), and mark bags as complete when finished to keep UI clean. Essential for daily workflow.

**Independent Test**: Log shots by selecting from active bags only (completed bags hidden), mark a bag complete, verify it disappears from shot logging picker but remains in history views. Delivers streamlined workflow.

### Tests for User Story 2 (Write FIRST) ⚠️

- [ ] T030 [P] [US2] Create BagServiceTests.cs in BaristaNotes.Tests/Unit/: Test CreateBagAsync validation (RoastDate not future, BeanId exists), test GetActiveBagsForShotLoggingAsync returns only incomplete bags ordered by RoastDate DESC, test MarkBagCompleteAsync sets IsComplete=true, test ReactivateBagAsync sets IsComplete=false
- [ ] T031 [P] [US2] Create BagRepositoryTests.cs in BaristaNotes.Tests/Integration/: Test GetBagSummariesForBeanAsync includes BeanName and IsComplete, test active bags query uses composite index (BeanId, IsComplete, RoastDate)
- [ ] T032 [P] [US2] Create ShotServiceTests.cs in BaristaNotes.Tests/Unit/: Test shot creation with BagId validates bag exists and is active (IsComplete=false)

### DTOs and Service Interfaces for User Story 2

- [ ] T033 [P] [US2] Create BagSummaryDto.cs in BaristaNotes.Core/Services/DTOs/ (copy from specs/001-bean-rating-tracking/contracts/DTOs/BagSummaryDto.cs with BeanName, IsComplete, DisplayLabel properties)
- [ ] T034 [P] [US2] Create IBagService.cs interface in BaristaNotes.Core/Services/ (copy from specs/001-bean-rating-tracking/contracts/IBagService.cs with CreateBagAsync, GetActiveBagsForShotLoggingAsync, MarkBagCompleteAsync, ReactivateBagAsync methods)

### Repository Layer for User Story 2

- [ ] T035 [P] [US2] Create IBagRepository.cs in BaristaNotes.Core/Data/Repositories/ with methods: GetBagSummariesForBeanAsync(int beanId, bool includeCompleted), GetActiveBagsForShotLoggingAsync()
- [ ] T036 [US2] Implement BagRepository.cs in BaristaNotes.Core/Data/Repositories/: Implement query methods using EF Core LINQ with .Include(b => b.Bean) for BeanName, apply IsComplete filter for active bags query, order by RoastDate DESC

### Service Implementation for User Story 2

- [ ] T037 [US2] Implement BagService.cs in BaristaNotes.Core/Services/: Implement CreateBagAsync (validate RoastDate not future, BeanId exists, Notes ≤500 chars), implement GetActiveBagsForShotLoggingAsync (call repository method), implement MarkBagCompleteAsync and ReactivateBagAsync (toggle IsComplete flag, update LastModifiedAt)
- [ ] T038 [US2] Update IShotService.cs in BaristaNotes.Core/Services/ to change CreateShotAsync parameter from BeanId to BagId
- [ ] T039 [US2] Modify ShotService.cs in BaristaNotes.Core/Services/: Change shot creation to use BagId, add validation that Bag exists and IsComplete=false before allowing shot creation
- [ ] T040 [US2] Register IBagService in MauiProgram.cs: Add builder.Services.AddScoped<IBagService, BagService>()

### UI Pages for User Story 2

- [ ] T041 [P] [US2] Create BagFormPage.cs in BaristaNotes/Pages/: Implement Reactor.Maui page with BeanId parameter, DatePicker for RoastDate, Entry for Notes, validation, save button calls IBagService.CreateBagAsync
- [ ] T042 [US2] Modify ShotLoggingPage.cs in BaristaNotes/Pages/: **MAJOR CHANGE** - Remove bean picker, add bag picker loading from GetActiveBagsForShotLoggingAsync(), display bag.DisplayLabel ("{BeanName} - Roasted {Date}"), show bean info below picker, handle empty state ("No active bags" with "Add Bag" button), update shot save to use BagId
- [ ] T043 [US2] Modify BeanDetailPage.cs in BaristaNotes/Pages/: Add "Bags" section listing bags with BagSummaryDto (roast date, shot count, status badge), add "Add Bag" button navigating to BagFormPage
- [ ] T044 [P] [US2] Create BagDetailPage.cs in BaristaNotes/Pages/: Display bag details (roast date, notes, bean name), show "Mark as Complete" button (or "Reactivate" if already complete), display bag-level rating aggregate using RatingDisplayComponent

### Validation for User Story 2

- [ ] T045 [US2] Run unit tests: `dotnet test --filter FullyQualifiedName~BagService`, verify bag CRUD operations and completion logic
- [ ] T046 [US2] Manual acceptance test US2-1: Open shot logging page, verify only active bags shown ordered by roast date
- [ ] T047 [US2] Manual acceptance test US2-2: Select bag in shot logger, verify bean info displayed automatically
- [ ] T048 [US2] Manual acceptance test US2-3: Mark bag complete, verify it disappears from shot logging picker but visible in bean detail history
- [ ] T049 [US2] Manual acceptance test US2-4: Add new bag from bean detail, verify it immediately appears in shot logging picker
- [ ] T050 [US2] Manual acceptance test US2-5: Reactivate completed bag, verify it reappears in shot logging picker

**Checkpoint**: User Story 2 complete. Bag-based workflow fully functional with completion management.

---

## Phase 5: User Story 3 - View Ratings by Individual Bag (Priority: P3)

**Goal**: Display bag-level rating aggregates to identify quality variations between roasting batches. Provides analytical insights for quality-conscious users.

**Independent Test**: View bean with multiple bags, see separate rating aggregate for each bag (e.g., "Bag 1: Roasted Dec 1 - 4.8★ (5 ratings)", "Bag 2: Roasted Nov 15 - 3.2★ (8 ratings)"). Enables batch comparison.

### Tests for User Story 3 (Write FIRST) ⚠️

- [ ] T051 [P] [US3] Add tests to RatingServiceTests.cs: Test GetBagRatingAsync returns correct aggregate for specific bag, test GetBagRatingsBatchAsync returns dictionary mapping BagId → RatingAggregateDto
- [ ] T052 [P] [US3] Add tests to BagServiceTests.cs: Test GetBagSummariesForBeanAsync includes AverageRating per bag

### Service Implementation for User Story 3

- [ ] T053 [US3] Implement GetBagRatingsBatchAsync in RatingService.cs: Query ShotRecords grouped by BagId, calculate aggregates for multiple bags in single query (optimization for bean detail page listing many bags)
- [ ] T054 [US3] Update BagRepository.cs GetBagSummariesForBeanAsync to include AverageRating calculation per bag in SELECT projection

### UI Enhancement for User Story 3

- [ ] T055 [US3] Enhance BagDetailPage.cs in BaristaNotes/Pages/: Load bag-level rating aggregate via IRatingService.GetBagRatingAsync, display RatingDisplayComponent with bag-specific data
- [ ] T056 [US3] Enhance BeanDetailPage.cs bags section: Display individual bag ratings next to each bag in list (average rating badge, e.g., "4.8★"), enable tap to navigate to BagDetailPage for full distribution

### Validation for User Story 3

- [ ] T057 [US3] Manual acceptance test US3-1: Create bean with 3 bags, log shots with different ratings per bag, view bean detail, verify each bag shows separate aggregate
- [ ] T058 [US3] Manual acceptance test US3-2: Tap on bag, view bag detail page, compare ratings between bags, identify best/worst roasting date
- [ ] T059 [US3] Manual acceptance test US3-3: View bag with no shots, verify "No ratings yet" indicator shown

**Checkpoint**: User Story 3 complete. All three user stories independently functional and tested.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final integration, edge cases, performance optimization, accessibility validation

### Integration & Edge Cases

- [ ] T060 [P] Handle edge case: Bean with only one shot (display single rating without warning per spec edge cases)
- [ ] T061 [P] Handle edge case: Multiple bags with same roasting date (distinguish by Notes or creation timestamp per spec)
- [ ] T062 [P] Handle edge case: All bags complete (show "No active bags" with "Add Bag" and "Reactivate" options per spec)
- [ ] T063 [P] Handle edge case: User tries to log shot with no active bags (prompt to add bag first per spec)
- [ ] T064 [P] Handle edge case: 50+ bags for one bean (verify bags ordered by roast date DESC, consider pagination if performance issues)

### Performance Optimization

- [ ] T065 Verify composite index IX_Bags_BeanId_IsComplete_RoastDate exists and is used in active bags query (check via EF Core query logging or SQLite EXPLAIN)
- [ ] T066 Benchmark bean detail page load time with 50 beans, 500 shots: Verify <2s p95 per NFR-P1
- [ ] T067 Benchmark rating calculation with 100 shots per bean: Verify <500ms p95 per NFR-P2
- [ ] T068 Profile ShotLoggingPage bag picker load: Verify <100ms perceived response time per NFR-P3
- [ ] T069 Test rating distribution rendering with 100 shots: Verify <1s per NFR-P4

### Accessibility Validation (WCAG 2.1 AA)

- [ ] T070 Verify keyboard navigation: Tab through rating displays, bag pickers, buttons (NFR-A1)
- [ ] T071 Test screen reader announcements: Rating values announced correctly ("Average 4.25 stars, 18 ratings"), bag labels read correctly (NFR-A4)
- [ ] T072 Verify touch targets: All buttons, pickers ≥44x44px (NFR-A3)
- [ ] T073 Check color contrast: Rating stars, distribution bars meet WCAG 2.1 AA requirements (NFR-A2)

### Documentation & Cleanup

- [ ] T074 [P] Update README.md with bag management workflow, completion feature, rating aggregation explanation
- [ ] T075 [P] Add XML documentation comments to public methods in IBagService, IRatingService if missing
- [ ] T076 [P] Update API documentation (if applicable) with new bag endpoints and DTOs
- [ ] T077 Run full test suite: `dotnet test --collect:"XPlat Code Coverage"`, verify ≥80% overall, 100% for RatingService per NFR-Q1
- [ ] T078 Run static analysis: Verify no warnings per NFR-Q2 (dotnet format, code analyzers)
- [ ] T079 Final integration test: Complete end-to-end workflow (create bean → add bag → log shots → view ratings → mark complete → add new bag → view bag-level ratings)

**Checkpoint**: Feature complete, tested, documented, and ready for merge.

---

## Dependencies & Execution Order

### Critical Path (Sequential)
```
Phase 1 (Setup) → Phase 2 (Foundation) → Phase 3 (US1) → Phase 4 (US2) → Phase 5 (US3) → Phase 6 (Polish)
```

### User Story Independence

- **US1** (P1): Can be delivered as MVP independently - bean ratings visible without bag management
- **US2** (P2): Depends on US1 indirectly (uses rating service) but primarily depends on Phase 2 (Bag entity exists)
- **US3** (P3): Depends on US1 (rating service) and US2 (bag detail page) but adds new views, mostly independent

### Parallel Execution Opportunities

**Within Phase 2 (Foundational)**:
- T003 (review entities) can run parallel with T001-T002
- T013 (migration tests) only after T004-T012 complete

**Within Phase 3 (US1)**:
- Tests (T014-T016) can run in parallel (different test files)
- DTOs/Interfaces (T017-T018) can run in parallel (different files)
- UI Component (T024) can run parallel with service implementation (T019-T023) until integration (T025)

**Within Phase 4 (US2)**:
- Tests (T030-T032) can run in parallel
- DTOs/Interfaces (T033-T034) can run in parallel
- Repository layer (T035-T036) can run while services (T037-T040) are being written (different concerns)
- UI pages (T041, T044) can run in parallel (different pages, no shared state)

**Within Phase 5 (US3)**:
- Tests (T051-T052) can run in parallel
- UI enhancements (T055-T056) can run in parallel (different pages)

**Within Phase 6 (Polish)**:
- Edge case handling (T060-T064), performance (T065-T069), accessibility (T070-T073), documentation (T074-T076) can all run in parallel

---

## Implementation Strategy

### Recommended MVP Scope
**Deliver User Story 1 (P1) first** as standalone MVP:
- Provides immediate value (bean rating aggregates)
- Validates rating calculation performance
- Enables user feedback on rating display UI
- Estimated: ~15-20 tasks (T001-T029)

### Incremental Delivery
1. **Sprint 1**: Phase 1 + Phase 2 + Phase 3 (US1) → MVP with bean ratings
2. **Sprint 2**: Phase 4 (US2) → Add bag management and completion workflow
3. **Sprint 3**: Phase 5 (US3) + Phase 6 → Add bag-level ratings and polish

### Risk Mitigation
- **Data migration (T004-T013)**: High risk. Test thoroughly with production-scale data before deploying.
- **ShotLoggingPage changes (T042)**: Medium risk. Existing 44KB complex page. Test bag picker integration carefully.
- **Performance (T065-T069)**: Medium risk. If targets not met, consider denormalization (cached ratings) as fallback.

---

## Total Task Count

- **Phase 1 (Setup)**: 3 tasks
- **Phase 2 (Foundational)**: 10 tasks
- **Phase 3 (US1 - P1)**: 16 tasks (MVP)
- **Phase 4 (US2 - P2)**: 21 tasks
- **Phase 5 (US3 - P3)**: 9 tasks
- **Phase 6 (Polish)**: 20 tasks

**Total**: 79 tasks

**Parallel Opportunities**: ~35 tasks marked with [P] can run in parallel (44% parallelizable)

**Test Coverage**: 13 test tasks (T014-T016, T030-T032, T051-T052, T013) ensuring TDD compliance

---

## Success Criteria (from spec.md)

- **SC-001**: Users can view aggregate bean ratings (average and distribution) within 2 seconds ✓ Validated in T066
- **SC-002**: Users can add new bag and associate shots in under 1 minute ✓ Validated in T049
- **SC-003**: 95% of users can identify highest-rated bean ✓ Validated in T028
- **SC-004**: Rating calculations accurate within 0.01 for up to 100 shots ✓ Validated in T026, T067
- **SC-005**: Users can distinguish bag-level vs bean-level ratings within 10 seconds ✓ Validated in T057

**All success criteria mapped to validation tasks. Feature ready for implementation.**
