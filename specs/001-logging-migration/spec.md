# Feature Specification: Migrate to Structured Logging

**Feature Branch**: `001-logging-migration`  
**Created**: 2025-12-10  
**Status**: Draft  
**Input**: User description: "we have a lot of debug.writeline and maybe some console.writeline calls in the code. I want to minimize that to only the essential calls, and I want to use the Microsoft.Extensions.Logging to handle logging instead."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Developer Debugging Production Issues (Priority: P1)

A developer receives a bug report from production and needs to understand what happened. They need log entries that provide context about the application's behavior, including timestamps, severity levels, and structured data that can be filtered and searched efficiently.

**Why this priority**: Production debugging is critical for maintaining application quality and user satisfaction. Without proper logging, developers waste hours trying to reproduce issues or resort to adding temporary Debug statements and redeploying. Structured logging with severity levels enables quick identification of errors, warnings, and diagnostic information.

**Independent Test**: Can be fully tested by triggering various application scenarios (successful operations, errors, warnings) and verifying that log entries are written with appropriate severity levels, timestamps, and contextual information that enables root cause analysis without code changes or redeployment.

**Acceptance Scenarios**:

1. **Given** an error occurs in the application, **When** logs are reviewed, **Then** the error is logged with Error level, includes exception details, stack trace, and context about what operation failed
2. **Given** a developer is investigating an issue, **When** they filter logs by severity level, **Then** they can isolate errors from informational messages and focus on problems
3. **Given** the application is running, **When** critical operations execute, **Then** key diagnostic information is logged with appropriate severity (Debug for detailed diagnostics, Information for significant events, Warning for recoverable issues)

---

### User Story 2 - Developer Code Maintenance (Priority: P1)

A developer is working on the codebase and encounters Debug.WriteLine or Console.WriteLine statements scattered throughout the code. They need to understand what's being logged, whether it's still needed, and how to maintain consistency across the application. With structured logging, they can add new log statements using a standard pattern with proper dependency injection.

**Why this priority**: Code maintainability directly impacts development velocity and bug introduction rate. Inconsistent logging patterns (Debug vs Console vs proper logging) create confusion, make it harder to add new diagnostic information, and result in important diagnostics being mixed with temporary debug statements. A unified logging approach reduces cognitive load and establishes clear patterns.

**Independent Test**: Can be fully tested by reviewing the codebase for logging consistency, verifying that all services use ILogger dependency injection, and confirming that log statements follow consistent patterns with appropriate severity levels and message templates.

**Acceptance Scenarios**:

1. **Given** a developer adds diagnostic logging to a service, **When** they review existing patterns, **Then** they find consistent ILogger usage with clear examples of appropriate severity levels and message templates
2. **Given** a service needs logging capabilities, **When** it's constructed, **Then** ILogger<T> is injected through the constructor following dependency injection patterns
3. **Given** temporary debug statements exist in code, **When** code review occurs, **Then** they're identified as inconsistent with the logging standard and flagged for replacement or removal

---

### User Story 3 - Operations Team Monitoring Application Health (Priority: P2)

The operations team needs to monitor the application's health and identify trends or recurring issues. They need logs that can be aggregated, filtered by severity, and analyzed over time to identify patterns like increasing error rates, performance degradation, or resource exhaustion.

**Why this priority**: Proactive monitoring prevents issues from impacting users. While P1 stories focus on immediate debugging and code quality, this story enables longer-term operational excellence through trend analysis and early warning systems. It's lower priority because basic functionality works without it, but it significantly improves operational maturity.

**Independent Test**: Can be fully tested by running the application over time, collecting logs, and demonstrating that log entries can be filtered by severity, grouped by category, and analyzed to identify patterns (e.g., "error rate increased 200% after deployment").

**Acceptance Scenarios**:

