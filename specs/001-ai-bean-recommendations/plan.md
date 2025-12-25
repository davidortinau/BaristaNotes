# Implementation Plan: AI Bean Recommendations

**Branch**: `001-ai-bean-recommendations` | **Date**: 2024-12-24 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-ai-bean-recommendations/spec.md`

## Summary

Extend the existing AI advice system to automatically recommend espresso extraction parameters (dose, grind, output, duration) when users select beans in the shot logging page. Two scenarios: (1) new beans with no history use AI to generate recommendations from bean characteristics, (2) returning beans use AI to analyze historical shots and suggest optimal parameters. Both display the existing animated loading bar and show context-appropriate toast messages with recommended values.

## Technical Context

**Language/Version**: C# 12 / .NET 10.0  
**Primary Dependencies**: MauiReactor 4.0.3-beta, Microsoft.Extensions.AI, Microsoft.Extensions.AI.OpenAI, OpenAI SDK, Entity Framework Core 8.0, UXDivers.Popups.Maui  
**Storage**: SQLite via EF Core (existing ShotRecord, Bean, Bag entities)  
**Testing**: xUnit with FluentAssertions (existing test infrastructure)  
**Target Platform**: iOS 15+, Android (via .NET MAUI)
**Project Type**: Mobile application  
**Performance Goals**: AI recommendations complete within 10 seconds (95th percentile)  
**Constraints**: Reuse existing AIAdviceService fallback pattern (local Apple Intelligence → OpenAI), existing loading bar animation, existing FeedbackService toast patterns  
**Scale/Scope**: Single user, local database, extends existing AI advice feature

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

All features must demonstrate alignment with constitutional principles:

- [x] **Code Quality Standards**: Design extends existing AIAdviceService with single-responsibility methods. New recommendation logic isolated in dedicated service method. No new abstractions required.
- [x] **Test-First Development**: Test scenarios defined in spec acceptance criteria. Unit tests for recommendation logic, integration tests for toast message format. 80% coverage target.
- [x] **User Experience Consistency**: Reuses existing animated loading bar (RenderAnimatedLoadingBar), existing FeedbackService.ShowInfoAsync() for toasts, no new UI patterns. Accessibility maintained via existing screen reader announcements.
- [x] **Performance Requirements**: 10-second timeout matches existing AI advice timeout. Loading bar provides immediate visual feedback (<100ms). Toast appears within 200ms of response.

**Violations requiring justification**: None - feature fully aligns with constitution.

## Project Structure

### Documentation (this feature)

```text
specs/001-ai-bean-recommendations/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (internal service contracts)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
BaristaNotes/
├── Pages/
│   └── ShotLoggingPage.cs          # Modify: Add bean selection handler for AI recommendations
├── Services/
│   ├── IAIAdviceService.cs         # Extend: Add GetRecommendationsForBeanAsync method
│   ├── AIAdviceService.cs          # Extend: Implement bean recommendation logic
│   └── IFeedbackService.cs         # Existing: Use ShowInfoAsync for toasts
└── DTOs/
    └── AIRecommendationDto.cs      # New: Response DTO for recommendations

BaristaNotes.Core/
├── Models/
│   ├── Bean.cs                     # Existing: Source for bean characteristics
│   ├── Bag.cs                      # Existing: Links beans to shots
│   └── ShotRecord.cs               # Existing: Historical shot data
└── Services/
    └── IShotService.cs             # Extend: Add GetBeanHistoryForAIAsync method

BaristaNotes.Tests/
└── Services/
    └── AIAdviceServiceTests.cs     # Extend: Add recommendation tests
```

**Structure Decision**: Extends existing mobile app structure. No new projects or layers required. Changes isolated to AIAdviceService extension and ShotLoggingPage bean picker handler.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| *None* | N/A | N/A |
