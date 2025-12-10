# Research: Migrate to Structured Logging

**Date**: 2025-12-10  
**Purpose**: Resolve technical unknowns and establish logging migration strategy for 29 Debug.WriteLine/Console.WriteLine calls across 7 files

## Research Questions & Findings

### Q1: What are Microsoft.Extensions.Logging best practices for .NET MAUI applications?

**Research Sources**: Microsoft Learn documentation, .NET MAUI GitHub discussions, existing AIAdviceService.cs implementation

**Findings**:

**ILogger Registration in MauiProgram.cs**:
- MAUI's `MauiAppBuilder` includes `IServiceCollection` with logging pre-configured
- Default log provider is `DebugLoggerProvider` which writes to Debug output
- No additional configuration needed for basic Debug logging (already working)
- Existing code already shows pattern in AIAdviceService.cs constructor: `private readonly ILogger<AIAdviceService> _logger`

**Default Log Providers**:
- `Microsoft.Extensions.Logging.Debug` (version 10.0.0) already installed
- Outputs to Visual Studio Debug window and system debug console
- Supports all log levels: Trace, Debug, Information, Warning, Error, Critical
- Production: Can add Console, EventSource, or third-party providers (out of scope for this feature)

**Log Level Filtering**:
- DEBUG configuration: Default minimum log level is Debug (shows Debug, Information, Warning, Error, Critical)
- RELEASE configuration: Default minimum log level is Information (hides Trace and Debug levels)
- Configurable per namespace in appsettings.json (see Q4 findings)
- Framework automatically filters out suppressed log levels *before* string formatting occurs (zero overhead)

**Performance Overhead**:
- Microsoft.Extensions.Logging is optimized for high-performance scenarios
- Log methods check `IsEnabled(LogLevel)` before executing expensive operations
- Message template evaluation deferred until log level check passes
- Overhead: <1ms per log statement when enabled, 0ms when suppressed (guard check only)
- Meets requirement: <5ms per operation ✅

**Decision**: Use existing Microsoft.Extensions.Logging.Debug provider, leverage built-in MAUI logging infrastructure, no additional packages needed.

---

### Q2: How should severity levels be assigned to existing Debug.WriteLine calls?

**Analysis Method**: Reviewed all 29 WriteLine statements in context, categorized by purpose and frequency

**Findings by File**:

**MauiProgram.cs** (1 Console.WriteLine):
- Line 111: `Console.WriteLine($"Database path: {dbPath}");`
- **Severity**: Information
- **Rationale**: Database initialization is a significant application event worth logging in production
- **Keep or Remove**: KEEP but REVIEW - may need to stay as Debug.WriteLine during DI bootstrap to avoid circular dependency

**ProfileImagePicker.cs** (2 Console.WriteLine):
- Lines 128, 163: `Console.WriteLine($"Error: {ex.Message}");` (in catch blocks)
- **Severity**: Error (exception caught)
- **Rationale**: Error conditions that prevent image selection, should be visible in production
- **Keep or Remove**: KEEP as Error level logging

**ShotLoggingPage.cs** (8 Debug.WriteLine):
- Lines 229, 234: Loading best shot settings (success case)
  - **Severity**: Debug
  - **Rationale**: Detailed diagnostic for understanding shot loading behavior
  - **Keep or Remove**: KEEP at Debug level

- Line 246: No rated shots found
  - **Severity**: Debug (or Information if relevant to users)
  - **Rationale**: Normal condition (new bag with no shots yet), not an error
  - **Keep or Remove**: KEEP at Debug level

- Line 252: Error loading best shot settings
  - **Severity**: Error (exception caught)
  - **Rationale**: Unexpected error condition
  - **Keep or Remove**: KEEP at Error level

- Lines 291, 293, 295, 297: Navigation flow debugging
  - **Severity**: TEMPORARY DEBUG
  - **Rationale**: These appear to be temporary debugging statements ("About to...", "...completed")
  - **Keep or Remove**: REMOVE (temporary debug statements)

**ImagePickerService.cs** (9 Console.WriteLine):
- Lines 21, 33, 38, 41, 46: Photo picking flow tracing
  - **Severity**: Debug
  - **Rationale**: Detailed diagnostic information for troubleshooting image picker issues
  - **Keep or Remove**: KEEP at Debug level (helpful for debugging platform-specific behavior)

