# Implementation Plan: Inline Bean Creation During Shot Logging

**Branch**: `001-inline-bean-creation` | **Date**: 2025-12-09 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-inline-bean-creation/spec.md`

## Summary

Enable new users to create beans and bags inline during shot logging via UXDivers.Popups.Maui modal forms. When no beans or bags exist, users see an empty state prompting them to create a bean first, then a bag, before logging their shot. This unblocks the "cold start" problem where users cannot log shots without pre-configured data.

## Technical Context

**Language/Version**: C# 12 / .NET 10.0  
**Primary Dependencies**: MauiReactor 4.0.3-beta, UXDivers.Popups.Maui 0.9.0, Entity Framework Core 8.0.0  
**Storage**: SQLite via EF Core (existing Bean, Bag, ShotRecord entities)  
**Testing**: xUnit + FluentAssertions  
**Target Platform**: iOS 17+, Android 14+, macOS Catalyst  
**Project Type**: Mobile application  
**Performance Goals**: Modal forms appear <100ms, save operations <500ms  
**Constraints**: Must use UXDivers.Popups.Maui FormPopup, no full-page navigation during creation flow  
**Scale/Scope**: Single user per device, local-first storage

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

All features must demonstrate alignment with constitutional principles:

- [x] **Code Quality Standards**: Feature uses existing service patterns (IBeanService, IBagService), single-responsibility components (separate popup components for bean/bag creation), and follows established MauiReactor patterns.
- [x] **Test-First Development**: Test scenarios defined for bean creation, bag creation, and flow chaining. Will write unit tests for service calls and integration tests for popup flow before implementation.
- [x] **User Experience Consistency**: Uses existing UXDivers.Popups patterns (SimpleActionPopup, ListActionPopup) as reference. Will use FormPopup for inline creation. Touch targets will be 44x44px minimum. Error messages shown inline in popup forms.
- [x] **Performance Requirements**: Modal popup appearance <100ms (UXDivers handles animations). Bean/Bag creation uses existing EF Core services which meet <500ms requirement. Loading states shown during save operations.
- [x] **Technology Stack Consistency**: MauiReactor for UI, UXDivers.Popups for modals, IBeanService/IBagService for data operations. No new libraries needed.

**Violations requiring justification**: None - all principles can be met.

## Project Structure

### Documentation (this feature)

```text
specs/001-inline-bean-creation/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (N/A - uses existing entities)
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (N/A - no new APIs)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
BaristaNotes/
├── Pages/
│   └── ShotLoggingPage.cs         # MODIFY: Add empty state detection, modal triggers
├── Integrations/
│   └── Popups/
│       ├── BeanCreationPopup.cs   # NEW: UXDivers FormPopup for bean creation
│       └── BagCreationPopup.cs    # NEW: UXDivers FormPopup for bag creation

BaristaNotes.Core/
├── Services/
│   ├── IBeanService.cs            # EXISTING: CreateBeanAsync already available
│   └── IBagService.cs             # EXISTING: CreateBagAsync already available
└── Models/
    ├── Bean.cs                    # EXISTING: No changes needed
    └── Bag.cs                     # EXISTING: No changes needed

BaristaNotes.Tests/
└── Integration/
    └── InlineBeanCreationTests.cs # NEW: Flow integration tests
```

**Structure Decision**: Single project structure - extends existing BaristaNotes MAUI app with new popup components. No new projects required.

## Complexity Tracking

> No constitution violations identified. Feature uses established patterns.

## Post-Design Constitution Re-Check

*GATE: Verified after Phase 1 design completion.*

- [x] **Code Quality Standards**: Design uses single-responsibility popups (BeanCreationPopup, BagCreationPopup), existing service layer, and follows MauiReactor state patterns. No complexity violations.
- [x] **Test-First Development**: Test scenarios documented in quickstart.md. Integration tests planned for flow chaining. Existing service validation coverage maintained.
- [x] **User Experience Consistency**: UXDivers FormPopup provides consistent modal styling. Empty state uses existing ThemeKeys patterns. Error/success feedback handled inline in popups.
- [x] **Performance Requirements**: UXDivers animations handle <100ms appearance. EF Core service calls verified <500ms in existing usage.
- [x] **Technology Stack Consistency**: No new libraries. Uses MauiReactor, UXDivers.Popups, EF Core per architecture constraints.
- [x] **Rating Standards**: N/A - No rating UI in this feature.

**Status**: ✅ All constitution checks pass. Ready for `/speckit.tasks`.

