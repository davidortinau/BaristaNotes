# Quickstart: Structured Logging with Microsoft.Extensions.Logging

**Purpose**: Quick reference guide for developers working with logging in BaristaNotes  
**Audience**: Developers adding new logging statements or migrating existing Debug/Console.WriteLine calls

## TL;DR - Copy-Paste Patterns

### Pattern 1: Add logging to a new service

```csharp
public class MyNewService
{
    private readonly ILogger<MyNewService> _logger;
    private readonly ISomeDependency _dependency;

    public MyNewService(
        ILogger<MyNewService> logger,
        ISomeDependency dependency)
    {
        _logger = logger;
        _dependency = dependency;
    }

    public async Task DoSomethingAsync(int itemId)
    {
        _logger.LogDebug("DoSomethingAsync called with itemId: {ItemId}", itemId);
        
        try
        {
            // ... business logic
            _logger.LogInformation("Successfully processed item {ItemId}", itemId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process item {ItemId}", itemId);
            throw;
        }
    }
}
```

### Pattern 2: Message templates with multiple parameters

```csharp
// ✅ GOOD: Named parameters in curly braces
_logger.LogDebug("Shot logged: DoseIn={DoseIn}g, Output={Output}g, Time={Time}s, Rating={Rating}/4",
    shot.DoseIn, shot.ExpectedOutput, shot.ExpectedTime, shot.Rating);

// ❌ BAD: String interpolation (loses structured data)
_logger.LogDebug($"Shot logged: DoseIn={shot.DoseIn}g, Output={shot.ExpectedOutput}g");
```

### Pattern 3: Exception logging

```csharp
// ✅ GOOD: Exception as first parameter
_logger.LogError(ex, "Failed to save image for user {UserId}", userId);

// ❌ BAD: Exception message in template (loses stack trace)
_logger.LogError("Error: {Message}", ex.Message);
```

### Pattern 4: Conditional logging (optional performance optimization)

```csharp
// Only format expensive string if Debug level enabled
if (_logger.IsEnabled(LogLevel.Debug))
{
    var expensiveData = GenerateExpensiveDebugInfo();
    _logger.LogDebug("Debug data: {Data}", expensiveData);
}
```

## When to Use Each Log Level

| Level | When to Use | Examples |
|-------|-------------|----------|
| **Trace** | Extremely detailed diagnostics, rarely used | Loop iterations, every property change |
| **Debug** | Detailed diagnostic information for troubleshooting | Method entry/exit, parameter values, query results |
| **Information** | Significant application events, important milestones | Application started, request completed, user logged in |
| **Warning** | Unexpected but recoverable conditions | Permission denied, using fallback, deprecated API used |
| **Error** | Error conditions that prevent operation from completing | Exception caught, validation failed, operation aborted |
| **Critical** | Critical failures threatening system stability | Database unavailable, out of memory, unrecoverable error |

### Level Selection Guide

```csharp
// User action succeeded → Information
_logger.LogInformation("Shot {ShotId} created successfully by user {UserId}", shotId, userId);

// Diagnostic detail (hidden in production) → Debug
_logger.LogDebug("Loading best shot for bag {BagId}, found {Count} rated shots", bagId, count);

// Expected user error (permission, validation) → Warning
_logger.LogWarning("User {UserId} denied photo permission", userId);

// Unexpected error (exception caught) → Error
_logger.LogError(ex, "Failed to process image {FileName}", fileName);

// System failure (rare) → Critical
_logger.LogCritical(ex, "Database connection failed, application cannot continue");
```

## Migration Checklist

Migrating from Debug.WriteLine or Console.WriteLine?

### Step 1: Identify severity level
- [ ] Is this temporary debug code? → **Remove it**
- [ ] Is this diagnostic info? → **Debug**
- [ ] Is this a significant event? → **Information**
- [ ] Is this a recoverable issue? → **Warning**
- [ ] Is this an error/exception? → **Error**

### Step 2: Add ILogger to constructor

```csharp
// BEFORE:
public MyService(ISomeDependency dependency)
{
    _dependency = dependency;
}

// AFTER:
public MyService(
    ILogger<MyService> logger,  // ← Add this
    ISomeDependency dependency)
{
    _logger = logger;           // ← Store in field
    _dependency = dependency;
}

// Add private field at top of class:
private readonly ILogger<MyService> _logger;
```

### Step 3: Convert WriteLine to LogXXX