1. **Given** the application is deployed, **When** logs are aggregated over 24 hours, **Then** the operations team can generate reports showing error rates, warning trends, and normal operation confirmation
2. **Given** multiple services are logging, **When** logs are filtered by category (e.g., "BaristaNotes.Services.AIAdviceService"), **Then** the team can isolate behavior of specific components
3. **Given** resource issues occur, **When** logs are reviewed, **Then** diagnostic information about memory usage, API call counts, and operation timing is available for analysis

---

### User Story 4 - Minimizing Log Noise (Priority: P2)

A developer or operator reviewing logs encounters too much noise from overly verbose logging that obscures important information. They need the ability to configure log levels to show only relevant information in different environments (Debug level in development, Information+ in production).

**Why this priority**: Log noise reduces effectiveness of all other stories. However, this is P2 because the initial migration establishes the foundation, and noise reduction can be refined after core logging is in place. It's essential for long-term usability but not blocking for initial deployment.

**Independent Test**: Can be fully tested by configuring different log levels (Debug, Information, Warning, Error) and verifying that only messages at or above the configured level appear in output, and that development environments can be more verbose than production.

**Acceptance Scenarios**:

1. **Given** the application is configured with log level "Information", **When** code executes, **Then** Debug-level messages are suppressed and only Information, Warning, and Error messages appear
2. **Given** a developer is troubleshooting locally, **When** they set log level to "Debug", **Then** detailed diagnostic information appears to aid investigation
3. **Given** production deployment occurs, **When** logging is configured, **Then** the default level is "Information" to balance diagnostic value with performance and log volume

---

### Edge Cases

- What happens when logging infrastructure fails (e.g., cannot write to log destination)? Application should continue functioning, logging system should not crash the app
- How are extremely large log messages handled (e.g., logging entire API responses)? Should truncate or use structured data references instead of full content
- What happens with circular dependencies if logging is used in core infrastructure services?
- How are sensitive data (API keys, passwords, personal information) prevented from appearing in logs? Log message templates should avoid sensitive fields
- What happens with high-frequency logging (e.g., logging every iteration of a loop with 10,000 items)? Should use appropriate severity (Trace/Debug) and be configurable

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST replace all Debug.WriteLine calls with appropriate Microsoft.Extensions.Logging calls using ILogger<T>
- **FR-002**: System MUST replace all Console.WriteLine calls with appropriate Microsoft.Extensions.Logging calls using ILogger<T>  
- **FR-003**: Logging statements MUST be categorized by severity level: Trace (most detailed), Debug (diagnostic info), Information (significant events), Warning (unexpected but recoverable), Error (failures), Critical (system-threatening failures)
- **FR-004**: Services requiring logging MUST receive ILogger<T> through constructor dependency injection where T is the service class name
- **FR-005**: Each logging statement MUST be reviewed to determine if it's still needed; temporary debug statements should be removed rather than migrated
- **FR-006**: Log messages MUST use structured logging with message templates (e.g., "Shot {ShotId} logged with rating {Rating}") rather than string concatenation
- **FR-007**: Logging configuration MUST be added to appsettings.json with configurable minimum log levels per namespace/category
- **FR-008**: System MUST continue using existing Microsoft.Extensions.Logging.Debug package (version 10.0.0) without introducing new dependencies
- **FR-009**: Log messages MUST NOT contain sensitive information (API keys, passwords, personal identifiable information)
- **FR-010**: High-frequency logging scenarios MUST use Debug or Trace level to allow suppression in production
- **FR-011**: Error logging MUST include exception objects when available using LogError(exception, message) pattern
- **FR-012**: Each service's logging category MUST match the full type name (e.g., ILogger<AIAdviceService> creates category "BaristaNotes.Services.AIAdviceService")

### Non-Functional Requirements (Constitution-Mandated)

**Performance** *(Per Principle IV: Performance Requirements)*:
- **NFR-P1**: Logging calls MUST NOT measurably impact user-perceived response time (<5ms overhead per operation)
- **NFR-P2**: String formatting for suppressed log levels MUST NOT occur (logging framework handles this)
- **NFR-P3**: Log writing MUST NOT block the main UI thread or critical operations

