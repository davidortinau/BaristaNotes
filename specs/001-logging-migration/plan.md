# Implementation Plan: Migrate to Structured Logging

**Branch**: `001-logging-migration` | **Date**: 2025-12-10 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `/specs/001-logging-migration/spec.md`

## Summary

Migrate all Debug.WriteLine and Console.WriteLine calls (29 total across 7 files) to Microsoft.Extensions.Logging with structured message templates, severity categorization, and ILogger<T> dependency injection. This migration establishes consistent logging patterns, enables production debugging through filtered logs, and improves code maintainability by replacing ad-hoc debug statements with professional logging infrastructure.

## Technical Context

**Language/Version**: C# 12 / .NET 10.0  
**Primary Dependencies**: Microsoft.Extensions.Logging.Debug 10.0.0 (already installed), Microsoft.Extensions.DependencyInjection (MAUI framework)  
**Storage**: N/A (logging only, no data persistence changes)  
**Testing**: xUnit (BaristaNotes.Tests project), Mock<ILogger<T>> for unit testing logging behavior  
**Target Platform**: .NET MAUI 10.0 - iOS 15+, Android 8.0+, MacCatalyst, Windows 10.0.19041+  
**Project Type**: Mobile/Desktop application (MAUI multi-platform)  
**Performance Goals**: <5ms overhead per logging operation, zero impact on UI thread responsiveness, suppressed log levels must not trigger string formatting  
**Constraints**: Use existing Microsoft.Extensions.Logging.Debug provider, no new package dependencies, maintain current test coverage levels  
**Scale/Scope**: 7 files with logging statements (UserProfileService.cs, MauiProgram.cs, ProfileImagePicker.cs, ShotLoggingPage.cs, ImagePickerService.cs, ThemeService.cs, ImageProcessingService.cs), 29 total Debug/Console.WriteLine calls to migrate or remove

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

All features must demonstrate alignment with constitutional principles:

- [x] **Code Quality Standards**: Structured logging improves code quality by establishing consistent patterns. Each service will use ILogger<T> through constructor injection (single responsibility). Message templates prevent string concatenation complexity. Static analysis will catch missing logger injections.

- [x] **Test-First Development**: Logging infrastructure is mockable (ILogger interface). Unit tests can verify log statements are called with appropriate severity levels using Mock<ILogger<T>>. Integration tests can verify log output in DEBUG configuration. Test coverage maintained at current levels (no reduction).

- [x] **User Experience Consistency**: Logging is developer-facing infrastructure, not user-facing UI. No UX impact. Error messages to users remain unchanged (separate from logging infrastructure).

- [x] **Performance Requirements**: Microsoft.Extensions.Logging framework is designed for performance. Suppressed log levels do not trigger string formatting (lazy evaluation). <5ms overhead per operation meets requirements. No UI thread blocking (logging is synchronous to Debug output but non-blocking in MAUI).

**Violations requiring justification**: None. All constitutional principles are satisfied.

**Complexity Tracking**: 
- **Circular dependency risk**: MauiProgram.cs logs during DI setup. Resolution: Use Debug.WriteLine only during DI bootstrap, migrate other statements.
- **High-frequency logging**: Some loops may log frequently. Resolution: Use Debug/Trace severity levels that are suppressed in production.

## Project Structure

### Documentation (this feature)

```text
specs/001-logging-migration/
├── plan.md              # This file
├── research.md          # Phase 0: Logging patterns, severity guidelines, migration strategies
├── data-model.md        # Phase 1: Logger categories, severity mapping, configuration schema
├── quickstart.md        # Phase 1: Quick reference for adding logging, message template examples
├── contracts/           # Phase 1: Logging configuration JSON schema, appsettings structure
│   └── logging-config-schema.json
└── tasks.md             # Phase 2: Created by /speckit.tasks command (NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
BaristaNotes/
├── Services/
│   ├── AIAdviceService.cs          # Already uses ILogger<T> (no changes needed)
│   ├── UserProfileService.cs       # MIGRATE: Debug.WriteLine → ILogger<UserProfileService>
│   ├── ImagePickerService.cs       # MIGRATE: Console.WriteLine → ILogger<ImagePickerService>
│   ├── ThemeService.cs             # MIGRATE: Debug.WriteLine → ILogger<ThemeService>
│   └── ImageProcessingService.cs   # MIGRATE: Console.WriteLine → ILogger<ImageProcessingService>
├── Components/
│   └── ProfileImagePicker.cs       # MIGRATE: Debug.WriteLine → ILogger<ProfileImagePicker>
├── Pages/
│   └── ShotLoggingPage.cs          # MIGRATE: Debug.WriteLine → ILogger<ShotLoggingPage>
├── MauiProgram.cs                  # REVIEW: Keep bootstrap logging as Debug.WriteLine
├── appsettings.json                # ADD: Logging configuration section
└── appsettings.Development.json    # ADD: Debug-level logging for development

BaristaNotes.Tests/
├── Services/
│   └── *ServiceTests.cs            # UPDATE: Mock ILogger<T> in unit tests
└── Helpers/
    └── MockLoggerFactory.cs        # NEW: Helper for creating mock loggers in tests
```