```csharp
// BEFORE:
Debug.WriteLine($"[MyService] Processing item {itemId}");

// AFTER:
_logger.LogDebug("Processing item {ItemId}", itemId);
// Note: Service name prefix not needed (logger category provides this)
// Note: Use message template, not string interpolation
```

### Step 4: Handle exceptions

```csharp
// BEFORE:
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
}

// AFTER:
catch (Exception ex)
{
    _logger.LogError(ex, "Operation failed");
    // Framework automatically includes exception details and stack trace
}
```

## Configuration Examples

### Development (appsettings.Development.json)

Show all Debug-level logging for BaristaNotes classes:

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

### Production (appsettings.json)

Show only Information and above for BaristaNotes:

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

### Troubleshooting Specific Service

Enable Debug logging only for ThemeService:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "BaristaNotes": "Information",
      "BaristaNotes.Services.ThemeService": "Debug",
      "Microsoft": "Warning"
    }
  }
}
```

## Common Mistakes to Avoid

### ❌ String interpolation in log message

```csharp
// BAD: Loses structured data, always formats string
_logger.LogDebug($"Processing {count} items");

// GOOD: Structured data, deferred formatting
_logger.LogDebug("Processing {Count} items", count);
```

### ❌ Logging sensitive data

```csharp
// BAD: Exposes API key in logs
_logger.LogInformation("API key: {ApiKey}", apiKey);

// GOOD: Log metadata only
_logger.LogInformation("API call completed in {ElapsedMs}ms", elapsed);
```

### ❌ Using wrong severity level

```csharp
// BAD: Using Error for expected user action
_logger.LogError("User cancelled image selection");

// GOOD: User cancellation is informational or debug
_logger.LogDebug("User cancelled image selection");
```

### ❌ Logging in loops without guards

```csharp
// BAD: Logs 10,000 times in production
foreach (var item in hugeList)
{
    _logger.LogInformation("Processing {Item}", item);
}

// GOOD: Use Debug level (suppressed in production) or guard
foreach (var item in hugeList)
{
    _logger.LogDebug("Processing {Item}", item);
}
// OR
if (_logger.IsEnabled(LogLevel.Debug))
{
    foreach (var item in hugeList)
    {
        _logger.LogDebug("Processing {Item}", item);
    }
}
```

### ❌ Missing exception parameter

```csharp
// BAD: Loses stack trace and exception details
_logger.LogError("Error: {Message}", ex.Message);

// GOOD: Framework captures full exception
_logger.LogError(ex, "Operation failed");
```

## Testing with Mock Loggers

### Unit Test Pattern

```csharp
using Moq;
using Microsoft.Extensions.Logging;
using Xunit;

public class MyServiceTests
{
    [Fact]
    public void DoSomething_CompletesSuccessfully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<MyService>>();
        var mockDependency = new Mock<ISomeDependency>();
        var service = new MyService(mockLogger.Object, mockDependency.Object);

        // Act
        service.DoSomething();

        // Assert
        // Service logic assertions...
        // Optional: Verify logging occurred (usually not necessary)
    }
}
```

### Helper Factory for Tests

```csharp
// BaristaNotes.Tests/Helpers/MockLoggerFactory.cs
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

// Usage in tests:
var service = new MyService(MockLoggerFactory.Create<MyService>(), mockDep.Object);
```

## Viewing Logs

### Visual Studio (Windows/Mac)
- Debug window: View → Output → Show output from: Debug
- Filter by category: Search for service name (e.g., "ThemeService")

### Visual Studio Code
- Debug Console panel during debugging session
- Look for formatted log entries with timestamps

### Device Logs (iOS/Android)
- iOS: Xcode → Window → Devices and Simulators → Console
- Android: Android Studio → Logcat (filter by package name)

## Quick Reference

**Add logging to service**: `ILogger<T>` constructor parameter → store in `private readonly ILogger<T> _logger` field  
**Log debug info**: `_logger.LogDebug("Message {Param}", value)`  
**Log significant event**: `_logger.LogInformation("Event {Param}", value)`  
**Log warning**: `_logger.LogWarning("Unexpected condition {Param}", value)`  
**Log error**: `_logger.LogError(ex, "Operation failed")`  
**Check if enabled**: `if (_logger.IsEnabled(LogLevel.Debug)) { ... }`  
**Configure log level**: Edit `appsettings.json` or `appsettings.Development.json`

---

**For complete details**: See [data-model.md](data-model.md) for severity mappings and [research.md](research.md) for architectural decisions.