- Line 51: Permission denied error
  - **Severity**: Warning
  - **Rationale**: User denied permission (expected scenario), not a system error but worth noting
  - **Keep or Remove**: KEEP at Warning level

- Lines 57, 58: Unexpected exceptions
  - **Severity**: Error
  - **Rationale**: Unexpected error conditions with stack trace
  - **Keep or Remove**: KEEP at Error level

**ThemeService.cs** (7 Debug.WriteLine):
- Lines 37, 44, 60, 81, 87, 92, 97: Theme switching and system theme event handling
  - **Severity**: Debug
  - **Rationale**: Detailed diagnostic for understanding theme behavior
  - **Keep or Remove**: KEEP at Debug level (useful for debugging theme issues)

**ImageProcessingService.cs** (2 Console.WriteLine):
- Line 37: Image validation error
  - **Severity**: Error
  - **Rationale**: Image validation failure
  - **Keep or Remove**: KEEP at Error level

- Line 63: Image saved confirmation
  - **Severity**: Debug (or Information if tracking storage is important)
  - **Rationale**: Successful operation with file path and size
  - **Keep or Remove**: KEEP at Debug level

**UserProfileService.cs** (0 found in grep, listed in initial analysis):
- **Status**: File exists but no WriteLine statements found in current search
- **Action**: Verify file manually during implementation

**Summary Statistics**:
- **Remove**: 4 temporary debug statements (ShotLoggingPage.cs lines 291, 293, 295, 297)
- **Error level**: 5 statements (ProfileImagePicker.cs x2, ShotLoggingPage.cs x1, ImagePickerService.cs x2, ImageProcessingService.cs x1)
- **Warning level**: 1 statement (ImagePickerService.cs permission denied)
- **Information level**: 1 statement (MauiProgram.cs database path - subject to circular dependency review)
- **Debug level**: 18 statements (remaining diagnostic information)

**Decision**: Migrate 25 statements (4 removed as temporary), categorize by severity as documented above.

---

### Q3: What are structured logging message template best practices?

**Research Sources**: Microsoft.Extensions.Logging documentation, ASP.NET Core logging guidance

**Findings**:

**Message Template Syntax**:
```csharp
// ✅ GOOD: Named parameters in curly braces
_logger.LogDebug("Loading best shot for bag {BagId}", bagId);

// ❌ BAD: String interpolation loses structured data
_logger.LogDebug($"Loading best shot for bag {bagId}");

// ✅ GOOD: Multiple parameters
_logger.LogInformation("Shot saved: DoseIn={DoseIn}g, Output={Output}g, Time={Time}s", 
    shot.DoseIn, shot.ExpectedOutput, shot.ExpectedTime);
```

**Parameter Naming Conventions**:
- Use PascalCase for parameter names (e.g., `{BagId}`, not `{bagid}` or `{bag_id}`)
- Match variable names when possible for clarity
- Avoid special characters except underscores
- Keep names concise but descriptive

**Sensitive Data Prevention**:
```csharp
// ❌ BAD: Logging sensitive data
_logger.LogInformation("API key: {ApiKey}", apiKey);

// ✅ GOOD: Log only non-sensitive metadata
_logger.LogInformation("API call completed in {ElapsedMs}ms", elapsed);

// ✅ GOOD: Use data sanitization
_logger.LogInformation("User {UserId} logged in from {IpAddressPrefix}.xxx.xxx", 
    userId, ipAddress.Substring(0, ipAddress.IndexOf('.')));
```

**Exception Logging**:
```csharp
// ✅ GOOD: Exception as first parameter (framework extracts stack trace)
_logger.LogError(ex, "Failed to load shot settings for bag {BagId}", bagId);

// ❌ BAD: Exception in message template (loses structured exception data)
_logger.LogError("Error: {Message}", ex.Message);

// ✅ GOOD: Add context to exception logging
_logger.LogError(ex, "Image validation failed for file {FileName}, size {FileSize} bytes", 
    fileName, fileSize);
```

**Migration Examples**:

