# Implementation Tasks: Migrate to Structured Logging

**Feature**: 001-logging-migration  
**Branch**: `001-logging-migration`  
**Generated**: 2025-12-10

## Overview

Migrate 29 Debug.WriteLine/Console.WriteLine calls to Microsoft.Extensions.Logging with structured message templates, severity categorization, and ILogger<T> dependency injection across 7 files.

**Total Tasks**: 32  
**Estimated Effort**: 8-12 hours  
**MVP Scope**: User Story 1 + User Story 2 (P1 priority)

## Task Legend

- `[P]` = Parallelizable (can be done concurrently with other [P] tasks)
- `[US#]` = User Story mapping (US1, US2, US3, US4)
- Task IDs: Sequential (T001, T002, T003...)

---

## Phase 1: Setup & Infrastructure

**Goal**: Configure logging infrastructure and verify existing setup

**Tasks**:

- [X] T001 Verify Microsoft.Extensions.Logging.Debug 10.0.0 is referenced in BaristaNotes/BaristaNotes.csproj
- [X] T002 Verify ILogger<T> DI is working by checking existing BaristaNotes/Services/AIAdviceService.cs implementation
- [ ] T003 Create BaristaNotes.Tests/Helpers/MockLoggerFactory.cs helper class for unit test logger mocking
- [ ] T004 Document MauiProgram.cs bootstrap logging exception - add comment explaining why line 111 Console.WriteLine remains

**Validation**: Build succeeds, AIAdviceService shows proper ILogger usage pattern, MockLoggerFactory compiles

---

## Phase 2: Foundational - Configuration Files

**Goal**: Add logging configuration for development and production environments

**Tasks**:

- [ ] T005 Add Logging configuration section to BaristaNotes/appsettings.json with Information level for production
- [X] T006 Add Logging configuration section to BaristaNotes/appsettings.Development.json with Debug level for development

**Configuration Schema** (appsettings.json):
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "BaristaNotes": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