### Phase Artifacts (generated by /speckit.plan)

- **research.md**: Best practices for Microsoft.Extensions.Logging, severity level guidelines, message template patterns, DI integration patterns
- **data-model.md**: Logger category naming conventions, severity level mappings for each current WriteLine statement, configuration schema for appsettings.json
- **contracts/logging-config-schema.json**: JSON schema for Logging section in appsettings.json with LogLevel configuration
- **quickstart.md**: Quick reference for developers adding new logging statements, message template examples, common patterns

## Phase 0: Research & Outline

**Goal**: Resolve all "NEEDS CLARIFICATION" items and establish technical foundation.

### Research Questions

1. **Q: What are Microsoft.Extensions.Logging best practices for .NET MAUI applications?**
   - How is ILogger<T> registered in MauiProgram.cs?
   - What are the default log providers for MAUI?
   - How do log levels filter in Debug vs Release configurations?
   - What is the performance overhead of logging?

2. **Q: How should severity levels be assigned to existing Debug.WriteLine calls?**
   - Which statements are temporary debug code (should be removed)?
   - Which statements are diagnostic information (Debug level)?
   - Which statements are significant events (Information level)?
   - Which statements are errors or warnings?

3. **Q: What are structured logging message template best practices?**
   - When to use message templates vs string interpolation?
   - How to avoid sensitive data in logs?
   - How to log exceptions properly?
   - What are the naming conventions for template parameters?

4. **Q: How should logging be configured for different environments?**
   - What log levels for Development vs Production?
   - How to configure per-namespace log levels?
   - Where to store configuration (appsettings.json)?
   - How to override log levels at runtime?

5. **Q: How should services be refactored to receive ILogger<T>?**
   - Constructor injection pattern?
   - What if service has no constructor (static methods)?
   - How to inject loggers into MauiReactor components?
   - How to handle circular dependency with logging infrastructure?

6. **Q: How should unit tests mock ILogger<T>?**
   - Use Mock<ILogger<T>> from Moq?
   - How to verify log statements in tests?
   - Should tests verify log content or just that logging occurred?
   - How to test different log levels?

### Research Output

**Deliverable**: `research.md` containing:
- Decision rationale for each severity level assignment
- Message template patterns with examples
- Configuration schema for appsettings.json
- Constructor injection patterns for each service type
- Test mocking patterns for ILogger<T>
- Migration strategy for each file (migrate vs remove)

**Completion Criteria**: All NEEDS CLARIFICATION items resolved, ready to proceed to Phase 1 design.

## Phase 1: Design & Contracts

**Goal**: Define data structures, contracts, and implementation reference guide.

### Design Artifacts

1. **data-model.md**: Document logger category structure
   - Category naming convention: Full type name (e.g., "BaristaNotes.Services.UserProfileService")
   - Severity level mapping for each existing Debug/Console.WriteLine statement
   - Configuration schema: Logging section structure for appsettings.json
   - State transitions: N/A (stateless logging infrastructure)

2. **contracts/logging-config-schema.json**: JSON schema for logging configuration
   - LogLevel section with Default and per-category overrides
   - Example configurations for Development and Production environments
   - Validation rules for log level values (Trace, Debug, Information, Warning, Error, Critical)

3. **quickstart.md**: Developer reference guide
   - Pattern 1: Adding ILogger<T> to service constructor
   - Pattern 2: Using message templates with structured data
   - Pattern 3: Logging exceptions with context
   - Pattern 4: Choosing appropriate severity levels
   - Anti-patterns: String concatenation, sensitive data, high-frequency Debug statements

### Agent Context Update

**Script**: `.specify/scripts/bash/update-agent-context.sh copilot`

**Technologies to add**:
- Microsoft.Extensions.Logging (structured logging framework)
- ILogger<T> interface for dependency injection
- Message templates with named parameters
- Log levels: Trace, Debug, Information, Warning, Error, Critical
- appsettings.json configuration for log filtering

**Completion Criteria**: Agent context updated with logging patterns, data-model.md defines all categories, contracts/ contains JSON schema, quickstart.md provides copy-paste examples.

