# Tasks: Espresso Shot Tracking & Management

**Feature**: 001-espresso-tracking  
**Input**: Design documents from `/specs/001-espresso-tracking/`  
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅

**Tests**: MANDATORY per Constitution Principle II (Test-First Development). Tests must be written FIRST, must FAIL before implementation, and require 80% minimum coverage (100% for critical paths like data persistence and shot pre-population).

**Quality Gates**: All tasks must pass constitution-mandated quality gates before merge: tests pass (80% coverage minimum), static analysis clean, code review approved, performance baseline met (<2s launch, <500ms pre-population, 60fps), documentation updated.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `- [ ] [ID] [P?] [Story] Description`

- **Checkbox**: `- [ ]` (markdown checkbox)
- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3) - ONLY for user story phases
- Include exact file paths in descriptions

## Path Conventions

Based on plan.md:
- **Main Project**: `BaristaNotes/` (MAUI app)
- **Core Library**: `BaristaNotes.Core/` (shared business logic)
- **Test Project**: `BaristaNotes.Tests/`
- Models: `BaristaNotes.Core/Models/`
- Data Layer: `BaristaNotes.Core/Data/`
- Services: `BaristaNotes.Core/Services/`
- Pages: `BaristaNotes/Pages/` (Reactor MVU components)
- Components: `BaristaNotes/Components/`
- Resources: `BaristaNotes/Resources/`
- Infrastructure: `BaristaNotes/Infrastructure/` (platform implementations)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and foundational structure

- [X] T001 Create .NET MAUI solution structure with BaristaNotes.sln, BaristaNotes/ project, and BaristaNotes.Tests/ project
- [X] T002 Add NuGet packages: Reactor.Maui (latest preview), CommunityToolkit.Maui (7.0.0), Microsoft.EntityFrameworkCore.Sqlite (8.0.0), CoreSync.Sqlite (2.3.0), CoreSync.Http.Client (2.3.0) to BaristaNotes/BaristaNotes.csproj
- [X] T003 [P] Add xUnit and EF Core testing packages to BaristaNotes.Tests/BaristaNotes.Tests.csproj
- [X] T004 [P] Configure MauiProgram.cs with UseMauiReactorApp and UseMauiCommunityToolkit
- [X] T005 [P] Add icon font file to BaristaNotes/Resources/Fonts/ and register in MauiProgram.cs
- [X] T006 [P] Create theme resources in BaristaNotes/Resources/Styles/Colors.xaml (color palette: coffee brown primary, surface colors, semantic colors)
- [X] T007 [P] Create application styles in BaristaNotes/Resources/Styles/Styles.xaml (spacing scale, typography, button styles)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core data layer and infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Data Models & Database

- [X] T008 [P] Create EquipmentType enum in BaristaNotes/Models/Enums/EquipmentType.cs (Machine, Grinder, Tamper, PuckScreen, Other)
- [X] T009 [P] Create Equipment model in BaristaNotes/Models/Equipment.cs with CoreSync metadata (Id, Name, Type, Notes, IsActive, CreatedAt, SyncId, LastModifiedAt, IsDeleted)
- [X] T010 [P] Create Bean model in BaristaNotes/Models/Bean.cs with CoreSync metadata (Id, Name, Roaster, RoastDate, Origin, Notes, IsActive, CreatedAt, SyncId, LastModifiedAt, IsDeleted)
- [X] T011 [P] Create UserProfile model in BaristaNotes/Models/UserProfile.cs with CoreSync metadata (Id, Name, AvatarPath, CreatedAt, SyncId, LastModifiedAt, IsDeleted)
- [X] T012 [P] Create ShotRecord model in BaristaNotes/Models/ShotRecord.cs with recipe, actuals, rating, and CoreSync metadata
- [X] T013 [P] Create ShotEquipment junction model in BaristaNotes/Models/ShotEquipment.cs (many-to-many for shot accessories)
- [X] T014 Create BaristaNotesContext DbContext in BaristaNotes/Data/BaristaNotesContext.cs with all entity configurations, indexes, and relationships per data-model.md
- [X] T015 Generate initial EF Core migration for all entities with command: dotnet ef migrations add InitialCreate

### Repository Layer

