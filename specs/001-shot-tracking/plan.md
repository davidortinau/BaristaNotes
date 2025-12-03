# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]
**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

[Extract from feature spec: primary requirement + technical approach from research]

## Technical Context

**Language/Version**: C# / .NET 10.0  
**Primary Dependencies**: .NET MAUI 10.0.11, MauiReactor 4.0.3-beta, Entity Framework Core 10.0.0, UXDivers.Popups 0.9.0  
**Storage**: SQLite via EF Core 10.0.0  
**Testing**: xUnit (BaristaNotes.Tests project)  
**Target Platform**: iOS 15+, Android 21+, Windows 10+, macOS Catalyst 15+  
**Project Type**: Mobile cross-platform application  
**Performance Goals**: Page load <2s (p95), API responses <500ms (p95), user interactions <100ms perceived response  
**Constraints**: Mobile memory <200MB, offline-capable, battery-efficient background operations  
**Scale/Scope**: Single-user local-first app, ~20 screens, expected user count <50 per installation

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

All features must demonstrate alignment with constitutional principles:

- [x] **Code Quality Standards**: Design enables single-responsibility components (PreferencesService for defaults, validators for business rules). No complexity violations. Standard MauiReactor patterns throughout.
- [x] **Test-First Development**: Test scenarios defined in spec.md acceptance criteria, approved by stakeholder. Coverage target: 90% for new code (preferences + validation are critical paths). Tests written before implementation per TDD cycle.
- [x] **User Experience Consistency**: Uses existing design system (Picker components, Entry styling consistent with ShotLoggingPage). WCAG 2.1 AA compliance via semantic properties, 44x44px touch targets, keyboard navigation. UXDivers.Popups for user feedback (NON-NEGOTIABLE).
- [x] **Performance Requirements**: Targets defined - Preferences API <10ms, user query <50ms for 50 users, form pre-population <20ms total. No performance regressions anticipated. Instrumentation via existing logging.

**Violations requiring justification**: None. All constitutional principles met without exceptions.

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
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```text
# [REMOVE IF UNUSED] Option 1: Single project (DEFAULT)
src/
├── models/
├── services/
├── cli/
└── lib/

tests/
├── contract/
├── integration/
└── unit/

# [REMOVE IF UNUSED] Option 2: Web application (when "frontend" + "backend" detected)
backend/
├── src/
│   ├── models/
│   ├── services/
│   └── api/
└── tests/

frontend/
├── src/
│   ├── components/
│   ├── pages/
│   └── services/
└── tests/

# [REMOVE IF UNUSED] Option 3: Mobile + API (when "iOS/Android" detected)
api/
└── [same as backend above]

ios/ or android/
└── [platform-specific structure: feature modules, UI flows, platform tests]
```

**Structure Decision**: [Document the selected structure and reference the real
directories captured above]

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