**Code Quality** *(Per Principle I: Code Quality Standards)*:
- **NFR-Q1**: All logging statements MUST follow consistent patterns with message templates
- **NFR-Q2**: Services MUST NOT have logging-related conditional compilation directives (#if DEBUG)
- **NFR-Q3**: Code coverage MUST be maintained at current levels (logging changes should not reduce testability)
- **NFR-Q4**: Logging infrastructure MUST be mockable for unit testing (ILogger interface enables mocking)

### Key Entities

This feature involves modifying existing services and components to use structured logging:

- **Logger Categories**: Each service/class that logs will have its own category (e.g., "BaristaNotes.Services.AIAdviceService", "BaristaNotes.Pages.ShotLoggingPage")
- **Log Levels**: Trace (0), Debug (1), Information (2), Warning (3), Error (4), Critical (5) - higher numbers = more severe
- **Log Configuration**: JSON-based configuration specifying minimum log level per category or namespace pattern
- **Message Templates**: Structured log messages with placeholders (e.g., "Processing {ItemCount} items")

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Zero Debug.WriteLine calls remain in the codebase after migration (currently 15 calls to replace)
- **SC-002**: Zero Console.WriteLine calls remain in the codebase after migration (currently 14 calls to replace)
- **SC-003**: When an error occurs, developers can identify the root cause within 10 minutes by reviewing structured logs (vs. 30+ minutes with current Debug statements)
- **SC-004**: All services that log diagnostic information use ILogger<T> dependency injection (100% adoption rate for services that need logging)
- **SC-005**: Production logs can be filtered to show only Warning or higher severity, reducing log volume by at least 80% while retaining all error information
- **SC-006**: Developers adding new logging statements follow the ILogger pattern 100% of the time (measured by code review)
- **SC-007**: Application performance is unchanged or improved (logging overhead <5ms per operation, measured via profiling)
- **SC-008**: Zero sensitive information (API keys, passwords) appears in logs after migration (verified by log auditing)

## Assumptions

- The existing Microsoft.Extensions.Logging.Debug package (version 10.0.0) provides sufficient capabilities; no additional logging providers (Serilog, NLog, etc.) are required for this migration
- The current 29 Debug.WriteLine/Console.WriteLine calls are distributed across ~7 files; this is manageable for a single feature implementation
- Some logging statements may be temporary debug code that should be removed rather than migrated (decision made during code review)
- .NET MAUI applications support standard Microsoft.Extensions.Logging patterns with ILogger<T> dependency injection through the MauiProgram.cs builder
- Development team is familiar with log severity levels (Debug, Information, Warning, Error) and can make appropriate categorization decisions
- Log output will continue using Debug output provider during development; production log destinations (file, cloud) can be added later if needed
- Structured logging with message templates is preferred over string interpolation for better log parsing and filtering

## Dependencies

- Existing Microsoft.Extensions.Logging.Debug package (version 10.0.0) already referenced in BaristaNotes.csproj
- MauiProgram.cs configuration for dependency injection (already in place)
- Understanding of current codebase locations with Debug/Console.WriteLine calls (identified: UserProfileService.cs, MauiProgram.cs, ProfileImagePicker.cs, ShotLoggingPage.cs, ImagePickerService.cs, ThemeService.cs, ImageProcessingService.cs)

## Out of Scope

- Adding new logging providers (Serilog, NLog, Application Insights, etc.) - this migration uses existing Microsoft.Extensions.Logging.Debug
- Creating centralized logging infrastructure or log aggregation services
- Implementing structured logging viewers or log analysis tools
- Adding performance profiling or tracing beyond basic logging
- Migrating logging in test projects (focus on application code)
- Implementing log rotation, archival, or retention policies
- Adding logging to new code locations (only migrating existing Debug/Console statements)
- Creating logging best practices documentation (focus on implementation)
- Implementing custom log formatters or log enrichers