- [X] T016 [P] Create IRepository<T> interface in BaristaNotes/Data/Repositories/IRepository.cs with CRUD methods
- [X] T017 [P] Create Repository<T> base implementation in BaristaNotes/Data/Repositories/Repository.cs
- [X] T018 [P] Create IEquipmentRepository interface with GetByTypeAsync in BaristaNotes/Data/Repositories/IEquipmentRepository.cs
- [X] T019 [P] Create EquipmentRepository implementation in BaristaNotes/Data/Repositories/EquipmentRepository.cs
- [X] T020 [P] Create IBeanRepository interface in BaristaNotes/Data/Repositories/IBeanRepository.cs
- [X] T021 [P] Create BeanRepository implementation in BaristaNotes/Data/Repositories/BeanRepository.cs
- [X] T022 [P] Create IUserProfileRepository interface in BaristaNotes/Data/Repositories/IUserProfileRepository.cs
- [X] T023 [P] Create UserProfileRepository implementation in BaristaNotes/Data/Repositories/UserProfileRepository.cs
- [X] T024 [P] Create IShotRecordRepository interface with GetMostRecentAsync in BaristaNotes/Data/Repositories/IShotRecordRepository.cs
- [X] T025 [P] Create ShotRecordRepository implementation with optimized queries (AsNoTracking, indexed lookups) in BaristaNotes/Data/Repositories/ShotRecordRepository.cs

### Service Layer DTOs & Interfaces

- [X] T026 [P] Create all DTOs in BaristaNotes/Services/DTOs/: ShotRecordDto, CreateShotDto, UpdateShotDto, EquipmentDto, CreateEquipmentDto, UpdateEquipmentDto, BeanDto, CreateBeanDto, UpdateBeanDto, UserProfileDto, CreateUserProfileDto, UpdateUserProfileDto, PagedResult<T>
- [X] T027 [P] Create exception classes in BaristaNotes/Services/Exceptions/: EntityNotFoundException, ValidationException
- [X] T028 [P] Create IShotService interface in BaristaNotes/Services/IShotService.cs per service-interfaces.md
- [X] T029 [P] Create IEquipmentService interface in BaristaNotes/Services/IEquipmentService.cs per service-interfaces.md
- [X] T030 [P] Create IBeanService interface in BaristaNotes/Services/IBeanService.cs per service-interfaces.md
- [X] T031 [P] Create IUserProfileService interface in BaristaNotes/Services/IUserProfileService.cs per service-interfaces.md
- [X] T032 [P] Create IPreferencesService interface in BaristaNotes/Services/IPreferencesService.cs per service-interfaces.md

### Dependency Injection Setup

- [X] T033 Configure dependency injection in MauiProgram.cs: register DbContext with SQLite, register all repositories (scoped), register all services (scoped/singleton), register IPreferencesStore (singleton)
- [X] T034 Add database migration execution on app startup in MauiProgram.cs using scope.ServiceProvider.GetRequiredService<BaristaNotesContext>().Database.Migrate()

### Base Components

- [X] T036 [P] Create App.cs Maui Reactor root component returning AppShell
- [X] T037 Create AppShell.cs with TabBar navigation structure (4 tabs: Shots, History, Equipment, Beans) using Maui Reactor fluent syntax

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Quick Daily Shot Logging (Priority: P1) 🎯 MVP

**Goal**: Enable home baristas to quickly log espresso shots with recipe pre-population from previous shot, record actual results, add taste rating, and save. This is the core daily workflow.

**Independent Test**: Create a shot record with preset recipe values, record actual results (time, output), add a taste rating (1-5), save, and verify shot appears in activity feed. Test pre-population by creating second shot and verifying form is pre-filled with previous shot's values.

### Tests for User Story 1 (REQUIRED - Write FIRST, Must FAIL) ⚠️

- [X] T038 [P] [US1] Write unit test for ShotService.CreateShotAsync validation (dose range 5-30g, time range 10-60s) in BaristaNotes.Tests/Unit/Services/ShotServiceTests.cs - TEST MUST FAIL
- [X] T039 [P] [US1] Write unit test for ShotService.GetMostRecentShotAsync returning correct last shot in BaristaNotes.Tests/Unit/Services/ShotServiceTests.cs - TEST MUST FAIL
- [X] T040 [P] [US1] Write unit test for PreferencesService storing and retrieving last selections in BaristaNotes.Tests/Unit/Services/PreferencesServiceTests.cs - TEST MUST FAIL
- [X] T041 [P] [US1] Write integration test for shot CRUD operations with InMemory SQLite in BaristaNotes.Tests/Integration/ShotRecordRepositoryTests.cs - TEST MUST FAIL
- [X] T042 [P] [US1] Write integration test for database relationships (shot → bean, shot → equipment) in BaristaNotes.Tests/Integration/DatabaseTests.cs - TEST MUST FAIL

