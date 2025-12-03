# Implementation Plan: Edit and Delete Shots from Activity Page

**Branch**: `001-edit-delete-shots` | **Date**: 2025-12-03 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-edit-delete-shots/spec.md`

**Note**: This plan follows the MVU (Model-View-Update) architecture using Maui Reactor.

## Summary

This feature adds edit and delete capabilities for shot records from the activity feed page. Users can:
1. Delete shots with confirmation dialog using UXDivers.Popups.Maui
2. Edit shot details (actual time, output weight, rating, drink type) while preserving creation metadata
3. Access both actions via intuitive UI patterns (swipe gestures or context menu)

**Technical Approach**: Extend existing `IShotService` with update method, add delete confirmation popup using UXDivers scaffolded components, implement MVU state management for edit form, and integrate visual feedback via existing `IFeedbackService`.

## Technical Context

**Language/Version**: C# .NET 10 (MAUI)
**Primary Dependencies**: 
  - Reactor.Maui 4.0.3-beta (MVU pattern, fluent UI syntax)
  - Microsoft.EntityFrameworkCore.Sqlite 8.0.0
  - UXDivers.Popups.Maui 0.9.0 (confirmation dialogs)
  - CommunityToolkit.Maui 9.1.1
  - CoreSync.Sqlite 0.1.122 (future sync capability)
**Storage**: SQLite with Entity Framework Core
**Testing**: xUnit (BaristaNotes.Tests project)
**Target Platform**: iOS/Android via .NET MAUI 10.0.10
**Project Type**: Mobile (cross-platform)
**Architecture**: MVU (Model-View-Update) via Maui Reactor - NO ViewModels, state managed in components
**Performance Goals**: 
  - Delete operation: <1s including UI feedback
  - Edit form load: <500ms
  - Save operation: <2s including validation
  - List refresh: <1s after operations
**Constraints**: 
  - Accessible touch targets (44x44px minimum)
  - Dark theme default with light theme support
  - Coffee-inspired color palette
  - 1950s modern design aesthetic
  - Thin visual tree (native controls preferred)
**Scale/Scope**: Single-user mobile app, ~10 screens, offline-first with future sync

## Constitution Check (Post-Design)

*Re-evaluated after Phase 1 design completion*

All features must demonstrate alignment with constitutional principles:

- [x] **Code Quality Standards**: ✅ PASSED
  - Design enables readable, maintainable code with clear separation of concerns
  - Single-responsibility maintained: EditShotPage handles edit UI, ShotService handles business logic, UpdateShotDto encapsulates data
  - No excessive complexity - extends existing patterns (service methods, MVU components)
  - Code will be peer-reviewed before merge
  - Static analysis via existing .NET tooling (no suppressions expected)

- [x] **Test-First Development**: ✅ PASSED
  - Test scenarios defined in spec (3 user stories with acceptance criteria)
  - Comprehensive test checklist in quickstart.md covers unit, integration, and UI tests
  - Tests will be written BEFORE implementation (Red-Green-Refactor)
  - Coverage targets: 100% for UpdateShotAsync/DeleteShotAsync service methods
  - Integration tests for database operations
  - UI tests for user flows (edit save, delete confirmation)
  - Product owner approval implicit (user requested feature)

- [x] **User Experience Consistency**: ✅ PASSED
  - Follows existing app patterns (SwipeView, UXDivers popups, MVU state management)
  - Reuses existing components (FeedbackService for toasts, theme styles)
  - Accessibility: SwipeView is screen-reader compatible, 44x44px touch targets enforced
  - Keyboard navigation for confirmation dialogs via UXDivers
  - Responsive: Works on all mobile form factors (iOS/Android)
  - Loading states: IsSaving flag shows during save operations
  - Error handling: User-friendly validation messages, no technical details exposed
  - Consistent patterns: Edit form mirrors create shot form styling

- [x] **Performance Requirements**: ✅ PASSED
  - Specific targets defined and measurable:
    * Delete: <1s (service call + UI feedback)
    * Edit load: <500ms (GetShotByIdAsync + form render)
    * Save: <2s (validation + UpdateShotAsync + toast)
    * List refresh: <1s (reload + render filtered shots)
  - Monitoring: Use Stopwatch for timing measurements in tests
  - No new heavy dependencies (extends existing EF Core, Reactor, UXDivers)
  - Database queries optimized (existing indexes, soft delete filtering)
  - Resource efficient: No background processing, minimal memory footprint

**Violations requiring justification**: None - all principles satisfied

**Changes from Initial Check**: None - design confirmed initial assessment. All constitutional requirements met.

## Project Structure

### Documentation (this feature)

```text
specs/001-edit-delete-shots/
├── plan.md              # This file
├── research.md          # Phase 0 output (MVU patterns, UXDivers integration)
├── data-model.md        # Phase 1 output (ShotRecord updates, DTOs)
├── quickstart.md        # Phase 1 output (developer guide)
├── contracts/           # Phase 1 output (service interface updates)
└── tasks.md             # Phase 2 output (created separately)
```

### Source Code (repository root)

```text
BaristaNotes/                    # Main MAUI app
├── Pages/
│   ├── ActivityFeedPage.cs     # Add edit/delete UI actions
│   └── EditShotPage.cs         # NEW: Edit form component (MVU)
├── Components/
│   └── ShotCard.cs             # Add swipe/context menu actions
├── Integrations/
│   └── UXDivers.Popups/        # UXDivers scaffolding (if needed)
├── Services/
│   └── IFeedbackService.cs     # Existing toast notifications
├── Theme/
│   └── AppTheme.cs             # Existing styles (no new styles needed)

