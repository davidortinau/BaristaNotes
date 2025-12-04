# Implementation Plan: CRUD Settings with Modal Bottom Sheets

**Branch**: `002-crud-settings-modals` | **Date**: December 2, 2025 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/002-crud-settings-modals/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Restructure navigation to make Activity Feed the primary page, move Equipment/Beans/User Profiles CRUD operations to a Settings page accessible via toolbar, and implement bottom sheet modals for all CRUD forms using **Plugin.Maui.BottomSheet** with MauiReactor's `[Scaffold]` pattern and MVU components.

## Technical Context

**Language/Version**: C# / .NET 10.0
**Primary Dependencies**: MauiReactor (Reactor.Maui 4.0.3-beta), Plugin.Maui.BottomSheet (NEW), CommunityToolkit.Maui 9.1.1, Entity Framework Core 8.0.0
**Bottom Sheet**: Plugin.Maui.BottomSheet with MauiReactor scaffold integration (from official mauireactor-integration samples)
**Storage**: SQLite (local database via EF Core)
**Testing**: xUnit 2.9.3, Moq 4.20.72, EF Core InMemory 8.0.0
**Target Platform**: iOS 15+, Android 21+, MacCatalyst 15+, Windows 10.0.17763+
**Project Type**: Mobile cross-platform MAUI application
**Performance Goals**: Modal animations <300ms, list loads <500ms, save operations <1s
**Constraints**: <200MB memory footprint, offline-capable (local SQLite), touch targets min 44x44px
**Scale/Scope**: Single-device personal app, ~5-10 screens, hundreds of records per entity

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

All features must demonstrate alignment with constitutional principles:

- [x] **Code Quality Standards**: Design enables readable, maintainable code with reusable modal components. Single responsibility: separate components for forms, lists, and modal shells. DRY: shared `BottomSheet` scaffold wrapper and `BottomSheetExtensions` for all CRUD operations.
- [x] **Test-First Development**: Test scenarios defined in spec acceptance criteria. Unit tests for service methods, integration tests for CRUD flows. Coverage target: 80% for new CRUD components.
- [x] **User Experience Consistency**: Bottom sheet modals follow platform-native patterns via Plugin.Maui.BottomSheet. Pure MauiReactor form components inside sheets. Consistent styling across all modals. Touch targets meet 44x44px minimum. Loading states for async operations.
- [x] **Performance Requirements**: Modal animations target <300ms using native bottom sheet animations. List rendering optimized with CollectionView. Save operations async with immediate visual feedback.

**Violations requiring justification**: None identified - all principles can be met with the proposed approach.

## Project Structure

### Documentation (this feature)

```text
specs/002-crud-settings-modals/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
BaristaNotes/                          # MAUI app project
├── App.cs
├── AppShell.cs                        # [MODIFY] Simplify tab bar to 2 tabs
├── MauiProgram.cs                     # [MODIFY] Add .UseBottomSheet()
├── Pages/
│   ├── ActivityFeedPage.cs            # [MODIFY] Add toolbar item, make primary
│   ├── ShotLoggingPage.cs
│   ├── SettingsPage.cs                # [NEW] Settings hub with navigation to management lists
│   ├── EquipmentManagementPage.cs     # [MODIFY] Integrate with bottom sheet modals
│   ├── BeanManagementPage.cs          # [MODIFY] Integrate with bottom sheet modals
│   └── UserProfileManagementPage.cs   # [MODIFY] Integrate with bottom sheet modals
├── Components/
│   ├── Modals/                        # [NEW] Bottom sheet infrastructure
│   │   ├── BottomSheet.cs             # [NEW] Scaffold wrapper for Plugin.Maui.BottomSheet
│   │   ├── BottomSheetExtensions.cs   # [NEW] OpenBottomSheet extension method
│   │   └── ConfirmDeleteComponent.cs  # [NEW] Delete confirmation component
│   ├── Forms/                         # [NEW] Form components directory
│   │   ├── EquipmentFormComponent.cs  # [NEW] Equipment form (pure MauiReactor)
│   │   ├── BeanFormComponent.cs       # [NEW] Bean form (pure MauiReactor)
│   │   └── UserProfileFormComponent.cs # [NEW] Profile form (pure MauiReactor)
│   ├── RatingControl.cs
│   └── ShotRecordCard.cs
└── Infrastructure/
    └── MauiPreferencesStore.cs

BaristaNotes.Core/                     # Core business logic project
├── Services/
│   ├── IEquipmentService.cs           # [EXISTS] Full CRUD interface
│   ├── IBeanService.cs                # [EXISTS] Full CRUD interface  
│   ├── IUserProfileService.cs         # [EXISTS] Full CRUD interface
│   └── DTOs/
│       └── DataTransferObjects.cs     # [EXISTS] All DTOs defined
└── Data/
    └── Repositories/                  # [EXISTS] Repository implementations

BaristaNotes.Tests/                    # Test project
├── Unit/
│   ├── Services/
│   └── ViewModels/                    # [NEW] Tests for page state/logic
└── Integration/
    └── ModalCrudTests.cs              # [NEW] Integration tests for CRUD flows
```

**Structure Decision**: Extending existing mobile project structure. New modal components organized under `Components/Modals/`. No new projects required - existing Core services provide all CRUD operations.

## Complexity Tracking

> No violations identified - design aligns with all constitutional principles.

---

## Constitution Check (Post-Design Review)

*Re-evaluated after Phase 1 design completion.*

All features demonstrate alignment with constitutional principles:

- [x] **Code Quality Standards**: 
  - ✅ Reusable `BottomSheet` scaffold wrapper and extension methods (DRY)
  - ✅ Form components follow consistent MVU pattern (single responsibility)
  - ✅ Existing services unchanged - clean separation of concerns
  - ✅ Clear component hierarchy: Pages → BottomSheet → Forms → Services

- [x] **Test-First Development**: 
  - ✅ Acceptance scenarios defined in spec for all user stories
  - ✅ Test files identified in project structure
  - ✅ 80% coverage target documented
  - ✅ Integration test approach defined for CRUD flows

- [x] **User Experience Consistency**: 
  - ✅ Plugin.Maui.BottomSheet provides native bottom sheet behavior
  - ✅ Pure MauiReactor form components with MVU state management
  - ✅ Confirmation dialogs for all destructive actions
  - ✅ Form validation with inline error messages
  - ✅ Loading states documented in component contracts

- [x] **Performance Requirements**: 
  - ✅ Performance targets defined: <300ms modals, <500ms lists, <1s saves
  - ✅ Async patterns with visual feedback
  - ✅ CollectionView for optimized list rendering
  - ✅ No new database migrations required

**Final Assessment**: PASS - Ready for task breakdown and implementation.

---

## Generated Artifacts

| Artifact | Path | Status |
|----------|------|--------|
| Implementation Plan | [plan.md](plan.md) | ✅ Complete |
| Research | [research.md](research.md) | ✅ Complete |
| Data Model | [data-model.md](data-model.md) | ✅ Complete |
| Component Contracts | [contracts/component-contracts.md](contracts/component-contracts.md) | ✅ Complete |
| Quickstart Guide | [quickstart.md](quickstart.md) | ✅ Complete |
| Task Breakdown | tasks.md | ⏳ Next: Run `/speckit.tasks` |