### Implementation for User Story 1

**Service Layer**:
- [X] T045 [US1] Implement PreferencesService in BaristaNotes.Core/Services/PreferencesService.cs with IPreferencesStore abstraction for GetLastDrinkType, SetLastDrinkType, GetLastBeanId, SetLastBeanId, etc. (depends on T040)
- [X] T046 [US1] Implement ShotService in BaristaNotes.Core/Services/ShotService.cs with CreateShotAsync (validation per service-interfaces.md, remember selections via PreferencesService), GetMostRecentShotAsync, GetShotHistoryAsync, UpdateShotAsync, DeleteShotAsync (depends on T038, T039, T041, T045)

**UI Layer** (MVU Pattern - Reactor Components):
- [X] T047 [US1] Create ShotLoggingPage in BaristaNotes/Pages/ShotLoggingPage.cs with Maui Reactor MVU pattern: State record (DoseIn, GrindSetting, ExpectedTime, ExpectedOutput, DrinkType, ActualTime, ActualOutput, Rating, Beans, Machines, Grinders), Messages (Load, Save, UpdateField), Update functions, View rendering with form fields and save button (depends on T046)
- [X] T048 [US1] Create ActivityFeedPage in BaristaNotes/Pages/ActivityFeedPage.cs with Maui Reactor MVU pattern: State record (ShotRecords, IsLoading), Messages (Load, Refresh), Update functions, View with CollectionView virtualization (depends on T046)
- [X] T049 [US1] Create RatingControl component in BaristaNotes/Components/RatingControl.cs with 5-point rating display/input using Maui Reactor (depends on T047)
- [X] T050 [US1] Create ShotRecordCard component in BaristaNotes/Components/ShotRecordCard.cs displaying timestamp, drink type, recipe, actuals, rating using theme styles (depends on T048)

**Integration & Validation**:
- [X] T051 [US1] Run all User Story 1 tests and verify 100% pass (depends on T038-T042, T045-T050)
- [ ] T052 [US1] Manual testing: Log shot, verify pre-population works on second shot, verify shot appears in activity feed (depends on T051)
- [ ] T053 [US1] Performance validation: Form pre-population <500ms, activity feed loads 50 records <1s, 60fps scrolling (depends on T052)

**Checkpoint**: User Story 1 (MVP) is fully functional and independently testable. App can log shots, pre-populate forms, and display history.

---

## Phase 4: User Story 2 - Equipment & Bean Management (Priority: P2)

**Goal**: Enable users to manage equipment inventory (machines, grinders, accessories) and bean collection so they can select them when creating shots and understand how different combinations affect results.

**Independent Test**: Add equipment items (machine, grinder, tamper, puck screen), add bean records (name, roaster, roast date, origin), create shot using specific equipment/bean combination, verify shot history shows correct equipment/bean used, archive equipment/bean and verify historical shots still display correctly.

### Tests for User Story 2 (REQUIRED - Write FIRST, Must FAIL) ⚠️

- [ ] T056 [P] [US2] Write unit test for EquipmentService.CreateEquipmentAsync validation in BaristaNotes.Tests/Unit/Services/EquipmentServiceTests.cs - TEST MUST FAIL
- [ ] T057 [P] [US2] Write unit test for EquipmentService.ArchiveEquipmentAsync preserving shot history in BaristaNotes.Tests/Unit/Services/EquipmentServiceTests.cs - TEST MUST FAIL
- [ ] T058 [P] [US2] Write unit test for BeanService.CreateBeanAsync validation in BaristaNotes.Tests/Unit/Services/BeanServiceTests.cs - TEST MUST FAIL
- [ ] T059 [P] [US2] Write unit test for BeanService.ArchiveBeanAsync preserving shot history in BaristaNotes.Tests/Unit/Services/BeanServiceTests.cs - TEST MUST FAIL
- [ ] T060 [P] [US2] Write integration test for equipment CRUD with InMemory SQLite in BaristaNotes.Tests/Integration/EquipmentRepositoryTests.cs - TEST MUST FAIL
- [ ] T061 [P] [US2] Write integration test for bean CRUD with InMemory SQLite in BaristaNotes.Tests/Integration/BeanRepositoryTests.cs - TEST MUST FAIL

