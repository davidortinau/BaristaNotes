# BaristaNotes Development Guidelines

Auto-generated from all feature plans. Last updated: 2025-12-02

## Active Technologies
- C# / .NET 10.0 + MauiReactor (Reactor.Maui 4.0.3-beta), Plugin.Maui.BottomSheet (NEW), CommunityToolkit.Maui 9.1.1, Entity Framework Core 8.0.0 (002-crud-settings-modals)
- SQLite (local database via EF Core) (002-crud-settings-modals)
- C# 12 / .NET 9 + .NET MAUI, MauiReactor (preview), UXDivers.Popups.Maui, CommunityToolkit.Maui (001-crud-feedback)
- SQLite with EntityFramework Core, CoreSync for offline-first sync (001-crud-feedback)
- C# 12, .NET 8.0 + Maui Reactor (preview), UXDivers.Popups.Maui, Microsoft.Maui.Controls, CommunityToolkit.Maui (001-crud-feedback)
- SQLite with Entity Framework Core (existing) (001-crud-feedback)
- C# .NET 10 (MAUI) (001-edit-delete-shots)
- C# / .NET 9.0 + .NET MAUI 9.0, MauiReactor 4.x, Entity Framework Core 9.x, CommunityToolkit.Maui, UXDivers.Popups (001-shot-tracking)
- SQLite via Entity Framework Core (ShotRecords, UserProfiles tables) (001-shot-tracking)
- C# 13 / .NET 10 + .NET MAUI 10.0, MauiReactor (Theme system), Microsoft.Maui.Graphics (Color APIs), Microsoft.Maui.Essentials (Preferences API for theme persistence) (002-coffee-theme)
- MAUI Preferences API for theme mode persistence (key-value storage in platform-specific secure storage) (002-coffee-theme)
- C# 12 / .NET 10.0 (003-profile-image-picker)
- C# 12, .NET 10.0 + MauiReactor (UI), UXDivers.Popups.Maui (feedback), Microsoft.EntityFrameworkCore (data) (004-bean-detail-page)
- SQLite via EF Core (existing infrastructure) (004-bean-detail-page)
- C# .NET 10.0 + .NET MAUI 10.0, Entity Framework Core 10.0, SQLite, Reactor.Maui 4.0.3-beta (001-bean-rating-tracking)
- SQLite database via EF Core (local, with CoreSync for future cloud sync) (001-bean-rating-tracking)

- C# / .NET 10.0 + MauiReactor (Reactor.Maui 4.0.3-beta), CommunityToolkit.Maui 9.1.1, Entity Framework Core 8.0.0 (002-crud-settings-modals)

## Project Structure

```text
src/
tests/
```

## Commands

# Add commands for C# / .NET 10.0

## Code Style

C# / .NET 10.0: Follow standard conventions

## MAUI UI Guidelines (MANDATORY)

### Deprecated APIs - DO NOT USE

The following APIs are deprecated and must NOT be used in new code:

- **Frame** - Use `Border` instead
- **ListView** - Use `CollectionView` instead
- **TableView** - Use `CollectionView` or custom layouts instead

### Rounded Corners - Use Border, NOT BoxView

**NEVER** use `BoxView` with `CornerRadius` for rounded container backgrounds. The corner radius gets distorted during device rotation (portrait ↔ landscape).

```csharp
// ❌ WRONG: BoxView corner radius distorts on rotation
BoxView()
    .BackgroundColor(backgroundColor)
    .HeightRequest(50)
    .CornerRadius(25)

// ✅ CORRECT: Border with RoundRectangle maintains proper corners
Border()
    .BackgroundColor(backgroundColor)
    .HeightRequest(50)
    .StrokeThickness(0)
    .StrokeShape(new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 25 })
```

### Orientation Rotation Handling

When UI elements need to respond to orientation changes, use `DeviceDisplay.MainDisplayInfoChanged` event:

```csharp
// In component state
private class State
{
    public DisplayOrientation Orientation { get; set; } = DisplayOrientation.Portrait;
}

// Subscribe in OnMounted
protected override void OnMounted()
{
    DeviceDisplay.MainDisplayInfoChanged += OnDisplayInfoChanged;
    SetState(s => s.Orientation = DeviceDisplay.MainDisplayInfo.Orientation);
    base.OnMounted();
}

// Unsubscribe in OnWillUnmount
protected override void OnWillUnmount()
{
    DeviceDisplay.MainDisplayInfoChanged -= OnDisplayInfoChanged;
    base.OnWillUnmount();
}

// Handler triggers state update to invalidate/re-render
private void OnDisplayInfoChanged(object? sender, DisplayInfoChangedEventArgs e)
{
    SetState(s => s.Orientation = e.DisplayInfo.Orientation);
}

// Use in Render() for orientation-specific layouts
public override VisualNode Render()
{
    var isLandscape = State.Orientation == DisplayOrientation.Landscape;
    // Adjust layout based on orientation...
}
```

## Logging Standards (MANDATORY)

**REQUIRED**: All services and components MUST use Microsoft.Extensions.Logging for diagnostic output.

### Core Rules

1. **No Debug.WriteLine or Console.WriteLine** in new code (except MauiProgram.cs bootstrap only)
2. **ILogger<T> injection** required for all new services via constructor dependency injection
3. **Message templates** with named parameters (PascalCase) - NO string interpolation
4. **Appropriate severity levels**: Debug (diagnostics), Information (significant events), Warning (recoverable issues), Error (failures with exceptions)

### Pattern Examples

```csharp
// ✅ CORRECT: Constructor injection + message template
public class MyService
{
    private readonly ILogger<MyService> _logger;
    
    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;
    }
    
    public async Task ProcessAsync(string bagId)
    {
        _logger.LogDebug("Processing bagId: {BagId}", bagId);
        try 
        {
            // work
            _logger.LogInformation("Processed {Count} items for bagId: {BagId}", count, bagId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing bagId: {BagId}", bagId);
            throw;
        }
    }
}

// ❌ WRONG: Debug.WriteLine
Debug.WriteLine($"Processing {bagId}"); // NEVER DO THIS

// ❌ WRONG: Console.WriteLine  
Console.WriteLine("Error: " + ex.Message); // NEVER DO THIS

// ❌ WRONG: String interpolation in log message
_logger.LogDebug($"Processing {bagId}"); // NEVER DO THIS
```

### Configuration

- **Development**: Debug level enabled (appsettings.Development.json)
- **Production**: Information level minimum (appsettings.json)
- **Per-service overrides**: Supported via appsettings Logging.LogLevel section

### Reference

See `specs/001-logging-migration/quickstart.md` for complete patterns and examples.

## Recent Changes
- 001-bean-rating-tracking: Added C# .NET 10.0 + .NET MAUI 10.0, Entity Framework Core 10.0, SQLite, Reactor.Maui 4.0.3-beta
- 004-bean-detail-page: Added C# 12, .NET 10.0 + MauiReactor (UI), UXDivers.Popups.Maui (feedback), Microsoft.EntityFrameworkCore (data)
- 003-profile-image-picker: Added C# 12 / .NET 10.0


<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
