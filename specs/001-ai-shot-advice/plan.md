# Implementation Plan: AI Shot Improvement Advice

**Branch**: `001-ai-shot-advice` | **Date**: 2025-12-09 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-ai-shot-advice/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Enable AI-powered shot improvement advice for home baristas using Microsoft.Extensions.AI with OpenAI (gpt-5-nano model). Users can explicitly request detailed advice from the shot detail page, and optionally receive passive insights after logging shots that deviate from their successful history. The AI will analyze shot parameters, rating history, and optional tasting notes to provide espresso-specific guidance.

## Technical Context

**Language/Version**: C# 12 / .NET 10.0  
**Primary Dependencies**: MauiReactor 4.0.3-beta, Microsoft.Extensions.AI, Microsoft.Extensions.AI.OpenAI, OpenAI SDK, Entity Framework Core 8.0  
**Storage**: SQLite via EF Core (existing) - adding TastingNotes field to ShotRecord  
**Testing**: xUnit with FluentAssertions (existing pattern)  
**Target Platform**: iOS 15+, Android 12+, macOS (via Mac Catalyst)  
**Project Type**: Mobile - .NET MAUI with MauiReactor  
**Performance Goals**: AI response <10 seconds, loading feedback within 100ms  
**Constraints**: API key is app-provided via IConfiguration (not user-configured), offline graceful degradation  
**Scale/Scope**: Single user, local data, external OpenAI API calls

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

All features must demonstrate alignment with constitutional principles:

- [x] **Code Quality Standards**: Service-based architecture with clear separation (IAIAdviceService). Single responsibility maintained. MauiReactor patterns followed.
- [x] **Test-First Development**: Test scenarios defined for AI service (mocked), prompt building, and UI integration. Coverage targets: 80% for business logic, 100% for prompt building.
- [x] **User Experience Consistency**: Uses existing IFeedbackService for loading/error states. MaterialSymbolsFont for icons. Follows established page patterns.
- [x] **Performance Requirements**: Loading indicator within 100ms. AI timeout at 10s. Non-blocking async pattern. UI remains responsive during API calls.

**Violations requiring justification**: None identified. Design follows existing patterns.

## Project Structure

### Documentation (this feature)

```text
specs/001-ai-shot-advice/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
# Mobile application structure (existing)
BaristaNotes/
├── Services/
│   ├── IAIAdviceService.cs          # NEW: AI advice service interface
│   ├── AIAdviceService.cs           # NEW: AI advice implementation
│   └── IFeedbackService.cs          # EXISTING: Used for loading states
├── Pages/
│   └── ShotDetailPage.cs            # NEW: Shot detail with AI advice button
├── Components/
│   └── AIAdviceDisplay.cs           # NEW: Component to display AI advice
└── MauiProgram.cs                   # MODIFY: Register AI services

BaristaNotes.Core/
├── Models/
│   └── ShotRecord.cs                # MODIFY: Add TastingNotes field
├── Services/
│   ├── DTOs/
│   │   ├── AIAdviceRequestDto.cs    # NEW: Request DTO for AI
│   │   └── AIAdviceResponseDto.cs   # NEW: Response DTO from AI
│   └── IShotService.cs              # MODIFY: Add GetShotsForAIContext

BaristaNotes.Tests/
├── Unit/
│   ├── AIAdviceServiceTests.cs      # NEW: AI service unit tests
│   └── AIPromptBuilderTests.cs      # NEW: Prompt building tests
└── Mocks/
    └── MockChatClient.cs            # NEW: Mock IChatClient for tests
```

**Structure Decision**: Follows existing mobile app structure. AI service in presentation layer (BaristaNotes) because it depends on Microsoft.Extensions.AI which is presentation-layer infrastructure. DTOs in Core layer for clean separation.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | N/A |