### Implementation for User Story 2

**Service Layer**:
- [ ] T064 [US2] Implement EquipmentService in BaristaNotes.Core/Services/EquipmentService.cs with CreateEquipmentAsync, GetAllActiveEquipmentAsync, GetEquipmentByTypeAsync, UpdateEquipmentAsync, ArchiveEquipmentAsync, DeleteEquipmentAsync (depends on T056, T057, T060)
- [ ] T065 [US2] Implement BeanService in BaristaNotes.Core/Services/BeanService.cs with CreateBeanAsync, GetAllActiveBeansAsync, UpdateBeanAsync, ArchiveBeanAsync, DeleteBeanAsync (depends on T058, T059, T061)

**UI Layer** (MVU Pattern - Reactor Components):
- [ ] T066 [US2] Create EquipmentManagementPage in BaristaNotes/Pages/EquipmentManagementPage.cs with MVU: State (Equipment collection, SelectedType filter), Messages (Load, Create, Update, Archive), Update functions, View with CollectionView and CRUD buttons (depends on T064)
- [ ] T067 [US2] Create BeanManagementPage in BaristaNotes/Pages/BeanManagementPage.cs with MVU: State (Beans collection), Messages (Load, Create, Update, Archive), Update functions, View with CollectionView and CRUD buttons (depends on T065)
- [ ] T068 [US2] Create EquipmentPicker component in BaristaNotes/Components/EquipmentPicker.cs for selecting equipment in shot logging form (depends on T066)
- [ ] T069 [US2] Create BeanPicker component in BaristaNotes/Components/BeanPicker.cs for selecting beans in shot logging form (depends on T067)

**Integration with User Story 1**:
- [ ] T070 [US2] Update ShotLoggingPage State to include equipment/bean selection and integrate EquipmentPicker and BeanPicker components in BaristaNotes/Pages/ShotLoggingPage.cs (depends on T068, T069)
- [ ] T071 [US2] Update ShotRecordCard to display equipment and bean names in activity feed in BaristaNotes/Components/ShotRecordCard.cs (depends on T070)

**Integration & Validation**:
- [ ] T072 [US2] Run all User Story 2 tests and verify 100% pass (depends on T056-T061, T064-T071)
- [ ] T073 [US2] Manual testing: Add equipment, add beans, log shot with specific equipment/bean, archive equipment, verify shot history still shows archived equipment name (depends on T072)
- [ ] T074 [US2] Verify User Story 1 still works: Log shot with equipment/bean selection, verify pre-population includes equipment/bean (depends on T073)

**Checkpoint**: User Stories 1 AND 2 are both fully functional and independently testable. Users can manage equipment/bean inventory and use them in shot logging.

---

## Phase 5: User Story 3 - User Profiles & Activity Feed (Priority: P3)

**Goal**: Enable multi-user households to track who made each shot and who it was made for, and view all shots in a chronological activity feed showing complete shot information including profiles.

**Independent Test**: Create user profiles (names, optional avatars), select "made by" and "made for" profiles when logging shots, view activity feed with profile information displayed, filter activity feed by specific user (as barista or consumer), verify profile selections are pre-populated on next shot.

### Tests for User Story 3 (REQUIRED - Write FIRST, Must FAIL) ⚠️

- [ ] T078 [P] [US3] Write unit test for UserProfileService.CreateProfileAsync validation in BaristaNotes.Tests/Unit/Services/UserProfileServiceTests.cs - TEST MUST FAIL
- [ ] T079 [P] [US3] Write unit test for ShotService.GetShotHistoryByUserAsync filtering in BaristaNotes.Tests/Unit/Services/ShotServiceTests.cs - TEST MUST FAIL
- [ ] T080 [P] [US3] Write integration test for user profile CRUD with InMemory SQLite in BaristaNotes.Tests/Integration/UserProfileRepositoryTests.cs - TEST MUST FAIL
- [ ] T081 [P] [US3] Write integration test for shot-to-profile relationships (MadeBy, MadeFor) in BaristaNotes.Tests/Integration/DatabaseTests.cs - TEST MUST FAIL

### Implementation for User Story 3

