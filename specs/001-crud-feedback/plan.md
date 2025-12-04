# Implementation Plan: CRUD Operation Visual Feedback

**Branch**: `001-crud-feedback` | **Date**: 2025-12-02 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-crud-feedback/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Implement visual user feedback for all CRUD operations (create, read, update, delete) across the BaristaNotes application using toast notifications. The solution must provide immediate confirmation of success, clear error messaging with recovery guidance, and loading indicators for in-progress operations. Technical approach uses UXDivers.Popups.Maui package scaffolded for Maui Reactor's MVU architecture, with a centralized FeedbackService managing all notifications in a theme-aware, accessible manner.

## Technical Context

**Language/Version**: C# 12, .NET 8.0  
**Primary Dependencies**: Maui Reactor (preview), UXDivers.Popups.Maui, Microsoft.Maui.Controls, CommunityToolkit.Maui  
**Storage**: SQLite with Entity Framework Core (existing)  
**Testing**: xUnit, FluentAssertions, Moq (existing test infrastructure)  
**Target Platform**: iOS, Android (cross-platform .NET MAUI)
**Project Type**: Mobile application with shared business logic  
**Performance Goals**: Toast notifications appear within 100ms, animations at 60fps minimum  
**Constraints**: Mobile memory <200MB, battery efficient, offline-capable with local feedback  
**Scale/Scope**: 4 CRUD entity types (Beans, Equipment, Profiles, Shots), ~15 CRUD operations total

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

All features must demonstrate alignment with constitutional principles:

- [x] **Code Quality Standards**: YES - FeedbackService provides centralized, single-responsibility toast management. Reactor component scaffolding follows established patterns. No duplication across CRUD operations.
- [x] **Test-First Development**: YES - User Story acceptance scenarios defined in spec. Test scenarios cover success/error/loading states for all CRUD operations. Will achieve 80% coverage minimum before implementation.
- [x] **User Experience Consistency**: YES - All toasts use consistent positioning (bottom center), theme-aware styling (coffee colors), accessibility (icons + text + ARIA), and work in light/dark modes. WCAG 2.1 AA contrast requirements addressed.
- [x] **Performance Requirements**: YES - Toast appearance <100ms (NFR-P1), loading states <100ms (NFR-P2), 60fps animations (NFR-P3). Non-blocking UI (NFR-P4). Performance easily measurable with UI test automation.

**Violations requiring justification**: None - all constitutional principles can be met within this feature scope.

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

```text
BaristaNotes/                              # Main MAUI application project
├── Pages/                                  # Reactor page components (MVU)
│   ├── HomePage.cs
│   ├── BeansPage.cs
│   ├── EquipmentPage.cs
│   └── ProfilesPage.cs
├── Components/                             # Reactor UI components
│   ├── Integration/                        # Scaffolded 3rd-party controls
│   │   └── RxPopups.cs                    # Reactor wrapper for UXDivers.Popups.Maui
│   └── Feedback/
│       ├── ToastNotification.cs           # Toast UI component (if custom styling needed)
│       └── LoadingIndicator.cs            # Loading state component
├── Services/                               # Business logic services
│   ├── FeedbackService.cs                 # Central toast notification service
│   ├── BeanService.cs                     # Existing - update with feedback calls
│   ├── EquipmentService.cs                # Existing - update with feedback calls
│   ├── ProfileService.cs                  # Existing - update with feedback calls
│   └── ShotService.cs                     # Existing - update with feedback calls
├── Models/                                 # Data models
│   ├── FeedbackMessage.cs                 # Toast notification data model
│   └── OperationResult.cs                 # CRUD operation result wrapper
├── Resources/                              # Theme resources
│   └── Styles/
│       └── AppTheme.cs                    # Theme colors for feedback (coffee palette)
└── MauiProgram.cs                         # DI registration

BaristaNotes.Core/                         # Shared business logic library
├── Entities/                               # EF Core entities (existing)
│   ├── Bean.cs
│   ├── Equipment.cs
│   ├── Profile.cs
│   └── ShotNote.cs
└── Data/
    └── AppDbContext.cs                    # EF Core context (existing)

BaristaNotes.Tests/                        # Test project
├── Services/
│   ├── FeedbackServiceTests.cs            # Unit tests for FeedbackService
│   ├── BeanServiceTests.cs                # Update with feedback verification
│   ├── EquipmentServiceTests.cs           # Update with feedback verification
│   ├── ProfileServiceTests.cs             # Update with feedback verification
│   └── ShotServiceTests.cs                # Update with feedback verification
└── Integration/
    └── FeedbackIntegrationTests.cs        # End-to-end feedback flow tests
```

**Structure Decision**: Mobile application with shared Core library for testable business logic. BaristaNotes project contains Reactor UI components and services. BaristaNotes.Core contains EF Core entities and DbContext (already established). Tests reference Core library to avoid MAUI test framework conflicts.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
