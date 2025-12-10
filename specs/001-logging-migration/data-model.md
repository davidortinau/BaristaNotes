# Data Model: Structured Logging Architecture

**Purpose**: Define logger categories, severity mappings, and configuration structure for logging migration  
**Date**: 2025-12-10

## Logger Categories

Logger categories follow the full type name pattern `ILogger<T>` where T is the service/component class.

### Service Layer Categories

| Service Class | Logger Category | Registration | Logging Scope |
|---------------|-----------------|--------------|---------------|
| ThemeService | `BaristaNotes.Services.ThemeService` | Singleton | Theme mode changes, system theme events |
| ImageProcessingService | `BaristaNotes.Services.ImageProcessingService` | Singleton | Image validation, save operations |
| ImagePickerService | `BaristaNotes.Services.ImagePickerService` | Singleton | Photo picking flow, permissions, errors |
| UserProfileService | `BaristaNotes.Core.Services.UserProfileService` | Singleton | Profile operations (if logging added) |

### Page/Component Categories

| Component Class | Logger Category | Note |
|-----------------|-----------------|------|
| ShotLoggingPage | `BaristaNotes.Pages.ShotLoggingPage` | Limited logging (UI flow only) |
| ProfileImagePicker | N/A | Logging moved to ImagePickerService |

### Bootstrap Category

| Location | Logger Category | Note |
|----------|-----------------|------|
| MauiProgram.cs | N/A | Keep Console.WriteLine for bootstrap (circular dependency) |

## Severity Level Mappings

### Log Level Definitions

| Level | Numeric Value | Usage | Example |
|-------|---------------|-------|---------|
| Trace | 0 | Very detailed diagnostic (rarely used) | Loop iterations, property changes |
| Debug | 1 | Detailed diagnostic information | Method entry/exit, parameter values |
| Information | 2 | Significant application events | Application started, request processed |
| Warning | 3 | Unexpected but recoverable conditions | Permission denied, fallback used |
| Error | 4 | Error conditions, operation failed | Exception caught, operation aborted |
| Critical | 5 | Critical failures, system-threatening | Database unavailable, out of memory |

### Migration Mapping by File

#### ThemeService.cs (7 statements)

| Line | Original Statement | New Severity | Message Template |
|------|-------------------|--------------|------------------|
| 37 | `Debug.WriteLine($"[ThemeService] Loaded saved theme mode: {_currentMode}")` | Debug | `"Loaded saved theme mode: {ThemeMode}"` |
| 44 | `Debug.WriteLine($"[ThemeService] Subscribed to RequestedThemeChanged event")` | Debug | `"Subscribed to RequestedThemeChanged event"` |
| 60 | `Debug.WriteLine($"[ThemeService] SetThemeModeAsync called with mode: {mode}")` | Debug | `"SetThemeModeAsync called with mode: {Mode}"` |
| 81 | `Debug.WriteLine($"[ThemeService] ApplyTheme: CurrentMode={_currentMode}, TargetTheme={targetTheme}, SystemTheme={Application.Current.RequestedTheme}")` | Debug | `"ApplyTheme: CurrentMode={CurrentMode}, TargetTheme={TargetTheme}, SystemTheme={SystemTheme}"` |
| 87 | `Debug.WriteLine($"[ThemeService] OnSystemThemeChanged fired: NewTheme={e.RequestedTheme}, CurrentMode={_currentMode}")` | Debug | `"OnSystemThemeChanged fired: NewTheme={NewTheme}, CurrentMode={CurrentMode}"` |
| 92 | `Debug.WriteLine($"[ThemeService] Applying theme because CurrentMode is System")` | Debug | `"Applying theme because CurrentMode is System"` |
| 97 | `Debug.WriteLine($"[ThemeService] Ignoring system theme change because CurrentMode is {_currentMode}")` | Debug | `"Ignoring system theme change because CurrentMode is {CurrentMode}"` |

#### ImageProcessingService.cs (2 statements)

| Line | Original Statement | New Severity | Message Template |
|------|-------------------|--------------|------------------|
| 37 | `Console.WriteLine($"Image validation error: {ex.Message}")` | Error | `"Image validation error"` (with exception parameter) |
| 63 | `Console.WriteLine($"Image saved to: {path}, size: {memoryStream.Length} bytes")` | Debug | `"Image saved to: {Path}, size: {SizeBytes} bytes"` |

