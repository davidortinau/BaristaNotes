# Implementation Plan: Bean Rating Tracking and Bag Management

**Branch**: `001-bean-rating-tracking` | **Date**: 2025-12-07 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-bean-rating-tracking/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Add two-level rating system to BaristaNotes: (1) Bean-level aggregate ratings across all bags to inform reordering decisions, and (2) Bag-level ratings for individual roast batches. Introduce Bag entity to separate bean varieties from physical inventory, allowing users to track multiple bags of the same bean over time. Display rating distributions (5 stars: X, 4 stars: Y, etc.) similar to product reviews.

## Technical Context

**Language/Version**: C# .NET 10.0  
**Primary Dependencies**: .NET MAUI 10.0, Entity Framework Core 10.0, SQLite, Reactor.Maui 4.0.3-beta  
**Storage**: SQLite database via EF Core (local, with CoreSync for future cloud sync)  
**Testing**: xUnit with BaristaNotes.Tests project  
**Target Platform**: Cross-platform mobile (iOS 15+, Android 21+, macOS Catalyst, Windows 10)  
**Project Type**: Mobile (.NET MAUI) with separate Core library  
**Performance Goals**: Bean detail view <2s load (p95), rating calculations <500ms (p95), UI interactions <100ms feedback  
**Constraints**: Offline-first (local SQLite), must work with existing CoreSync infrastructure, responsive design required  
**Scale/Scope**: Single-user mobile app, ~50 beans typical, ~500 shots/year, up to 100 bags per bean edge case

**Known Technical Details**:
- Existing Bean entity in BaristaNotes.Core.Models has RoastDate (currently single bag per bean)
- ShotRecord.Rating already exists (nullable int, likely 1-5 scale)
- BaristaNotesContext uses EF Core with manual OnModelCreating configuration
- UI uses Reactor.Maui (reactive UI framework), not traditional XAML
- Repository pattern used in Core/Data/Repositories
- Services layer in Core/Services with interface/implementation pattern

**NEEDS CLARIFICATION**:
1. **Bean-Bag data model migration**: How to migrate existing Beans (with RoastDate) to new Bag entity while preserving existing shot associations? Need strategy for zero-downtime schema evolution.
2. **Rating calculation strategy**: Should aggregate ratings be calculated on-demand (LINQ queries) or denormalized/cached (stored columns with triggers/updates)? Performance vs. data consistency trade-off.
3. **Rating distribution storage**: Store distribution as JSON column, separate RatingDistribution table, or compute on-the-fly? Impact on query performance and schema complexity.
4. **Bag selection UX**: When logging a shot, how does user select which bag? Dropdown, modal, inline picker? Must integrate with existing ShotLoggingPage.cs (44KB, complex Reactor component).
5. **Empty state handling**: UI patterns for beans with no bags, bags with no shots, etc. Must align with existing design system.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

All features must demonstrate alignment with constitutional principles:

- [x] **Code Quality Standards**: Design enables single-responsibility code (Bag entity separate from Bean, rating calculations isolated in service layer). Repository pattern maintains existing architecture. No unjustified complexity - bag-bean separation is essential for requirement.
- [x] **Test-First Development**: Test scenarios defined in spec (P1-P3 user stories with acceptance criteria). Will write unit tests for rating calculations (100% coverage per NFR-Q1), integration tests for bag CRUD, contract tests for service interfaces. Stakeholder approval: feature spec approved above.
- [x] **User Experience Consistency**: Uses existing Reactor.Maui patterns (see ShotLoggingPage.cs). Accessibility requirements explicit in NFR-A1-A4 (keyboard nav, WCAG 2.1 AA, 44px touch targets, screen reader). Responsive design required per NFR-UX4.
- [x] **Performance Requirements**: Targets defined: <2s bean detail load, <500ms rating calc, <100ms UI feedback (NFR-P1-P4). Will instrument queries and monitor performance. Architecture supports requirement - local SQLite queries with proper indexing.

**Violations requiring justification**: None. All constitutional principles can be met within existing architecture.