**Service Layer**:
- [ ] T084 [US3] Implement UserProfileService in BaristaNotes.Core/Services/UserProfileService.cs with CreateProfileAsync, GetAllProfilesAsync, UpdateProfileAsync, DeleteProfileAsync (depends on T078, T080)
- [ ] T085 [US3] Update ShotService in BaristaNotes.Core/Services/ShotService.cs to add GetShotHistoryByUserAsync filtering method (depends on T079, T081)

**UI Layer** (MVU Pattern - Reactor Components):
- [ ] T086 [US3] Create UserProfileManagementPage in BaristaNotes/Pages/UserProfileManagementPage.cs with MVU: State (Profiles collection), Messages (Load, Create, Update, Delete), Update functions, View with CollectionView and CRUD buttons (depends on T084)
- [ ] T087 [US3] Add UserProfile tab to AppShell in BaristaNotes/AppShell.cs (depends on T086)

**Integration with User Stories 1 & 2**:
- [ ] T088 [US3] Update ShotLoggingPage State to add MadeBy and MadeFor profile selection properties and picker UI in BaristaNotes/Pages/ShotLoggingPage.cs (depends on T086)
- [ ] T089 [US3] Update PreferencesService to remember last MadeBy and MadeFor selections in BaristaNotes.Core/Services/PreferencesService.cs (depends on T088)
- [ ] T090 [US3] Update ShotRecordCard to display barista and consumer profile names in activity feed in BaristaNotes/Components/ShotRecordCard.cs (depends on T088)
- [ ] T091 [US3] Update ActivityFeedPage State and View to add user filter picker UI and filtering logic in BaristaNotes/Pages/ActivityFeedPage.cs (depends on T085, T090)

**Integration & Validation**:
- [ ] T092 [US3] Run all User Story 3 tests and verify 100% pass (depends on T078-T081, T084-T091)
- [ ] T093 [US3] Manual testing: Create profiles, log shot with profiles selected, verify activity feed shows profiles, filter by user, verify pre-population includes profiles (depends on T092)
- [ ] T094 [US3] Verify User Stories 1 & 2 still work: Log shot with equipment/bean/profiles, verify all selections pre-populate on next shot (depends on T093)

**Checkpoint**: All user stories (1, 2, 3) are fully functional and independently testable. Full feature set complete.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories and final quality gates

- [ ] T098 [P] Add loading indicators to all async operations using LoadingIndicator component in BaristaNotes/Components/LoadingIndicator.cs
- [ ] T099 [P] Add error handling with user-friendly messages using CommunityToolkit.Maui Toast for validation errors and save confirmations
- [ ] T100 [P] Add accessibility semantics to all interactive controls (SemanticProperties.Description) for screen reader support
- [ ] T101 [P] Add unit tests for remaining services to achieve 80% minimum coverage in BaristaNotes.Tests/Unit/Services/
- [ ] T102 [P] Add unit tests for remaining Pages (MVU components) to achieve 80% minimum coverage in BaristaNotes.Tests/Unit/Pages (MVU components)/
- [ ] T103 Performance optimization: Profile app launch time (target <2s), form pre-population (target <500ms), activity feed loading (target <1s for 50 records), scrolling (target 60fps)
- [ ] T104 Memory profiling: Verify memory footprint <200MB, check for leaks in Pages (MVU components), dispose DbContext properly
- [ ] T105 [P] Add documentation: Update README.md with setup instructions, feature overview, and screenshots
- [ ] T106 [P] Code cleanup: Remove unused imports, apply consistent formatting, verify naming conventions
- [ ] T107 Static analysis: Run .NET analyzers, fix warnings, ensure no code smells
- [ ] T108 Security: Verify no sensitive data logged, validate all user inputs in services, check for SQL injection risks (EF Core parameterizes)
- [ ] T109 Accessibility audit: Verify WCAG 2.1 AA compliance (touch targets 44x44px, color contrast ratios, screen reader navigation)
- [ ] T110 Constitution compliance verification: Code quality (separation of concerns ✓), Test-first (80% coverage ✓), UX consistency (theme-based styling ✓), Performance (<2s launch, <500ms pre-pop, 60fps ✓)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Foundational phase completion - MVP deliverable
- **User Story 2 (Phase 4)**: Depends on Foundational phase completion - Can run parallel with US3 if staffed
- **User Story 3 (Phase 5)**: Depends on Foundational phase completion - Can run parallel with US2 if staffed
- **Polish (Phase 6)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories - MVP standalone
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - Integrates with US1 (shot form equipment/bean selection) but independently testable
- **User Story 3 (P3)**: Can start after Foundational (Phase 2) - Integrates with US1 (shot form profile selection) but independently testable

