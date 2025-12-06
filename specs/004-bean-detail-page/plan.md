# Implementation Plan: Bean Detail Page

**Branch**: `004-bean-detail-page` | **Date**: December 5, 2025 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/004-bean-detail-page/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Replace the current bottom sheet form for beans with a dedicated full-page `BeanDetailPage`. The page provides all existing create/edit/delete functionality plus displays paginated shot history (most recent first) when viewing existing beans. Uses existing `IBeanService` and `IShotService.GetShotHistoryByBeanAsync()` without modification.

## Technical Context

**Language/Version**: C# 12, .NET 10.0  
**Primary Dependencies**: MauiReactor (UI), UXDivers.Popups.Maui (feedback), Microsoft.EntityFrameworkCore (data)  
**Storage**: SQLite via EF Core (existing infrastructure)  
**Testing**: xUnit + FluentAssertions  
**Target Platform**: iOS Simulator (arm64), Android (future)
**Project Type**: Mobile (.NET MAUI)  
**Performance Goals**: Page load <2s, shot history first page <500ms, save feedback <100ms  
**Constraints**: Must use existing services without modification, MauiReactor component patterns, Shell navigation  
**Scale/Scope**: Single new page, modify one existing page (BeanManagementPage)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

All features must demonstrate alignment with constitutional principles:

- [x] **Code Quality Standards**: Design follows existing patterns (ProfileFormPage as template), single-responsibility (page handles form + shot history display), DRY (reuses ShotRecordCard component, existing services)
- [x] **Test-First Development**: Test scenarios defined in spec acceptance criteria. Unit tests for page state logic, integration tests for navigation flow. Coverage target: 80% for new code.
- [x] **User Experience Consistency**: Uses existing theme keys (ThemeKeys.Card, ThemeKeys.SubHeadline), form patterns match ProfileFormPage, error display matches existing inline pattern, touch targets follow 44x44px minimum.
- [x] **Performance Requirements**: Pagination prevents loading all shots at once (use PagedResult from IShotService), loading states shown during save/load operations. Performance budget: <2s total page load.

**Violations requiring justification**: None. Feature can be implemented using existing patterns and services.

## Project Structure

### Documentation (this feature)

```text
specs/004-bean-detail-page/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
BaristaNotes/
├── Pages/
│   ├── BeanDetailPage.cs          # NEW: Full-page bean form with shot history
│   └── BeanManagementPage.cs      # MODIFY: Navigate to BeanDetailPage instead of showing bottom sheet
├── Components/
│   └── ShotRecordCard.cs          # EXISTING: Reused for shot history display
└── MauiProgram.cs                 # MODIFY: Register "bean-detail" route

BaristaNotes.Core/
├── Services/
│   ├── IBeanService.cs            # EXISTING: No changes
│   ├── BeanService.cs             # EXISTING: No changes
│   ├── IShotService.cs            # EXISTING: Use GetShotHistoryByBeanAsync
│   └── ShotService.cs             # EXISTING: No changes
└── Services/DTOs/
    └── DataTransferObjects.cs     # EXISTING: BeanDto, ShotRecordDto, PagedResult<T>

BaristaNotes.Tests/
├── Unit/
│   └── Pages/
│       └── BeanDetailPageTests.cs # NEW: State management, validation logic tests
└── Integration/
    └── BeanDetailNavigationTests.cs # NEW: Navigation flow tests
```

**Structure Decision**: Mobile single-project structure matching existing codebase. New page follows pattern established by ProfileFormPage. No new abstractions or patterns introduced.

## Complexity Tracking

> No violations - all features can be implemented using existing patterns.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| *None* | - | - |