## Phase 2: Execution Workflow

**Note**: This phase is executed by the `/speckit.tasks` command, which generates `tasks.md` with detailed, atomic implementation steps.

The tasks will be organized as follows:

1. **Infrastructure Setup**: Add logging configuration to appsettings.json, verify DI setup in MauiProgram.cs
2. **Service Migration (US1 & US2 - P1)**: Migrate each service file to use ILogger<T> with appropriate severity levels
3. **Component Migration**: Migrate MauiReactor components (ProfileImagePicker, ShotLoggingPage)
4. **Review & Cleanup**: Remove temporary debug statements, verify no Debug/Console.WriteLine remain
5. **Test Updates**: Update unit tests to mock ILogger<T>, verify logging behavior
6. **Configuration (US4 - P2)**: Add Development and Production log level configurations
7. **Validation**: Run application, verify logs appear correctly, test log filtering by severity

**Execution**: Run `/speckit.tasks` to generate the detailed task breakdown from this plan.

## Success Criteria Validation

Each success criterion maps to specific implementation tasks:

- **SC-001** (Zero Debug.WriteLine): Task will verify with `grep -r "Debug\.WriteLine" --include="*.cs"` returns 0 results (except MauiProgram.cs bootstrap)
- **SC-002** (Zero Console.WriteLine): Task will verify with `grep -r "Console\.WriteLine" --include="*.cs"` returns 0 results
- **SC-003** (10-minute debugging): Integration test will simulate error scenario, verify structured log output enables root cause identification
- **SC-004** (100% ILogger adoption): Code review checklist item to verify all services with logging use ILogger<T>
- **SC-005** (80% log volume reduction): Task will measure log output before/after with different log levels configured
- **SC-006** (100% pattern follow-through): Enforced through code review and static analysis (no Debug/Console.WriteLine in new code)
- **SC-007** (No performance impact): Task will profile application startup and common operations before/after migration
- **SC-008** (Zero sensitive data): Task will audit all log messages for API keys, passwords, PII before migration

## Risks & Mitigation

1. **Risk**: Circular dependency if logging infrastructure itself needs logging during initialization
   - **Mitigation**: Keep MauiProgram.cs bootstrap logging as Debug.WriteLine, migrate application-level logging only
   - **Detection**: Compile-time error if circular dependency introduced

2. **Risk**: Breaking existing functionality if logging statements have side effects
   - **Mitigation**: Code review to identify any statements where WriteLine call has side effects (e.g., in conditional expressions)
   - **Detection**: Existing tests should catch behavioral changes

3. **Risk**: Performance degradation if high-frequency logging not properly filtered
   - **Mitigation**: Use Debug/Trace severity for high-frequency statements, configure production log level to Information+
   - **Detection**: Performance profiling tasks in SC-007 validation

4. **Risk**: Developers continue using Debug.WriteLine in new code
   - **Mitigation**: Update code review checklist, add static analysis rule if possible
   - **Detection**: Pre-commit hooks or CI checks for Debug/Console.WriteLine patterns

5. **Risk**: Log configuration errors in appsettings.json break application startup
   - **Mitigation**: Validate JSON schema in CI, logging framework has safe defaults if config invalid
   - **Detection**: Application startup tests

## Dependencies

- Microsoft.Extensions.Logging.Debug 10.0.0 (already installed) ✅
- MauiProgram.cs dependency injection setup (already configured) ✅
- xUnit and Moq for unit testing (already in BaristaNotes.Tests project) ✅
- Understanding of current Debug/Console.WriteLine locations (already identified) ✅

## Out of Scope (Explicitly)

- Adding new logging providers (Serilog, NLog, Application Insights, etc.)
- Centralized log aggregation infrastructure (Splunk, ELK stack, etc.)
- Custom log formatters or JSON-structured log output
- Performance profiling beyond basic logging overhead measurement
- Logging in test projects (focus on application code only)
- Log rotation, archival, or retention policies
- Correlation IDs or distributed tracing
- Logging best practices documentation (focus on implementation)

## Next Steps

1. **Immediate**: Run `/speckit.plan` to generate research.md, data-model.md, quickstart.md, and contracts/
2. **After Phase 1 Complete**: Run `/speckit.tasks` to generate detailed implementation tasks in tasks.md
3. **Implementation**: Follow tasks.md checklist, marking tasks complete as work progresses
4. **Validation**: Execute success criteria validation tasks before marking feature complete

**Ready for Phase 0 Research**: ✅ Constitution check passed, technical context defined, no blocking unknowns.
