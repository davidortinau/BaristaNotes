# Implementation Plan: Update Documentation for AI Features and Configuration

**Branch**: `002-update-documentation` | **Date**: 2025-12-10 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `/specs/002-update-documentation/spec.md`

**Note**: This is a documentation-only feature. No code implementation required - only updates to README.md and docs/CONTRIBUTING.md to reflect the current state of the codebase after AI feature implementation.

## Summary

Update project documentation (README.md and docs/CONTRIBUTING.md) to reflect recently implemented AI features using Microsoft.Extensions.AI, document Shiny-based platform-aware configuration management, explain secret management patterns for local development vs. production, and provide educational explanations of architectural decisions. This will enable new developers to quickly understand the project, successfully configure their environment, and learn modern .NET MAUI patterns.

## Technical Context

**Language/Version**: Markdown documentation, no code implementation  
**Primary Dependencies**: Documentation references these technologies:
- .NET 10.0 / C# 12
- .NET MAUI 10.0
- MauiReactor 4.0.3-beta
- Microsoft.Extensions.AI v9.5.0-preview.1.25262.9
- Microsoft.Extensions.AI.OpenAI v9.5.0-preview.1.25262.9
- Shiny.Extensions.Configuration v3.3.4
- Entity Framework Core 10.0
- OpenAI GPT-4o-mini model

**Storage**: N/A (documentation only)  
**Testing**: N/A (documentation validation through developer testing - see Success Criteria)  
**Target Platform**: Documentation consumed by developers on macOS/Windows/Linux  
**Project Type**: Documentation update for existing .NET MAUI mobile/desktop application  
**Performance Goals**: Developers can understand project in <5 minutes, successfully configure environment on first attempt  
**Constraints**: Zero secrets in documentation, all code examples must be copy-paste functional  
**Scale/Scope**: 2 files to update (README.md ~400 lines, CONTRIBUTING.md ~485 lines)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

All features must demonstrate alignment with constitutional principles:

- [x] **Code Quality Standards**: Documentation will follow clear structure, consistent formatting, proper markdown hierarchy. Self-documenting examples with clear explanations. N/A for code since this is documentation-only.
- [x] **Test-First Development**: N/A for documentation. Success criteria define validation through developer testing (3 test developers can successfully configure environment on first attempt).
- [x] **User Experience Consistency**: Documentation provides consistent formatting, clear visual hierarchy, proper heading levels, syntax-highlighted code blocks. All examples tested for accuracy.
- [x] **Performance Requirements**: Documentation enables 5-minute comprehension time. Examples are concise and directly copy-pasteable. No bloat or unnecessary content.

**Violations requiring justification**: None. Documentation updates align with all applicable constitutional principles. Test-First Development doesn't apply to documentation, but validation methodology is defined in Success Criteria.

## Project Structure

### Documentation (this feature)

```text
specs/002-update-documentation/
├── plan.md              # This file
├── research.md          # Phase 0: Research on documentation patterns and existing codebase
├── data-model.md        # Phase 1: Document structure mapping (existing content → new content)
├── quickstart.md        # Phase 1: Quick reference for implementing documentation updates
├── contracts/           # Phase 1: Documentation contracts (sections, content requirements)
│   └── README-sections.md
└── tasks.md             # Phase 2: Detailed implementation tasks (created by /speckit.tasks)
```

### Source Code (repository root)

```text
README.md                # Primary update target
docs/
├── CONTRIBUTING.md      # Secondary update target
├── MAUIREACTOR_PATTERNS.md  # Reference only (no changes)
├── DATA_LAYER.md        # Reference only (no changes)
└── SERVICES.md          # Reference only (no changes)

.gitignore               # Reference to verify secret exclusion patterns
BaristaNotes/
├── Services/
│   ├── AIAdviceService.cs        # Reference for documentation
│   └── IAIAdviceService.cs       # Reference for documentation
├── MauiProgram.cs                # Reference for Shiny configuration examples
├── appsettings.json              # Reference for configuration structure
└── Platforms/
    ├── Android/Assets/
    │   └── appsettings.Development.json    # Reference for Android config
    └── iOS/Resources/                      # Reference for iOS config location

BaristaNotes.Core/
└── Services/
    └── AIPromptBuilder.cs        # Reference for prompt builder documentation
```

**Structure Decision**: This is a documentation-only update targeting 2 existing files (README.md and docs/CONTRIBUTING.md). No source code changes required. References to existing codebase are for documentation accuracy only.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No constitutional violations. This section intentionally left blank.

---

## Phase 0: Outline & Research

**Objective**: Identify all technical details needed for accurate documentation and research best practices for documentation structure.

### Research Tasks

1. **Audit Current Codebase State**:
   - Review AIAdviceService implementation for accurate API documentation
   - Review AIPromptBuilder for prompt engineering explanation
   - Review Shiny configuration in MauiProgram.cs for code examples
   - Review appsettings.json structure across platforms
   - Verify .gitignore patterns for secret exclusion

2. **Analyze Existing Documentation Structure**:
   - Map current README.md structure and identify insertion points for AI features
   - Map current CONTRIBUTING.md structure and identify where to add secret handling guidance
   - Review tone and style of existing documentation for consistency
   - Identify cross-references and linking patterns

