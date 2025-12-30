# Implementation Plan: Shot History Filter

**Branch**: `001-shot-history-filter` | **Date**: 2025-12-30 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-shot-history-filter/spec.md`

## Summary

Add filtering capability to the shot history (ActivityFeedPage) allowing users to filter by Bean, Made For (person), and Rating. Users access filters via a ToolbarItem that opens a UXDivers popup where they can select multiple filter criteria (AND logic). The filtered results display in the same view with visual indicators showing active filters and result count.

## Technical Context

**Language/Version**: C# 12 / .NET 10.0  
**Primary Dependencies**: MauiReactor 4.0.9-beta, UXDivers.Popups.Maui 0.9.1, Entity Framework Core 8.0  
**Storage**: SQLite via EF Core (existing ShotRecord, Bean, UserProfile entities)  
**Testing**: xUnit with Moq (existing test infrastructure)  
**Target Platform**: iOS, Android, macOS, Windows (via .NET MAUI)  
**Project Type**: Mobile app with shared Core library  
**Performance Goals**: Filter popup opens <300ms, list updates <500ms, handle 1000+ shots  
**Constraints**: Session-only filter state (no persistence), offline-capable (local DB)  
**Scale/Scope**: Single feature affecting ActivityFeedPage, new popup component, repository extensions

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

All features must demonstrate alignment with constitutional principles:

- [x] **Code Quality Standards**: Single-responsibility design with separate FilterPopup component, FilterCriteria model, and repository filter methods. ThemeKey system for all styling.
- [x] **Test-First Development**: Test scenarios defined in spec acceptance criteria. Unit tests for filter logic, integration tests for repository queries. 80% coverage target.
- [x] **User Experience Consistency**: Uses UXDivers popup (existing pattern), MaterialSymbolsFont icons (no emojis), 44x44px touch targets, VoiceOver/TalkBack accessible.
- [x] **Performance Requirements**: Filter popup <300ms, list update <500ms per spec NFR-P1/P2. Instrumentation via ILogger.

**Violations requiring justification**: None identified.

## Project Structure

### Documentation (this feature)

\`\`\`text
specs/001-shot-history-filter/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (internal contracts only)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
\`\`\`

### Source Code (repository root)

\`\`\`text
BaristaNotes/
├── Pages/
│   └── ActivityFeedPage.cs          # Modify: add filter toolbar, state, integration
├── Integrations/Popups/
│   └── ShotFilterPopup.cs           # NEW: filter selection popup
├── Models/
│   └── ShotFilterCriteria.cs        # NEW: filter state model
└── Services/
    └── IShotService.cs              # Modify: add filtered query method

BaristaNotes.Core/
├── Data/Repositories/
│   ├── IShotRecordRepository.cs     # Modify: add combined filter method
│   └── ShotRecordRepository.cs      # Modify: implement combined filter query
└── DTOs/
    └── ShotFilterCriteriaDto.cs     # NEW: filter criteria DTO

BaristaNotes.Tests/
├── Unit/
│   └── ShotFilterTests.cs           # NEW: filter logic unit tests
└── Integration/
    └── ShotRecordRepositoryFilterTests.cs  # NEW: repository filter tests
\`\`\`

**Structure Decision**: Follows existing .NET MAUI + Core library pattern. New popup follows established UXDivers integration in `/Integrations/Popups/`. Filter criteria as separate model for testability.

## Complexity Tracking

> No constitutional violations identified.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | N/A |