```csharp
// BEFORE:
Console.WriteLine($"ImagePickerService: Opening stream for {fileResult.FileName}");

// AFTER:
_logger.LogDebug("Opening stream for file {FileName}", fileResult.FileName);

// BEFORE:
System.Diagnostics.Debug.WriteLine($"[ShotLoggingPage] Found best shot: DoseIn={bestShot.DoseIn}, GrindSetting={bestShot.GrindSetting}");

// AFTER:
_logger.LogDebug("Found best shot: DoseIn={DoseIn}g, GrindSetting={GrindSetting}, ExpectedOutput={ExpectedOutput}g, ExpectedTime={ExpectedTime}s",
    bestShot.DoseIn, bestShot.GrindSetting, bestShot.ExpectedOutput, bestShot.ExpectedTime);

// BEFORE:
Console.WriteLine($"ImagePickerService: Error picking image - {ex.Message}");
Console.WriteLine($"ImagePickerService: Stack trace - {ex.StackTrace}");

// AFTER:
_logger.LogError(ex, "Error picking image");
```

**Decision**: Use message templates with named parameters (PascalCase), log exceptions with LogError(exception, message) pattern, avoid sensitive data, remove service name prefixes (logger category provides this).

---

### Q4: How should logging be configured for different environments?

**Research Sources**: Microsoft.Extensions.Logging configuration documentation, .NET MAUI appsettings.json patterns

**Findings**:

**Configuration Location**:
- Primary: `appsettings.json` in project root (embedded resource)
- Environment-specific: `appsettings.Development.json` (already exists for OpenAI config)
- Shiny.Extensions.Configuration already loads both files based on DEBUG conditional compilation

**Configuration Schema**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "BaristaNotes": "Debug",
      "BaristaNotes.Services": "Debug",
      "BaristaNotes.Pages": "Debug",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "OpenAI": {
    "ApiKey": "..."
  }
}
```

**Log Level Hierarchy**:
- Most specific namespace wins (e.g., `BaristaNotes.Services.ThemeService` > `BaristaNotes.Services` > `BaristaNotes` > `Default`)
- Common practice: Set Default to Information, enable Debug for application namespaces
- Framework namespaces (Microsoft, System) typically set to Warning to reduce noise

**Recommended Configurations**:

**Development (appsettings.Development.json)**:
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

**Production (appsettings.json)**:
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

**Runtime Override** (optional, out of scope for initial migration):
- Can be done through code in MauiProgram.cs:
```csharp
builder.Logging.SetMinimumLevel(LogLevel.Debug);
builder.Logging.AddFilter("BaristaNotes", LogLevel.Debug);
```

**Integration with Existing Configuration**:
- Shiny loads both appsettings.json and appsettings.Development.json via `AddJsonPlatformBundle()`
- Logging configuration automatically read by Microsoft.Extensions.Logging framework
- No additional code needed in MauiProgram.cs (framework handles it)

**Decision**: Add Logging section to existing appsettings.json (Information level for production), add Logging section to appsettings.Development.json (Debug level for development), leverage existing Shiny configuration loading.

---

### Q5: How should services be refactored to receive ILogger<T>?

**Research Sources**: Existing AIAdviceService.cs implementation (reference pattern), dependency injection best practices

**Findings**:

**Constructor Injection Pattern** (preferred):
```csharp
public class ThemeService
{
    private readonly ILogger<ThemeService> _logger;
    private readonly IPreferences _preferences;

    public ThemeService(ILogger<ThemeService> logger, IPreferences preferences)
    {
        _logger = logger;
        _preferences = preferences;
    }

