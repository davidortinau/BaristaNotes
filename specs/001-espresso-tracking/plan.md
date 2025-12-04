# Implementation Plan: Espresso Shot Tracking & Management

**Branch**: `001-espresso-tracking` | **Date**: 2025-12-02 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-espresso-tracking/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Build a mobile-first espresso tracking application enabling home baristas to log daily shots with equipment, beans, recipes, and taste ratings. The app provides quick shot logging by pre-populating forms from previous entries, manages equipment/bean inventory, supports multi-user profiles, and displays shot history in an activity feed timeline. Technical approach uses .NET MAUI with Maui Reactor for fluent UI, SQLite with CoreSync for offline-first data persistence, and native UI controls styled via application theme.

## Technical Context

**Language/Version**: C# 12 / .NET 8.0  
**Primary Dependencies**: 
- .NET MAUI (Multi-platform App UI)
- Maui Reactor (Reactor.Maui NuGet, latest preview) - Fluent UI syntax
- CommunityToolkit.Maui - Helpers and views
- SQLite with Entity Framework Core
- CoreSync.Sqlite & CoreSync.Http.Client - Sync capabilities
- MauiReactor Integration libraries (https://github.com/adospace/mauireactor-integration)

**Storage**: SQLite database (local, offline-first) with Entity Framework Core, CoreSync for future sync capabilities  
**Testing**: xUnit or NUnit for unit tests, .NET MAUI testing frameworks for UI tests  
**Target Platform**: iOS 15+ and Android 8.0+ (API 26+)  
**Project Type**: Mobile (cross-platform .NET MAUI application)  
**Performance Goals**: 
- App launch: <2 seconds
- Shot form pre-population: <500ms
- UI interactions: 60fps, <100ms perceived response
- Activity feed: Load 50 records <1 second

**Constraints**: 
- Offline-capable (all features work without network)
- Mobile memory footprint <200MB
- Local SQLite database only (no cloud sync in MVP)
- Native UI controls preferred over custom drawing
- Thin visual tree for performance

**Scale/Scope**: 
- Single-user device (no authentication initially)
- ~10-20 equipment items per user
- ~10-20 bean records per user
- ~1000+ shot records per year
- 5-10 user profiles per household
- Activity feed: Virtual scrolling for 100+ records

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

All features must demonstrate alignment with constitutional principles:

- [x] **Code Quality Standards**: Design enables readable, maintainable code through:
  - Maui Reactor's fluent syntax promotes self-documenting UI code
  - Entity Framework models follow single responsibility (one entity per concern)
  - Services layer separates business logic from data access and UI
  - Theme-based styling prevents scattered style definitions
  - Repository pattern for data access provides clear abstraction
  
- [x] **Test-First Development**: Test scenarios defined in spec.md with stakeholder acceptance criteria:
  - Unit tests: ViewModels, Services, Data repositories (xUnit/NUnit)
  - Integration tests: Database operations, CoreSync functionality
  - UI tests: .NET MAUI testing for critical flows (shot logging, activity feed)
  - Coverage target: 80% minimum (100% for data persistence logic)
  
- [x] **User Experience Consistency**: 
  - Native .NET MAUI controls ensure platform consistency (iOS/Android)
  - CommunityToolkit.Maui provides standardized components
  - Centralized theme dictionary for all styles and colors
  - Icon font for consistent iconography across platforms
  - Touch targets: 44x44px minimum (MAUI default sizing)
  - Accessibility: Platform screen readers supported via MAUI semantics
  
- [x] **Performance Requirements**: Targets defined and achievable:
  - App launch <2s: .NET MAUI native compilation, SQLite local storage
  - Form pre-population <500ms: EF Core queries with indexed lookups
  - 60fps: Thin visual tree, native controls, virtualized list rendering
  - Offline-capable: SQLite local database, no network dependencies for MVP
  - Memory <200MB: Native controls, efficient image handling, GC optimization

**Violations requiring justification**: None. All constitutional principles satisfied.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

BaristaNotes/                           # Solution root
├── BaristaNotes.sln                    # Visual Studio solution file
├── BaristaNotes.Core/                  # Shared business logic library (net10.0)
│   ├── BaristaNotes.Core.csproj
│   ├── Models/                         # Domain entities (EF Core models)
│   │   ├── Equipment.cs
│   │   ├── Bean.cs
│   │   ├── UserProfile.cs
│   │   ├── ShotRecord.cs
│   │   └── Enums/
│   │       ├── EquipmentType.cs
│   │       └── DrinkType.cs
│   │
│   ├── Data/                           # Data access layer
│   │   ├── BaristaNotesContext.cs      # EF Core DbContext
│   │   ├── Repositories/
│   │   │   ├── IRepository.cs
│   │   │   ├── EquipmentRepository.cs
│   │   │   ├── BeanRepository.cs
│   │   │   ├── UserProfileRepository.cs
│   │   │   └── ShotRecordRepository.cs
│   │   └── Migrations/                 # EF Core migrations
│   │
│   └── Services/                       # Business logic layer
│       ├── IShotService.cs
│       ├── ShotService.cs              # Shot logging, pre-population logic
│       ├── IEquipmentService.cs
│       ├── EquipmentService.cs
│       ├── IBeanService.cs
│       ├── BeanService.cs
│       ├── IUserProfileService.cs
│       ├── UserProfileService.cs
│       ├── IPreferencesService.cs
│       ├── PreferencesService.cs       # Remember last selections
│       └── DTOs/                       # Data transfer objects
│
├── BaristaNotes/                       # Main MAUI project
│   ├── BaristaNotes.csproj
│   ├── MauiProgram.cs                  # App initialization, DI setup
│   ├── App.cs                          # Maui Reactor root component
│   ├── AppShell.cs                     # Shell navigation structure
│   │
│   ├── Infrastructure/                 # Platform-specific implementations
│   │   └── MauiPreferencesStore.cs     # MAUI implementation of IPreferencesStore
│   │
│   ├── Pages/                          # Maui Reactor page components (MVU)
│   │   ├── ShotLoggingPage.cs          # Quick shot logging form
│   │   ├── ActivityFeedPage.cs         # Timeline/history view
│   │   ├── EquipmentManagementPage.cs  # Equipment CRUD
│   │   ├── BeanManagementPage.cs       # Bean CRUD
│   │   └── UserProfileManagementPage.cs # Profile CRUD
│   │
│   ├── Components/                     # Reusable Reactor UI components
│   │   ├── ShotRecordCard.cs           # Shot display in feed
│   │   ├── EquipmentPicker.cs          # Equipment selection control
│   │   ├── BeanPicker.cs               # Bean selection control
│   │   ├── RatingControl.cs            # 5-point rating input
│   │   └── LoadingIndicator.cs         # Loading state component
│   │
│   ├── Resources/                      # MAUI resources
│   │   ├── Fonts/
│   │   │   └── IconFont.ttf            # Icon font file
│   │   └── Images/                     # Image assets
│   │
│   └── Platforms/                      # Platform-specific code
│       ├── Android/
│       └── iOS/
│
└── BaristaNotes.Tests/                 # Test project
    ├── BaristaNotes.Tests.csproj
    ├── Mocks/
    │   └── MockPreferencesStore.cs     # In-memory preferences for testing
    ├── Unit/
    │   ├── Services/
    │   │   ├── ShotServiceTests.cs
    │   │   ├── EquipmentServiceTests.cs
    │   │   ├── BeanServiceTests.cs
    │   │   └── PreferencesServiceTests.cs
    │   └── Repositories/
    │       ├── ShotRecordRepositoryTests.cs
    │       └── EquipmentRepositoryTests.cs
    └── Integration/
        ├── DatabaseTests.cs             # EF Core operations
        └── CoreSyncTests.cs             # Sync capability tests
```

**Structure Decision**: MVU architecture with Maui Reactor. Core library (net10.0) contains all business logic, models, and data access - testable and platform-agnostic. MAUI project contains only UI (Reactor components using MVU pattern) and platform-specific implementations. No ViewModels needed - Reactor components manage their own state via MVU pattern. Tests can reference Core library directly for full test coverage.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No violations. All constitutional principles satisfied without requiring complexity justifications.

---

## Phase 0: Research Complete ✅

**Output**: `research.md` (13.6KB)

**Key Decisions**:
- .NET MAUI + Maui Reactor for cross-platform mobile with fluent UI
- SQLite + EF Core for offline-first data persistence
- CoreSync metadata prepared (sync not implemented in MVP)
- CommunityToolkit.Maui for standardized controls
- Icon font for consistent iconography
- Theme-based styling architecture
- MVU (Model-View-Update) + Services + Repositories separation
- xUnit with InMemory SQLite for testing

All NEEDS CLARIFICATION items resolved. Ready for Phase 1.

---

## Phase 1: Design & Contracts Complete ✅

**Outputs**:
- `data-model.md` (18.2KB) - 5 entities with EF Core configuration
- `contracts/service-interfaces.md` (15.0KB) - Service contracts and DTOs
- `quickstart.md` (23.6KB) - Complete implementation guide

**Data Model**:
- Equipment (machines, grinders, accessories)
- Bean (coffee bean records)
- UserProfile (baristas and consumers)
- ShotRecord (logged espresso shots)
- ShotEquipment (junction for accessories)
- All entities include CoreSync metadata for future sync
- Soft deletes implemented via IsDeleted flag

**Service Contracts**:
- IShotService: Shot logging, retrieval, history
- IEquipmentService: Equipment CRUD and archiving
- IBeanService: Bean CRUD and archiving
- IUserProfileService: Profile management
- IPreferencesService: Remember last selections
- All DTOs defined with validation rules

**Quickstart Guide**:
- Step-by-step implementation from data layer through UI
- NuGet package installation
- Migration creation and execution
- Repository, Service, ViewModel, and Page implementation
- Dependency injection configuration
- Testing setup and validation

---

## Constitution Re-Check (Post-Design) ✅

- [x] **Code Quality Standards**: ✅ PASS
  - Repository pattern separates data access from business logic
  - Service layer implements single responsibility (one service per domain)
  - DTOs decouple UI from persistence layer
  - Theme-based styling prevents scattered style definitions
  - Maui Reactor fluent syntax promotes readable UI code

- [x] **Test-First Development**: ✅ PASS
  - Test structure defined in quickstart.md
  - Unit tests: Services, ViewModels, Repositories
  - Integration tests: Database operations, CoreSync metadata
  - xUnit with InMemory SQLite configured
  - 80% coverage target with 100% for data persistence

- [x] **User Experience Consistency**: ✅ PASS
  - Native .NET MAUI controls ensure platform consistency
  - CommunityToolkit.Maui provides standardized components
  - Centralized theme dictionary (Colors.xaml, Styles.xaml)
  - Icon font for consistent iconography
  - 44x44px touch targets (MAUI defaults)
  - Accessibility: Platform screen readers via MAUI semantics

- [x] **Performance Requirements**: ✅ PASS
  - App launch <2s: Native compilation, SQLite local storage
  - Form pre-population <500ms: Indexed queries, AsNoTracking()
  - 60fps: Thin visual tree, native controls, CollectionView virtualization
  - Offline-capable: SQLite embedded database, no network dependencies
  - Memory <200MB: Native controls, efficient GC usage

**Final Verdict**: All constitutional principles satisfied. Design ready for implementation.

---

## Implementation Readiness

**Ready to Proceed**: ✅ YES

**Artifacts Generated**:
1. ✅ plan.md (this file) - Technical context, structure, gates
2. ✅ research.md - All technology decisions with rationale
3. ✅ data-model.md - Complete entity definitions and EF configuration
4. ✅ contracts/service-interfaces.md - Service contracts and DTOs
5. ✅ quickstart.md - Step-by-step implementation guide

**Next Command**: `/speckit.tasks` to generate implementation task breakdown

---

## Notes for Implementation

**Critical Path Items**:
1. Database schema creation (models + migrations) - BLOCKING
2. Repository implementation - BLOCKING
3. Service implementation with DTOs - BLOCKING
4. ViewModel state management - Non-blocking (parallel after services)
5. UI components with Reactor syntax - Non-blocking (parallel after ViewModels)

**Performance Hotspots**:
- Activity feed query: Index on Timestamp DESC (already in data model)
- Pre-population query: Cache in IPreferencesService (design includes this)
- CollectionView virtualization: Use built-in ItemsUpdatingScrollMode

**Testing Priority**:
- P0 (Critical): Data persistence (shots, equipment, beans, profiles)
- P0 (Critical): Shot pre-population logic
- P1 (High): Service validation logic
- P2 (Medium): ViewModel state management
- P3 (Low): UI component rendering

**Deployment Considerations**:
- Database migrations: Automated on app launch (MauiProgram.cs)
- First-run experience: Seed with sample data or onboarding flow
- Version compatibility: CoreSync metadata future-proofs schema