#### ImagePickerService.cs (9 statements)

| Line | Original Statement | New Severity | Message Template |
|------|-------------------|--------------|------------------|
| 21 | `Console.WriteLine("ImagePickerService: Starting photo pick...")` | Debug | `"Starting photo pick"` |
| 33 | `Console.WriteLine($"ImagePickerService: PickPhotosAsync returned, results count: {results?.Count ?? 0}")` | Debug | `"PickPhotosAsync returned, results count: {ResultCount}"` |
| 38 | `Console.WriteLine($"ImagePickerService: Opening stream for {fileResult.FileName}")` | Debug | `"Opening stream for file {FileName}"` |
| 41 | `Console.WriteLine($"ImagePickerService: Stream opened, CanRead: {stream.CanRead}, CanSeek: {stream.CanSeek}, Length: {(stream.CanSeek ? stream.Length : -1)}")` | Debug | `"Stream opened, CanRead: {CanRead}, CanSeek: {CanSeek}, Length: {Length}"` |
| 46 | `Console.WriteLine("ImagePickerService: No results, user cancelled")` | Debug | `"No results, user cancelled"` |
| 51 | `Console.WriteLine($"ImagePickerService: Permission denied - {ex.Message}")` | Warning | `"Permission denied"` (with exception parameter) |
| 57 | `Console.WriteLine($"ImagePickerService: Error picking image - {ex.Message}")` | Error | `"Error picking image"` (with exception parameter) |
| 58 | `Console.WriteLine($"ImagePickerService: Stack trace - {ex.StackTrace}")` | REMOVE | (Combined into line 57 via exception parameter) |

#### ProfileImagePicker.cs (2 statements)

| Line | Original Statement | Action | Rationale |
|------|-------------------|--------|-----------|
| 128 | `Console.WriteLine($"Error: {ex.Message}")` | REMOVE | Move error handling to ImagePickerService |
| 163 | `Console.WriteLine($"Error: {ex.Message}")` | REMOVE | Move error handling to ImagePickerService |

#### ShotLoggingPage.cs (8 statements)

| Line | Original Statement | New Severity | Message Template |
|------|-------------------|--------------|------------------|
| 229 | `Debug.WriteLine($"[ShotLoggingPage] LoadBestShotSettingsAsync called for bagId: {bagId}")` | Debug | `"LoadBestShotSettingsAsync called for bagId: {BagId}"` |
| 234 | `Debug.WriteLine($"[ShotLoggingPage] Found best shot: DoseIn={bestShot.DoseIn}, GrindSetting={bestShot.GrindSetting}, ExpectedOutput={bestShot.ExpectedOutput}, ExpectedTime={bestShot.ExpectedTime}")` | Debug | `"Found best shot: DoseIn={DoseIn}g, GrindSetting={GrindSetting}, ExpectedOutput={ExpectedOutput}g, ExpectedTime={ExpectedTime}s"` |
| 246 | `Debug.WriteLine($"[ShotLoggingPage] No rated shots found for bagId: {bagId}")` | Debug | `"No rated shots found for bagId: {BagId}"` |
| 252 | `Debug.WriteLine($"[ShotLoggingPage] Error loading best shot settings: {ex.Message}")` | Error | `"Error loading best shot settings"` (with exception parameter) |
| 291 | `Debug.WriteLine("[ShotLoggingPage] About to call ShowSuccessAsync")` | REMOVE | Temporary debugging statement |
| 293 | `Debug.WriteLine("[ShotLoggingPage] ShowSuccessAsync completed")` | REMOVE | Temporary debugging statement |
| 295 | `Debug.WriteLine("[ShotLoggingPage] About to navigate back")` | REMOVE | Temporary debugging statement |
| 297 | `Debug.WriteLine("[ShotLoggingPage] Navigation completed")` | REMOVE | Temporary debugging statement |

#### MauiProgram.cs (1 statement)

| Line | Original Statement | Action | Rationale |
|------|-------------------|--------|-----------|
| 111 | `Console.WriteLine($"Database path: {dbPath}")` | KEEP as Console.WriteLine | Bootstrap logging during DI setup (circular dependency) |