    public void SetThemeMode(ThemeMode mode)
    {
        _logger.LogDebug("SetThemeMode called with mode: {Mode}", mode);
        // ... implementation
    }
}
```

**Service Registration** (verify existing in MauiProgram.cs):
- Services already registered as singletons/transients in MauiProgram.cs
- ILogger<T> automatically injected by MAUI's dependency injection container
- No changes to service registration needed (DI container resolves ILogger<T> automatically)

**MauiReactor Components** (special case):
- MauiReactor components don't use constructor injection (they're not services)
- Options:
  1. Pass ILogger<T> as parameter from page/component that has DI access
  2. Use `Application.Current.Handler.MauiContext.Services.GetService<ILogger<T>>()` (service locator anti-pattern)
  3. Extract logic to service with proper DI
- **Decision for ProfileImagePicker**: It's a UI component, extract image picking logic to existing ImagePickerService (which can have ILogger injected properly)

**ShotLoggingPage** (MauiReactor page component):
- Pages in MauiReactor don't have constructor injection
- Options:
  1. Keep Debug.WriteLine for UI flow debugging (acceptable for temporary debugging)
  2. Extract business logic to services with proper DI
  3. Use service locator (not recommended)
- **Decision**: Keep minimal Debug.WriteLine in page component for UI flow debugging, migrate business logic logging to services

**Static Methods / Utilities**:
- If utility has static methods with logging needs:
  1. Refactor to instance methods with ILogger<T> injection (preferred)
  2. Accept ILogger as method parameter
  3. Use ILoggerFactory if truly static (requires passing factory)
- **Current Status**: No static logging scenarios identified in migration scope

**Circular Dependency Handling**:
- MauiProgram.cs database path logging occurs during DI setup (line 111)
- ILogger<T> not available yet during DI container build phase
- **Decision**: Keep this specific Console.WriteLine, migrate only after DI container is built

**Migration Strategy by File**:
1. **Services (UserProfileService, ImagePickerService, ThemeService, ImageProcessingService)**:
   - Add `ILogger<T>` constructor parameter
   - Update service registration if needed (verify auto-injection works)
   - Replace WriteLine with _logger methods

2. **ProfileImagePicker Component**:
   - Remove inline error logging (it's UI component)
   - Let ImagePickerService handle error logging (service layer)
   - Component can show error UI based on service return value

3. **ShotLoggingPage**:
   - Keep minimal UI flow debugging as Debug.WriteLine
   - Consider extracting shot loading logic to service in future (out of scope)
   - Migrate business logic logging (error handling) to Information/Error levels

4. **MauiProgram.cs**:
   - Keep database path logging as Console.WriteLine (circular dependency during bootstrap)
   - Document reason in code comment

**Decision**: Use constructor injection for services, extract component logic to services where possible, keep minimal Debug.WriteLine in UI components for flow debugging, document bootstrap logging exception.

---

### Q6: How should unit tests mock ILogger<T>?

**Research Sources**: Moq documentation, xUnit best practices, Microsoft.Extensions.Logging.Abstractions

**Findings**:

**Moq Pattern for ILogger<T>**:
```csharp
using Moq;
using Microsoft.Extensions.Logging;
using Xunit;

public class ThemeServiceTests
{
    [Fact]
    public void SetThemeMode_LogsDebugMessage()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ThemeService>>();
        var mockPreferences = new Mock<IPreferences>();
        var service = new ThemeService(mockLogger.Object, mockPreferences.Object);

        // Act
        service.SetThemeMode(ThemeMode.Dark);

        // Assert - Verify LogDebug was called
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("SetThemeMode called")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }
}
```

**Simpler Pattern - Just Verify Logger Was Injected**:
```csharp
public class ThemeServiceTests
{
    [Fact]
    public void SetThemeMode_DoesNotThrow()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ThemeService>>();
        var mockPreferences = new Mock<IPreferences>();
        var service = new ThemeService(mockLogger.Object, mockPreferences.Object);

        // Act & Assert - No exception thrown
        service.SetThemeMode(ThemeMode.Dark);
    }
}
```

**Helper Factory for Tests**:
```csharp
public static class MockLoggerFactory
{
    public static ILogger<T> Create<T>()
    {
        return new Mock<ILogger<T>>().Object;
    }

    public static Mock<ILogger<T>> CreateMock<T>()
    {
        return new Mock<ILogger<T>>();
    }
}