BaristaNotes.Core/               # Shared business logic
├── Models/
│   └── ShotRecord.cs           # Existing model (no changes needed)
├── Services/
│   ├── IShotService.cs         # EXTEND: Add UpdateShotAsync signature
│   ├── ShotService.cs          # IMPLEMENT: UpdateShotAsync, DeleteShotAsync
│   └── DTOs/
│       └── UpdateShotDto.cs    # NEW: DTO for edit operations
├── Data/
│   └── Repositories/
│       └── IShotRecordRepository.cs # Existing (update/delete already present)

BaristaNotes.Tests/              # Test project
├── Services/
│   └── ShotServiceTests.cs     # Add update/delete tests
└── Integration/
    └── ShotDatabaseTests.cs    # Add update/delete integration tests
```

**Structure Decision**: Mobile application with separation between UI (BaristaNotes MAUI app using Reactor MVU) and business logic (BaristaNotes.Core class library). Tests in separate xUnit project. This structure already exists and is working - we're extending it.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |

## Phase 0-1 Summary: Planning Complete

**Status**: ✅ All phases complete through Phase 1 (Design & Contracts)

### Artifacts Generated

1. **research.md**: MVU patterns, UXDivers integration, swipe actions, validation, soft delete strategy
2. **data-model.md**: Entity analysis, UpdateShotDto definition, state models, validation rules
3. **contracts/IShotService.md**: Service contract extensions, method signatures, usage examples
4. **quickstart.md**: Step-by-step implementation guide with code examples and testing checklist

### Key Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Edit Form Pattern | Local MVU state with Message enum | Temporary state doesn't need global app state |
| Delete Confirmation | UXDivers RxPopupPage | Styled, consistent with app theme, already integrated |
| UI Access Pattern | SwipeView with platform conventions | Native, accessible, intuitive |
| Validation Strategy | Client-side with service enforcement | Immediate feedback, prevents bad data |
| Delete Implementation | Soft delete (IsDeleted flag) | Sync compatibility, preserves history |

### Technical Stack (Confirmed)

- **Framework**: .NET MAUI 10 with Maui Reactor 4.0.3-beta (MVU)
- **Database**: SQLite + Entity Framework Core 8.0.0
- **UI Components**: UXDivers.Popups.Maui 0.9.0, CommunityToolkit.Maui 9.1.1
- **Testing**: xUnit in BaristaNotes.Tests project
- **Architecture**: MVU (Model-View-Update) - no ViewModels

### Dependencies Map

```
EditShotPage (UI Component)
    ├─ IShotService (business logic)
    │   ├─ IShotRecordRepository (data access)
    │   └─ IPreferencesService (settings)
    ├─ IFeedbackService (toast notifications)
    └─ Navigation (Maui Reactor built-in)

ActivityFeedPage (existing)
    ├─ SwipeView (MAUI built-in) - NEW
    ├─ RxPopupPage (UXDivers) - NEW
    └─ IShotService.DeleteShotAsync - NEW
```

### Performance Budget

| Operation | Target | Measurement |
|-----------|--------|-------------|
| Edit Form Load | <500ms | GetShotByIdAsync + render |
| Save Changes | <2s | Validation + UpdateShotAsync + feedback |
| Delete Shot | <1s | DeleteShotAsync + feedback |
| List Refresh | <1s | Query + filter + render |

### Test Coverage Plan

- **Unit Tests**: 12 tests (UpdateShotAsync variations, DeleteShotAsync, validation)
- **Integration Tests**: 6 tests (database persistence, soft delete filtering)
- **UI Tests**: 6 tests (edit flow, delete flow, validation errors)
- **Total**: 24 tests covering all user stories and edge cases

### Next Steps (Phase 2)

Run `/speckit.tasks` command to generate actionable task breakdown with:
- Concrete file paths and line numbers
- Test-first implementation order
- Verification checkpoints
- Estimated completion times

---

**Plan Status**: COMPLETE ✅  
**Ready for**: Task generation (`/speckit.tasks`) and implementation  
**Estimated Implementation Time**: 4-6 hours (per quickstart.md)  
**Branch**: `001-edit-delete-shots`  
**Generated**: 2025-12-03