### Summary Statistics

- **Total statements**: 29
- **Remove**: 6 (ProfileImagePicker x2, ShotLoggingPage temporary x4)
- **Keep as Console.WriteLine**: 1 (MauiProgram.cs bootstrap)
- **Migrate to structured logging**: 22
  - Error level: 4
  - Warning level: 1
  - Debug level: 17

## Configuration Schema

### Logging Configuration Structure

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
  }
}
```

### Configuration Hierarchy

```
Default (global baseline)
  └─ BaristaNotes (application root namespace)
      ├─ BaristaNotes.Services (all services)
      │   ├─ BaristaNotes.Services.ThemeService
      │   ├─ BaristaNotes.Services.ImageProcessingService
      │   └─ BaristaNotes.Services.ImagePickerService
      └─ BaristaNotes.Pages (all pages)
          └─ BaristaNotes.Pages.ShotLoggingPage
```

### Environment-Specific Configurations

**Development** (`appsettings.Development.json`):
- Default: Debug (show all application logging)
- BaristaNotes: Debug (full diagnostic information)
- Microsoft: Warning (suppress framework noise)

**Production** (`appsettings.json`):
- Default: Information (significant events only)
- BaristaNotes: Information (hide Debug statements)
- Microsoft: Warning (errors and warnings only)

## Constructor Injection Pattern

### Service Template

```csharp
public class ExampleService
{
    private readonly ILogger<ExampleService> _logger;
    private readonly IOtherDependency _dependency;

    public ExampleService(
        ILogger<ExampleService> logger,
        IOtherDependency dependency)
    {
        _logger = logger;
        _dependency = dependency;
    }

    public void DoSomething()
    {
        _logger.LogDebug("DoSomething called");
        try
        {
            // ... logic
            _logger.LogInformation("Operation completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operation failed");
            throw;
        }
    }
}
```

### Registration (no changes needed)

```csharp
// In MauiProgram.cs
builder.Services.AddSingleton<ExampleService>();

// ILogger<ExampleService> automatically resolved by DI container
```

## Validation Rules

### Message Template Rules

1. **Named parameters**: Use `{ParameterName}` not `$"{variable}"`
2. **PascalCase**: Parameter names in PascalCase (e.g., `{BagId}` not `{bagId}`)
3. **No sensitive data**: Never log API keys, passwords, PII without sanitization
4. **Exception parameter**: Use `LogError(exception, message)` not `LogError($"Error: {exception.Message}")`

### Configuration Rules

1. **Valid log levels**: Trace, Debug, Information, Warning, Error, Critical (case-insensitive)
2. **Namespace specificity**: Most specific namespace wins in hierarchy
3. **Default baseline**: Always set a Default level as fallback
4. **Framework noise**: Set Microsoft namespace to Warning or higher

### Code Quality Rules

1. **Constructor injection**: All services must receive `ILogger<T>` via constructor
2. **Private readonly field**: Store logger in `private readonly ILogger<T> _logger` field
3. **No service locator**: Don't use `Services.GetService<ILogger<T>>()` anti-pattern
4. **No string interpolation**: Use message templates for structured data

## State Transitions

N/A - Logging is stateless infrastructure. No state machines or workflows.

## Relationships

```
MauiProgram.cs (DI Container)
    ↓ Registers Services
    ↓ Injects ILogger<T>
Services (ThemeService, ImagePickerService, etc.)
    ↓ Uses
ILogger<T> Interface
    ↓ Writes to
DebugLoggerProvider
    ↓ Outputs to
Debug Console / System Debug Log
```

## Implementation Order

1. **ThemeService** - Simple, pure diagnostic logging
2. **ImageProcessingService** - Error handling pattern
3. **ImagePickerService** - Multiple severity levels
4. **ProfileImagePicker** - Component refactoring (remove logging)
5. **ShotLoggingPage** - Cleanup temporary statements
6. **Configuration files** - Add Logging section
7. **Tests** - Update with mock loggers
8. **Validation** - Verify migration complete

---

**Ready for Contracts Generation**: Data model complete, ready to create JSON schema and quickstart guide.