3. **Research Documentation Best Practices**:
   - Research effective onboarding documentation patterns (5-minute comprehension goal)
   - Research secret management documentation patterns (balancing security with clarity)
   - Research code example formats for maximum copy-paste success
   - Research educational documentation patterns for explaining architectural decisions

4. **Gather Technical Details from Recent Implementation**:
   - Package versions and dependencies from BaristaNotes.csproj
   - Configuration file locations per platform (iOS: project root, Android: Assets folder)
   - DEBUG conditional compilation pattern for environment-specific config
   - TastingNotes field addition to ShotRecord model
   - AI advice workflow: user interaction → service call → prompt building → OpenAI → response

### Research Questions to Answer

- **Q1**: What are the exact file paths for configuration files on each platform?
  - **Answer needed for**: FR-003 (step-by-step setup instructions)

- **Q2**: What are all the package names and versions for AI-related dependencies?
  - **Answer needed for**: FR-002 (document current dependencies)

- **Q3**: What is the exact Shiny configuration code pattern including DEBUG conditional?
  - **Answer needed for**: FR-005 (code examples for Shiny pattern)

- **Q4**: What architectural decisions were made and why?
  - **Answer needed for**: FR-009 (educational notes on decisions)

- **Q5**: What are common configuration errors and troubleshooting steps?
  - **Answer needed for**: FR-010 (troubleshooting guidance)

**Output**: `research.md` with findings from codebase audit and documentation best practices research.

---

## Phase 1: Design & Contracts

**Prerequisites:** `research.md` complete

### Design Artifacts to Create

1. **Document Structure Map** (`data-model.md`):
   - Map of README.md sections (existing → new/updated)
   - Map of CONTRIBUTING.md sections (existing → new/updated)
   - Outline of new "AI Features" section content
   - Outline of new "Configuration Management" section content
   - Outline of updated "Technology Stack" section
   - Outline of secret handling guidance in CONTRIBUTING.md

2. **Documentation Contracts** (`contracts/README-sections.md`):
   - Contract for "AI Features" section: required content, tone, examples
   - Contract for "Configuration Management" section: local vs. production comparison
   - Contract for "Technology Stack" updates: package list format
   - Contract for "Architecture" section updates: AI service layer description
   - Contract for CONTRIBUTING.md secret handling: 5-step guide format

3. **Quick Reference Guide** (`quickstart.md`):
   - Quick reference for writing configuration examples
   - Quick reference for explaining architectural decisions
   - Quick reference for formatting code blocks with proper syntax highlighting
   - Template for local vs. production comparison
   - Checklist for ensuring zero secrets in examples

### Documentation Structure Design

**README.md Updates**:
- Add "AI Features" section after "What You Can Do" (lines 11-22)
- Update "Technology Stack" section (lines 33-60) with new dependencies
- Update "Architecture" section (lines 61-88) with AI service layer
- Add "Configuration Management" section after "Getting Started" (lines 89-126)
- Update "What You Can Do" list to include AI advice feature (line 14)

**CONTRIBUTING.md Updates**:
- Add "Secret Management" section after "Development Workflow" (after line 111)
- Update "Testing Guidelines" section (lines 333-392) with configuration mocking guidance
- Update "Pull Request Process" checklist (lines 394-451) with secret verification step

**Output**: `data-model.md`, `contracts/README-sections.md`, `quickstart.md`

---

## Phase 1.5: Agent Context Update

**Prerequisites:** Phase 1 complete

Run the agent context update script to add new documentation patterns to the agent's knowledge:

```bash
cd /Users/davidortinau/work/BaristaNotes/BaristaNotes
.specify/scripts/bash/update-agent-context.sh copilot
```

This will:
- Detect that GitHub Copilot CLI is in use
- Update `.github/agents/copilot-instructions.md`
- Add documentation about AI features and configuration patterns
- Preserve existing manual additions between markers
- Add Shiny configuration patterns to technology list

**Output**: Updated `.github/agents/copilot-instructions.md`

---

## Phase 2: Task Breakdown

**Note**: Phase 2 is handled by the `/speckit.tasks` command, NOT by `/speckit.plan`. This section documents what will be created by that command.

The tasks.md file will break down implementation into atomic, testable tasks:

1. **Task Category: Audit Current State**
   - Tasks for reviewing each service/component to document
   - Tasks for verifying configuration patterns
   - Tasks for collecting package versions

2. **Task Category: Update README.md**
   - Tasks for each new section to add
   - Tasks for each existing section to update
   - Tasks for adding code examples
   - Tasks for adding architectural explanations

3. **Task Category: Update CONTRIBUTING.md**
   - Tasks for adding secret management guidance
   - Tasks for updating testing guidelines
   - Tasks for updating PR checklist

4. **Task Category: Validation**
   - Tasks for verifying all links work
   - Tasks for verifying all code examples compile/run
   - Tasks for verifying zero secrets in documentation
   - Tasks for developer testing (3 test developers)

**Output**: `/speckit.tasks` command will generate `tasks.md` with detailed implementation tasks.

---

## Status

- [x] Phase 0: Research - Ready to begin
- [ ] Phase 1: Design & Contracts - Pending Phase 0 completion
- [ ] Phase 1.5: Agent Context Update - Pending Phase 1 completion  
- [ ] Phase 2: Task Breakdown - Use `/speckit.tasks` command after Phase 1

**Next Command**: Begin Phase 0 research by examining codebase and documenting findings in `research.md`.