### Within Each User Story

**CRITICAL TEST-FIRST WORKFLOW**:
1. Write ALL tests for the story FIRST (T038-T044 for US1)
2. Verify ALL tests FAIL (no implementation yet)
3. Implement services (T045-T046 for US1)
4. Implement Pages (MVU components) (T047-T048 for US1)
5. Implement UI (T049-T052 for US1)
6. Verify ALL tests PASS (T053 for US1)
7. Manual validation (T054-T055 for US1)

**Within implementation**:
- Services before Pages (MVU components)
- Pages (MVU components) before UI
- Core implementation before integration
- Story complete before moving to next priority

### Parallel Opportunities

**Phase 1 (Setup)**:
- Tasks T003, T005, T006, T007 can run in parallel (different files)

**Phase 2 (Foundational)**:
- Models: T008-T013 can run in parallel (different entity files)
- Repositories: T018-T025 can run in parallel (different repository files)
- Service interfaces/DTOs: T026-T032 can run in parallel (different interface files)
- Base components: T035, T036 can run in parallel

**User Story 1 Tests**:
- All test tasks T038-T044 can run in parallel (different test files)

**User Story 2 Tests**:
- All test tasks T056-T063 can run in parallel (different test files)

**User Story 3 Tests**:
- All test tasks T078-T083 can run in parallel (different test files)

**Polish Phase**:
- Tasks T098-T102, T105-T106 can run in parallel (different concerns)

**Multiple User Stories** (if team capacity allows):
- After Phase 2 completes, US2 and US3 can start in parallel (different entities/services)
- US1 should complete first (MVP priority), then US2 and US3 can proceed in parallel

---

## Parallel Example: User Story 1