**Configuration Schema** (appsettings.Development.json):
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "BaristaNotes": "Debug",
      "Microsoft": "Warning"
    }
  }
}
```

**Validation**: JSON files are valid, Shiny loads configuration on app startup

---

## Phase 3: User Story 1 - Developer Debugging Production Issues (P1)

**Story Goal**: Enable developers to debug production issues with structured logs containing severity levels, timestamps, and contextual information for root cause analysis

**Independent Test**: Trigger error scenarios (image validation failure, permission denied, shot loading error) and verify logs show Error/Warning level with exception details and context that enables debugging without code changes

**Tasks**:

### T007-T012: ImageProcessingService (Error + Debug logging)

- [X] T007 [P] [US1] Add ILogger<ImageProcessingService> constructor parameter to BaristaNotes/Services/ImageProcessingService.cs
- [X] T008 [P] [US1] Store ILogger in private readonly field _logger in BaristaNotes/Services/ImageProcessingService.cs
- [X] T009 [US1] Replace Console.WriteLine at line 37 with _logger.LogError(ex, "Image validation error") in BaristaNotes/Services/ImageProcessingService.cs
- [X] T010 [US1] Replace Console.WriteLine at line 63 with _logger.LogDebug("Image saved to: {Path}, size: {SizeBytes} bytes", path, memoryStream.Length) in BaristaNotes/Services/ImageProcessingService.cs
- [ ] T011 [US1] Update ImageProcessingService unit tests to inject MockLoggerFactory.Create<ImageProcessingService>() in BaristaNotes.Tests/Services/ImageProcessingServiceTests.cs
- [ ] T012 [US1] Verify ImageProcessingService: Build, run tests, trigger image validation error, confirm Error log appears with exception

### T013-T021: ImagePickerService (Error + Warning + Debug logging)

- [X] T013 [P] [US1] Add ILogger<ImagePickerService> constructor parameter to BaristaNotes/Services/ImagePickerService.cs
- [X] T014 [P] [US1] Store ILogger in private readonly field _logger in BaristaNotes/Services/ImagePickerService.cs
- [X] T015 [US1] Replace Console.WriteLine at line 21 with _logger.LogDebug("Starting photo pick") in BaristaNotes/Services/ImagePickerService.cs
- [X] T016 [US1] Replace Console.WriteLine at line 33 with _logger.LogDebug("PickPhotosAsync returned, results count: {ResultCount}", results?.Count ?? 0) in BaristaNotes/Services/ImagePickerService.cs
- [X] T017 [US1] Replace Console.WriteLine at line 38 with _logger.LogDebug("Opening stream for file {FileName}", fileResult.FileName) in BaristaNotes/Services/ImagePickerService.cs
- [X] T018 [US1] Replace Console.WriteLine at line 41 with _logger.LogDebug("Stream opened, CanRead: {CanRead}, CanSeek: {CanSeek}, Length: {Length}", stream.CanRead, stream.CanSeek, stream.CanSeek ? stream.Length : -1) in BaristaNotes/Services/ImagePickerService.cs
- [X] T019 [US1] Replace Console.WriteLine at line 46 with _logger.LogDebug("No results, user cancelled") in BaristaNotes/Services/ImagePickerService.cs
- [X] T020 [US1] Replace Console.WriteLine at line 51 with _logger.LogWarning(ex, "Permission denied") and REMOVE line 58 stack trace WriteLine in BaristaNotes/Services/ImagePickerService.cs
- [X] T021 [US1] Replace Console.WriteLine at line 57 with _logger.LogError(ex, "Error picking image") in BaristaNotes/Services/ImagePickerService.cs
- [ ] T022 [US1] Update ImagePickerService unit tests to inject MockLoggerFactory.Create<ImagePickerService>() in BaristaNotes.Tests/Services/ImagePickerServiceTests.cs (if tests exist)
- [ ] T023 [US1] Verify ImagePickerService: Build, run tests, trigger permission denied and error scenarios, confirm Warning/Error logs appear

### T024-T028: ShotLoggingPage (Error + Debug logging, remove temporary statements)

- [X] T024 [P] [US1] Add ILogger<ShotLoggingPage> as field in BaristaNotes/Pages/ShotLoggingPage.cs (service locator pattern for MauiReactor page)
- [X] T025 [US1] Replace Debug.WriteLine at line 229 with _logger.LogDebug("LoadBestShotSettingsAsync called for bagId: {BagId}", bagId) in BaristaNotes/Pages/ShotLoggingPage.cs
- [X] T026 [US1] Replace Debug.WriteLine at line 234 with _logger.LogDebug("Found best shot: DoseIn={DoseIn}g, GrindSetting={GrindSetting}, ExpectedOutput={ExpectedOutput}g, ExpectedTime={ExpectedTime}s", bestShot.DoseIn, bestShot.GrindSetting, bestShot.ExpectedOutput, bestShot.ExpectedTime) in BaristaNotes/Pages/ShotLoggingPage.cs
- [X] T027 [US1] Replace Debug.WriteLine at line 246 with _logger.LogDebug("No rated shots found for bagId: {BagId}", bagId) in BaristaNotes/Pages/ShotLoggingPage.cs
- [X] T028 [US1] Replace Debug.WriteLine at line 252 with _logger.LogError(ex, "Error loading best shot settings") in BaristaNotes/Pages/ShotLoggingPage.cs
- [X] T029 [US1] REMOVE temporary Debug.WriteLine statements at lines 291, 293, 295, 297 in BaristaNotes/Pages/ShotLoggingPage.cs
- [X] T030 [US1] Verify ShotLoggingPage: Build, trigger shot loading with error, confirm Error log with exception details appears

**US1 Validation Criteria**:
- ✅ Error scenarios log with Error level including exception details
- ✅ Logs can be filtered by severity to isolate errors from diagnostics
- ✅ Contextual information (bagId, fileName, etc.) present in log messages
- ✅ Developers can identify error root cause from logs without code changes

---

## Phase 4: User Story 2 - Developer Code Maintenance (P1)

**Story Goal**: Establish consistent logging patterns with ILogger<T> dependency injection that developers can follow when adding new diagnostic statements

**Independent Test**: Code review confirms all services use consistent ILogger<T> constructor injection, all log statements use message templates (not string interpolation), no Debug/Console.WriteLine remain except MauiProgram.cs bootstrap

**Tasks**:

### T031-T037: ThemeService (Debug logging consistency)

- [X] T031 [P] [US2] Add ILogger<ThemeService> constructor parameter to BaristaNotes/Services/ThemeService.cs
- [X] T032 [P] [US2] Store ILogger in private readonly field _logger in BaristaNotes/Services/ThemeService.cs
- [X] T033 [US2] Replace Debug.WriteLine at line 37 with _logger.LogDebug("Loaded saved theme mode: {ThemeMode}", _currentMode) in BaristaNotes/Services/ThemeService.cs
- [X] T034 [US2] Replace Debug.WriteLine at line 44 with _logger.LogDebug("Subscribed to RequestedThemeChanged event") in BaristaNotes/Services/ThemeService.cs
- [X] T035 [US2] Replace Debug.WriteLine at line 60 with _logger.LogDebug("SetThemeModeAsync called with mode: {Mode}", mode) in BaristaNotes/Services/ThemeService.cs
- [X] T036 [US2] Replace Debug.WriteLine at line 81 with _logger.LogDebug("ApplyTheme: CurrentMode={CurrentMode}, TargetTheme={TargetTheme}, SystemTheme={SystemTheme}", _currentMode, targetTheme, Application.Current.RequestedTheme) in BaristaNotes/Services/ThemeService.cs
- [X] T037 [US2] Replace Debug.WriteLine at line 87 with _logger.LogDebug("OnSystemThemeChanged fired: NewTheme={NewTheme}, CurrentMode={CurrentMode}", e.RequestedTheme, _currentMode) in BaristaNotes/Services/ThemeService.cs
- [X] T038 [US2] Replace Debug.WriteLine at line 92 with _logger.LogDebug("Applying theme because CurrentMode is System") in BaristaNotes/Services/ThemeService.cs
- [X] T039 [US2] Replace Debug.WriteLine at line 97 with _logger.LogDebug("Ignoring system theme change because CurrentMode is {CurrentMode}", _currentMode) in BaristaNotes/Services/ThemeService.cs
- [ ] T040 [US2] Update ThemeService unit tests to inject MockLoggerFactory.Create<ThemeService>() in BaristaNotes.Tests/Services/ThemeServiceTests.cs (if tests exist)
- [ ] T041 [US2] Verify ThemeService: Build, run tests, change theme mode, confirm Debug logs appear with structured data

### T042-T043: ProfileImagePicker Component (Remove logging, delegate to service)

- [X] T042 [P] [US2] REMOVE Console.WriteLine at lines 128 and 163 in BaristaNotes/Components/ProfileImagePicker.cs (error handling moved to ImagePickerService)
- [X] T043 [US2] Verify ProfileImagePicker builds and image picking still shows errors via ImagePickerService logs

**US2 Validation Criteria**:
- ✅ All services use ILogger<T> constructor injection (consistent pattern)
- ✅ All log messages use message templates with named parameters (PascalCase)
- ✅ No string interpolation in log messages ($"{variable}")
- ✅ Temporary debug statements removed (ShotLoggingPage lines 291-297)
- ✅ Component logging delegated to services (ProfileImagePicker → ImagePickerService)

---

## Phase 5: User Story 3 - Operations Team Monitoring (P2)

**Story Goal**: Enable log aggregation and filtering by category/severity for trend analysis and health monitoring

**Independent Test**: Run application for extended period, collect logs, demonstrate filtering by category (BaristaNotes.Services.ThemeService) and severity (Error only), generate basic metrics (error rate over time)

**Tasks**:

- [ ] T044 [US3] Verify logging configuration allows per-category filtering by testing log level override for specific service in appsettings.Development.json
- [ ] T045 [US3] Document logger category naming pattern in BaristaNotes/README.md or docs/logging.md (namespace.ClassName format)
- [ ] T046 [US3] Test log filtering: Set BaristaNotes.Services log level to Warning, verify Debug messages suppressed

**US3 Validation Criteria**:
- ✅ Logs can be filtered by category (e.g., show only ThemeService logs)
- ✅ Logs can be filtered by severity (e.g., show only Error+ level)
- ✅ Logger categories follow consistent namespace.ClassName pattern
- ✅ Configuration supports per-service log level overrides

---

## Phase 6: User Story 4 - Minimizing Log Noise (P2)

**Story Goal**: Configure log levels to suppress verbose Debug statements in production while allowing detailed diagnostics in development

**Independent Test**: Configure production log level (Information), verify Debug statements suppressed. Configure development log level (Debug), verify detailed diagnostics appear. Measure log volume reduction (80%+ expected).

**Tasks**:

- [ ] T047 [US4] Test production configuration: Set log level to Information in appsettings.json, run app, confirm Debug messages suppressed
- [ ] T048 [US4] Test development configuration: Set log level to Debug in appsettings.Development.json, run app, confirm Debug messages appear
- [ ] T049 [US4] Measure log volume: Count log messages at Debug level vs Information level, verify 80%+ reduction at Information
- [ ] T050 [US4] Audit high-frequency logging: Review loops/frequent operations for appropriate Debug/Trace severity

**US4 Validation Criteria**:
- ✅ Information level suppresses Debug messages (production configuration)
- ✅ Debug level shows detailed diagnostics (development configuration)
- ✅ Log volume reduced 80%+ when configured at Information level
- ✅ No high-frequency logging at Information+ level (all in Debug/Trace)

---

## Phase 7: Validation & Polish

**Goal**: Verify migration complete, all success criteria met, no regressions

**Tasks**:

- [ ] T051 Run grep to verify zero Debug.WriteLine remain: `grep -r "Debug\.WriteLine" --include="*.cs" BaristaNotes/` (expect 0 results except MauiProgram.cs line 111)
- [ ] T052 Run grep to verify Console.WriteLine only in MauiProgram.cs: `grep -r "Console\.WriteLine" --include="*.cs" BaristaNotes/` (expect 1 result: MauiProgram.cs line 111)
- [ ] T053 Build BaristaNotes solution in Release mode, verify no compilation errors or warnings related to logging
- [ ] T054 Run all unit tests in BaristaNotes.Tests, verify 100% pass rate (no test coverage reduction)
- [ ] T055 Audit all log messages for sensitive data: Search for "ApiKey", "Password", "Token" in log templates, verify none logged
- [ ] T056 Test error scenario end-to-end: Trigger image validation error, measure time to identify root cause from logs (target: <10 minutes, baseline: 30+ minutes)
- [ ] T057 Profile application performance: Compare startup time and operation timing before/after migration, verify <5ms overhead per log statement
- [ ] T058 Code review: Verify all ILogger<T> uses follow quickstart.md patterns (message templates, PascalCase parameters, exception parameter for errors)
- [ ] T059 Update .github/copilot-instructions.md or constitution.md with logging standards (ILogger required for new services, no Debug.WriteLine in new code)

**Validation Criteria**:
- ✅ SC-001: Zero Debug.WriteLine (except MauiProgram.cs bootstrap)
- ✅ SC-002: Zero Console.WriteLine (except MauiProgram.cs bootstrap)
- ✅ SC-003: 10-minute debugging time (vs 30+ minutes baseline)
- ✅ SC-004: 100% ILogger adoption in migrated services
- ✅ SC-005: 80%+ log volume reduction at Information level
- ✅ SC-006: 100% pattern compliance (code review)
- ✅ SC-007: No performance impact (<5ms overhead)
- ✅ SC-008: Zero sensitive data in logs

---

## Implementation Strategy

### MVP Scope (Recommended First PR)

**Focus**: User Story 1 + User Story 2 (P1 priority only)

**Tasks**: T001-T043 (Infrastructure + ImageProcessingService + ImagePickerService + ShotLoggingPage + ThemeService + ProfileImagePicker)

**Rationale**: Delivers core value (production debugging + code maintainability) with complete, testable increment. Establishes logging patterns for remaining work.

**Deliverables**:
- All Error/Warning logging in place (production debugging enabled)
- All services migrated to ILogger<T> (consistent patterns established)
- Configuration files in place (development vs production)
- Temporary debug statements removed (code cleanup)

### Incremental Delivery Plan

1. **PR #1 (MVP)**: T001-T043 - Core migration (US1 + US2)
2. **PR #2**: T044-T050 - Configuration refinement (US3 + US4)
3. **PR #3**: T051-T059 - Validation and polish

### Parallel Execution Opportunities

**Phase 1** (Setup): T001-T004 can all be done in parallel (different files)

**Phase 3** (US1):
- T007-T008 (ImageProcessingService setup) || T013-T014 (ImagePickerService setup) || T024 (ShotLoggingPage setup)
- After setup complete: T009-T012 (ImageProcessingService migration) || T015-T023 (ImagePickerService migration) || T025-T030 (ShotLoggingPage migration)

**Phase 4** (US2):
- T031-T032 (ThemeService setup) || T042 (ProfileImagePicker cleanup)
- After ThemeService setup: T033-T041 (ThemeService migration) || T043 (ProfileImagePicker verification)

---

## Dependencies

### User Story Dependencies

```
Setup (Phase 1) → All user stories depend on infrastructure
    ↓