// Usage in tests
var service = new ThemeService(MockLoggerFactory.Create<ThemeService>(), mockPreferences.Object);
```

**Testing Philosophy**:
- **Option 1**: Don't verify log content in tests (logging is infrastructure, not business logic)
  - Tests focus on behavior, not logging side effects
  - Simpler tests, less brittle
  - Risk: Log statements could be incorrect and tests wouldn't catch it

- **Option 2**: Verify critical log statements (errors, security events)
  - Tests verify error logging occurs for exception paths
  - More complex test setup with Moq verification
  - Benefit: Ensures critical diagnostics are captured

**Decision**: Use Option 1 (don't verify log content) for most tests, use Option 2 (verify logging) only for critical error paths where logging is part of the requirement (e.g., FR-011: Error logging MUST include exceptions). Create MockLoggerFactory helper to reduce test boilerplate.

**Test Update Strategy**:
1. Add `ILogger<T>` parameter to service constructors in tests
2. Use `MockLoggerFactory.Create<T>()` to provide mock logger
3. Verify no tests break (if they do, it indicates missing DI setup)
4. Optionally add explicit tests for error logging on exception paths

---

## Migration Strategy Summary

### Files to Migrate (in order)

1. **BaristaNotes/Services/ThemeService.cs** (7 statements → 7 Debug)
   - Low risk, pure diagnostic logging
   - Constructor injection straightforward
   - Good first migration for pattern establishment

2. **BaristaNotes/Services/ImageProcessingService.cs** (2 statements → 1 Error, 1 Debug)
   - Simple service with error handling
   - Demonstrates error logging pattern

3. **BaristaNotes/Services/ImagePickerService.cs** (9 statements → 2 Error, 1 Warning, 6 Debug)
   - More complex with multiple severity levels
   - Good example for severity categorization

4. **BaristaNotes/Components/ProfileImagePicker.cs** (2 statements → REMOVE)
   - Extract error logging to ImagePickerService
   - Demonstrates component → service refactoring

5. **BaristaNotes/Pages/ShotLoggingPage.cs** (8 statements → REMOVE 4, keep 4 as Debug/Error)
   - Mixed temporary and real logging
   - Demonstrates cleanup strategy

6. **BaristaNotes/MauiProgram.cs** (1 statement → KEEP as Console.WriteLine with comment)
   - Bootstrap logging exception
   - Demonstrates circular dependency handling

### Configuration Files to Update

1. **BaristaNotes/appsettings.json** - Add Logging section (Information level)
2. **BaristaNotes/appsettings.Development.json** - Add Logging section (Debug level)

### Test Files to Update

1. **BaristaNotes.Tests/Helpers/MockLoggerFactory.cs** - Create helper for mock logger creation
2. **All service tests** - Add ILogger<T> mock to service constructors

### Validation Steps

1. Build solution - verify no compilation errors
2. Run all tests - verify test coverage maintained
3. Run application in DEBUG - verify Debug statements appear
4. Configure Information level - verify Debug statements suppressed
5. Trigger error scenarios - verify error logging works
6. Search for Debug.WriteLine/Console.WriteLine - verify all migrated (except MauiProgram.cs bootstrap)

---

## Technical Decisions Summary

| Decision Point | Choice | Rationale |
|----------------|--------|-----------|
| Logging framework | Microsoft.Extensions.Logging.Debug | Already installed, MAUI built-in support, meets requirements |
| Severity mapping | See Q2 findings table | Based on context analysis of each statement |
| Message templates | Named parameters, PascalCase | Structured logging best practices, enables filtering |
| Configuration | appsettings.json + appsettings.Development.json | Existing infrastructure, Shiny already loads |
| Constructor injection | Yes for services | Standard DI pattern, testable, maintainable |
| Component logging | Extract to services or keep Debug.WriteLine | MauiReactor components lack DI support |
| Bootstrap logging | Keep as Console.WriteLine | Circular dependency during DI setup |
| Test mocking | MockLoggerFactory helper, don't verify content | Simple, maintainable tests focused on behavior |
| Temporary statements | Remove (4 in ShotLoggingPage) | Not needed for production diagnostics |

---

## Success Criteria Validation Approach

- **SC-001 (Zero Debug.WriteLine)**: `grep -r "Debug\.WriteLine" --include="*.cs" | wc -l` = 0 (except MauiProgram.cs)
- **SC-002 (Zero Console.WriteLine)**: `grep -r "Console\.WriteLine" --include="*.cs" | wc -l` = 1 (MauiProgram.cs only)
- **SC-003 (10-minute debugging)**: Manual test with error scenario, measure time to identify root cause
- **SC-004 (100% ILogger adoption)**: Code review checklist, verify all migrated services use ILogger<T>
- **SC-005 (80% log volume reduction)**: Count log statements in Debug vs Information mode
- **SC-006 (100% pattern follow-through)**: Static analysis in CI, code review guidelines
- **SC-007 (No performance impact)**: Profile app startup and common operations before/after
- **SC-008 (Zero sensitive data)**: Manual audit of all log messages for API keys, passwords, PII

---

## Ready for Phase 1

All research questions resolved. No remaining NEEDS CLARIFICATION items. Ready to proceed to data model design and contracts generation.

**Next Phase**: Create data-model.md (logger categories, severity mappings), contracts/logging-config-schema.json, quickstart.md (developer reference guide).