```bash
# Step 1: Launch all tests for User Story 1 together (WRITE FIRST):
Task T038: "Write unit test for ShotService.CreateShotAsync validation"
Task T039: "Write unit test for ShotService.GetMostRecentShotAsync"
Task T040: "Write unit test for PreferencesService storing/retrieving"
Task T041: "Write integration test for shot CRUD operations"
Task T042: "Write integration test for database relationships"
Task T043: "Write Page test for ShotLoggingPage.LoadDataAsync"
Task T044: "Write Page test for ShotLoggingPage.SaveShotAsync"

# Step 2: Verify ALL tests FAIL (no implementation yet)

# Step 3: Implement services sequentially (T045 first, then T046 depends on it)

# Step 4: Implement Pages (MVU components) in parallel:
Task T047: "Create ShotLoggingPage"
Task T048: "Create ActivityFeedPage"

# Step 5: Implement UI components in parallel:
Task T049: "Create ShotLoggingPage"
Task T050: "Create RatingControl component"
Task T051: "Create ActivityFeedPage"
Task T052: "Create ShotRecordCard component"

# Step 6: Run all tests again - should now PASS (T053)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

**Fastest path to working app**:

1. Complete Phase 1: Setup (T001-T007) → ~1 day
2. Complete Phase 2: Foundational (T008-T037) → ~2-3 days (CRITICAL - blocks everything)
3. Complete Phase 3: User Story 1 (T038-T055) → ~3-4 days
   - Write tests FIRST (T038-T044)
   - Implement services (T045-T046)
   - Implement Pages (MVU components) (T047-T048)
   - Implement UI (T049-T052)
   - Validate (T053-T055)
4. **STOP and VALIDATE**: Test User Story 1 independently - can log shots, see history, pre-population works
5. **MVP COMPLETE** - Deploy/demo if ready

**MVP delivers**: Quick shot logging with pre-population, taste ratings, activity feed. This is the core value proposition.

### Incremental Delivery (Recommended)

**Each phase adds value without breaking previous work**:

1. Complete Setup + Foundational (Phases 1-2) → **Foundation ready** (~3-4 days)
2. Add User Story 1 (Phase 3) → Test independently → **MVP release** (~3-4 days)
3. Add User Story 2 (Phase 4) → Test independently → **Equipment/Bean management release** (~2-3 days)
4. Add User Story 3 (Phase 5) → Test independently → **Multi-user release** (~2-3 days)
5. Polish (Phase 6) → **Production-ready release** (~1-2 days)

**Total estimate**: ~12-16 days for full feature (single developer)

### Parallel Team Strategy

**With 3 developers**:

1. **Week 1**: Team completes Setup + Foundational together (Phases 1-2)
2. **Week 2**: Once Foundational is done:
   - Developer A: User Story 1 (Phase 3) - MVP priority
   - Developer B: User Story 2 (Phase 4) - starts in parallel
   - Developer C: User Story 3 (Phase 5) - starts in parallel
3. **Week 3**: Integration testing, Polish (Phase 6)

**Total estimate**: ~2-3 weeks for full feature (3 developers)

---

## Notes

- **[P] marker**: Tasks can run in parallel (different files, no dependencies)
- **[Story] label**: Maps task to specific user story for traceability (ONLY in user story phases)
- **Test-First**: Write tests FIRST (T038-T044), verify they FAIL, then implement (T045-T052)
- **80% coverage minimum**: Constitution requirement - ALL user stories must be tested
- **100% coverage for critical paths**: Data persistence (repositories), shot pre-population logic
- Each user story should be independently completable and testable
- Verify tests fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Performance targets: <2s launch, <500ms pre-population, 60fps scrolling, <1s feed load

---

## Task Count Summary

- **Phase 1 (Setup)**: 7 tasks
- **Phase 2 (Foundational)**: 30 tasks (CRITICAL BLOCKING PHASE)
- **Phase 3 (User Story 1 - MVP)**: 18 tasks (7 tests + 11 implementation)
- **Phase 4 (User Story 2)**: 22 tasks (8 tests + 14 implementation)
- **Phase 5 (User Story 3)**: 20 tasks (6 tests + 14 implementation)
- **Phase 6 (Polish)**: 13 tasks

**Total**: 110 tasks

**Test tasks**: 21 (19% of total - ensures 80%+ coverage per constitution)
**Tasks per user story**:
- US1: 18 tasks (MVP)
- US2: 22 tasks
- US3: 20 tasks

**Parallel opportunities**: 35+ tasks marked [P] can run in parallel

**MVP scope (Phases 1-3)**: 55 tasks → ~1-2 weeks (single developer)
**Full feature (Phases 1-6)**: 110 tasks → ~2-3 weeks (single developer), ~2-3 weeks (3 developers in parallel)

---

## Suggested MVP Scope

**Minimum Viable Product**: Phases 1-3 (User Story 1 only)

**Delivers**:
- Quick shot logging with pre-populated forms
- Recipe tracking (dose, grind, time, output, drink type)
- Actual results recording
- 5-point taste rating
- Activity feed with shot history
- Offline-first SQLite persistence
- Theme-based UI
- 80% test coverage

**Defer to later iterations**:
- Equipment & Bean management (Phase 4)
- User profiles (Phase 5)
- Polish & optimizations (Phase 6)

**Why this MVP**: User Story 1 delivers the core daily workflow that provides immediate value. Users can log shots and see patterns without needing full equipment/bean/profile management. This validates the app concept and workflow before investing in additional features.

---

## Constitution Compliance Verification

**Code Quality Standards** (Principle I):
- ✅ Repository pattern separates data access from business logic (Phase 2)
- ✅ Service layer implements single responsibility (Phase 2)
- ✅ Pages (MVU components) separate from UI and business logic (User story phases)
- ✅ Theme-based styling prevents scattered definitions (Phase 1)
- ✅ DTOs decouple UI from persistence (Phase 2)

**Test-First Development** (Principle II):
- ✅ Tests written FIRST for each user story (T038-T044, T056-T063, T078-T083)
- ✅ Tests MUST FAIL before implementation
- ✅ 80% minimum coverage enforced (T101-T102)
- ✅ 100% coverage for critical paths (data persistence, pre-population)
- ✅ Unit tests, integration tests, Page tests all included

**User Experience Consistency** (Principle III):
- ✅ Native .NET MAUI controls for platform consistency
- ✅ Centralized theme (Colors.xaml, Styles.xaml) in Phase 1
- ✅ Icon font for consistent iconography (Phase 1)
- ✅ 44x44px touch targets (MAUI defaults)
- ✅ Accessibility semantics added (T100)

**Performance Requirements** (Principle IV):
- ✅ App launch <2s target (T103)
- ✅ Form pre-population <500ms target (T055, T103)
- ✅ 60fps scrolling target (T055, T103)
- ✅ Activity feed <1s for 50 records target (T055, T103)
- ✅ Offline-capable (SQLite local storage)

**All constitutional principles satisfied** ✅