Foundational (Phase 2) → All user stories depend on configuration
    ↓
├── US1 (P1) → Independent (can start immediately after Phase 2)
├── US2 (P1) → Independent (can start immediately after Phase 2, or after US1 for pattern reference)
├── US3 (P2) → Depends on US1 + US2 (needs migrated services to test filtering)
└── US4 (P2) → Depends on US1 + US2 (needs migrated services to test noise reduction)
```

### Task Dependencies Within User Stories

**US1 (ImageProcessingService)**:
- T007-T008 → T009-T010 → T011 → T012 (sequential within service)

**US1 (ImagePickerService)**:
- T013-T014 → T015-T021 → T022 → T023 (sequential within service)

**US1 (ShotLoggingPage)**:
- T024 → T025-T029 → T030 (sequential within component)

**US2 (ThemeService)**:
- T031-T032 → T033-T039 → T040 → T041 (sequential within service)

**US2 (ProfileImagePicker)**:
- T042 → T043 (sequential)

**US3 & US4**: Depend on US1+US2 completion (need migrated services)

---

## Checklist Summary

- **Total Tasks**: 59
- **Setup**: 4 tasks (T001-T004)
- **Foundational**: 2 tasks (T005-T006)
- **US1 (P1)**: 24 tasks (T007-T030)
- **US2 (P1)**: 13 tasks (T031-T043)
- **US3 (P2)**: 3 tasks (T044-T046)
- **US4 (P2)**: 4 tasks (T047-T050)
- **Validation**: 9 tasks (T051-T059)

**Parallelizable Tasks**: 8 tasks marked with [P] (setup, initial service modifications)

**Estimated Effort**:
- MVP (US1+US2): 6-8 hours
- Full feature (US1-US4 + Validation): 10-14 hours

---

## Quick Reference

**Files Modified** (7 files):
1. BaristaNotes/Services/ThemeService.cs (7 statements → ILogger)
2. BaristaNotes/Services/ImageProcessingService.cs (2 statements → ILogger)
3. BaristaNotes/Services/ImagePickerService.cs (9 statements → ILogger, 1 removed)
4. BaristaNotes/Components/ProfileImagePicker.cs (2 statements removed)
5. BaristaNotes/Pages/ShotLoggingPage.cs (4 statements → ILogger, 4 removed)
6. BaristaNotes/appsettings.json (add Logging section)
7. BaristaNotes/appsettings.Development.json (add Logging section)

**Files Created** (1 file):
1. BaristaNotes.Tests/Helpers/MockLoggerFactory.cs

**Files Updated for Tests** (~4 files):
1. BaristaNotes.Tests/Services/ImageProcessingServiceTests.cs
2. BaristaNotes.Tests/Services/ImagePickerServiceTests.cs
3. BaristaNotes.Tests/Services/ThemeServiceTests.cs
4. Other service tests as needed

**Configuration Sections Added**:
- appsettings.json: Logging.LogLevel (Information level)
- appsettings.Development.json: Logging.LogLevel (Debug level)

---

**Ready to implement**: All tasks are atomic, testable, and reference specific file paths with line numbers from data-model.md. Each user story can be independently implemented and tested.