**Post-Design Re-check** (completed after Phase 1):
- [x] **Code Quality**: Data model maintains single responsibility (Bag=inventory, Bean=variety, Shot=event, RatingService=calculations). No entity does multiple jobs.
- [x] **Test-First**: Test plan in quickstart.md covers all acceptance scenarios. Tests written before implementation (Red-Green-Refactor cycle).
- [x] **UX Consistency**: UI components follow existing Reactor.Maui patterns (VStack/HStack, Picker, Button). Rating display uses standard progress bars. Empty states match existing pattern (inline messages + CTA).
- [x] **Performance**: Query patterns optimized with composite indexes (BagId+Rating). On-demand calculations tested to meet <500ms target. No N+1 queries (explicit .Include() used).

**Final Gate**: ✅ PASS - All constitutional principles satisfied. Ready for Phase 2 (task breakdown).

## Project Structure

### Documentation (this feature)

```text
specs/001-bean-rating-tracking/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output: data migration strategy, rating calculations, UX patterns
├── data-model.md        # Phase 1 output: Bag entity, rating aggregates, migration plan
├── quickstart.md        # Phase 1 output: developer onboarding for this feature
├── contracts/           # Phase 1 output: service interfaces, DTOs
│   ├── IBagService.cs
│   ├── IRatingService.cs
│   └── DTOs/
│       ├── BagSummaryDto.cs
│       └── RatingAggregateDto.cs
├── checklists/
│   └── requirements.md  # Already completed (spec validation)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
BaristaNotes.Core/               # Shared business logic
├── Models/
│   ├── Bean.cs                  # Modified: remove RoastDate (moved to Bag)
│   ├── Bag.cs                   # NEW: physical bag entity
│   ├── ShotRecord.cs            # Modified: add BagId FK (replace direct BeanId)
│   └── Enums/
├── Data/
│   ├── BaristaNotesContext.cs   # Modified: add Bag DbSet, configure relationships
│   └── Repositories/
│       ├── IBeanRepository.cs   # Modified: add rating aggregate queries
│       ├── BeanRepository.cs
│       ├── IBagRepository.cs    # NEW: bag CRUD operations
│       └── BagRepository.cs     # NEW
├── Services/
│   ├── IBeanService.cs          # Modified: add GetWithRatings methods
│   ├── BeanService.cs           # Modified
│   ├── IBagService.cs           # NEW: bag management service
│   ├── BagService.cs            # NEW
│   ├── IShotService.cs          # Modified: add bag selection parameter
│   ├── ShotService.cs           # Modified
│   ├── IRatingService.cs        # NEW: rating calculations (bean & bag level)
│   ├── RatingService.cs         # NEW
│   └── DTOs/
│       ├── BagSummaryDto.cs     # NEW
│       └── RatingAggregateDto.cs # NEW
├── Migrations/
│   └── [timestamp]_AddBagEntity.cs  # NEW: EF migration (via dotnet ef migrations add)
└── barista_notes.db             # SQLite database (schema updated via migration)

BaristaNotes/                    # MAUI UI project
├── Pages/
│   ├── BeanDetailPage.cs        # Modified: add rating display + bag list
│   ├── BagDetailPage.cs         # NEW: individual bag view with ratings
│   ├── ShotLoggingPage.cs       # Modified: add bag selection UI
│   └── BagFormPage.cs           # NEW: add new bag for existing bean
├── Components/
│   ├── RatingDisplayComponent.cs    # NEW: star rating + distribution bars
│   └── BagSelectorComponent.cs      # NEW: bag picker for shot logging
└── Services/
    └── (platform-specific services, if needed)

BaristaNotes.Tests/
├── Unit/
│   ├── RatingServiceTests.cs    # NEW: test aggregate calculations
│   ├── BagServiceTests.cs       # NEW
│   └── BeanServiceTests.cs      # Modified: add rating query tests
├── Integration/
│   ├── BagRepositoryTests.cs    # NEW: test EF queries with ratings
│   └── DataMigrationTests.cs    # NEW: test Bean→Bag migration
└── Contract/
    ├── IBagServiceContractTests.cs  # NEW
    └── IRatingServiceContractTests.cs # NEW
```

**Structure Decision**: Using existing three-project structure (Core library, MAUI app, Tests). Core contains all business logic and EF models per existing pattern. MAUI app handles UI with Reactor.Maui components. Repository pattern maintained for data access. Services layer uses interface/implementation separation per current architecture.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No constitutional violations. Table omitted.
