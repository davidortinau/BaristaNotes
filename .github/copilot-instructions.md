# BaristaNotes Development Guidelines

Auto-generated from all feature plans. Last updated: 2025-12-24

## Active Technologies
- C# 12 / .NET 10.0 + MauiReactor 4.0.3-beta, Microsoft.Extensions.AI, Microsoft.Extensions.AI.OpenAI, OpenAI SDK, Entity Framework Core 8.0 (001-ai-shot-advice)
- SQLite via EF Core (existing) - adding TastingNotes field to ShotRecord (001-ai-shot-advice)
- C# 12 / .NET 10.0 + Microsoft.Extensions.Logging.Debug 10.0.0 (already installed), Microsoft.Extensions.DependencyInjection (MAUI framework) (001-logging-migration)
- N/A (logging only, no data persistence changes) (001-logging-migration)
- C# 12 / .NET 10.0 + MauiReactor 4.0.3-beta, UXDivers.Popups.Maui 0.9.0, Entity Framework Core 8.0.0 (001-inline-bean-creation)
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
  BaristaNotes/          # MAUI head: net11.0-android;net11.0-ios;net11.0-maccatalyst (+windows on Win)
  BaristaNotes.Core/     # net11.0 shared models/services
  BaristaNotes.Tests/    # net11.0 xUnit tests
  BaristaNotes.sln
specs/                   # Feature plans (001-*, 002-*, etc.)
scripts/
```

**Actual TFM is `net11.0`**, not 10.0 — ignore the ".NET 10.0" strings in the auto-generated lines above; they are stale spec metadata, not the build truth.

## Commands

```bash
# Build (pick the TFM you intend to run)
dotnet build src/BaristaNotes -f net11.0-ios
dotnet build src/BaristaNotes -f net11.0-maccatalyst
dotnet build src/BaristaNotes -f net11.0-android

# Run on iOS sim / Mac Catalyst (prefer DevFlow for the full loop — see below)
dotnet build src/BaristaNotes -t:Run -f net11.0-ios
dotnet build src/BaristaNotes -t:Run -f net11.0-maccatalyst

# Tests (xUnit)
dotnet test src/BaristaNotes.Tests
dotnet test src/BaristaNotes.Tests --filter "FullyQualifiedName~ShotRecord"   # single test/class

# Clean if the SourceGen cache acts up (CS0436 duplicate-type errors)
dotnet build src/BaristaNotes -t:Clean
rm -rf src/BaristaNotes/obj src/BaristaNotes/bin
```

For any running-app work (build → deploy → inspect → fix), use the **`maui-devflow-debug`** skill — not `osascript`, `xcrun simctl io`, or `adb shell`. Standard loop:

```bash
dotnet build src/BaristaNotes -t:Run -f net11.0-ios   # or maui devflow run
maui devflow wait
# then: screenshot / inspect / interact / read logs
maui devflow MAUI logs --follow
```

## Code Style

C# / .NET 11.0: Follow standard conventions. File-scoped namespaces; nullable reference types enabled; `record` for DTOs where appropriate; MauiReactor `Component<TState>` for UI.

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

## DevFlow: Target Selection & Waiting (MANDATORY)

Repeated failure mode: agent boots a stale simulator while the user already has the right one running, or sits in `maui devflow wait` for minutes while the app has been foreground the whole time.

**Before** invoking `maui devflow run` / `wait` or `xcrun simctl boot`:

1. Check what's already running and prefer it:
   - `xcrun simctl list devices booted` — use the booted simulator if there is one
   - `maui devflow broker status` — if an agent is already connected to a device/sim, that's your target
   - `xcrun devicectl list devices` — for physical devices (DX24, etc.)
2. The user runs **iPhone 17 Pro on iOS 26.x**. Do NOT default to iOS 18.x simulators; pick the newest matching runtime if you must boot fresh.
3. Never spin up a second simulator of the same family — kill the stale one or attach to the live one.

**While waiting**: if `maui devflow wait` exceeds ~30s but you have other signals the app is up (logs streaming, process running, screenshot succeeds), **stop waiting and screenshot/inspect to verify state**. Treat >60s on any single debug step as a signal to pivot, not to wait longer.

## Visual Tree Inspection Before Tap Attribution (MANDATORY)

Trust-breaking failure mode: agent describes a tap-induced behavior ("the + button is launching the camera through a hidden layer") that the user can plainly see is wrong, because the agent inferred instead of inspected.

**Before** claiming what a UI element is, what page is active, or why a tap had an unexpected effect:

1. `screenshot` to confirm what's actually on screen
2. `inspect` (visual tree) to confirm the element id/type/handler at the coordinates you tapped
3. Only then describe the result

Never explain an unexpected behavior with a phantom "hidden control behind another control" or a "pre-existing layered handler" without visual-tree evidence. If the user says "that's not what I see," they're right — go re-inspect, don't re-argue.

## Sample / Test Data: Exercise the App, Don't Touch the DB (MANDATORY)

Direct user rule: *"you should seed the app data by exercising the app, not bypassing the app and going to the database."*

- Seed users, beans, shots, equipment, etc. via the app's own forms — `maui devflow interact` taps, mock voice commands through `VoiceCommandService`, or manual entry the user can repeat.
- Direct EF Core / raw SQLite `INSERT`s into `barista_notes.db` are for **read-only debugging only**, never for creating sample state.
- This is a single-user dev app with no historical data on anyone's machine other than the user's own. **Do not author EF Core migrations, "database reset" steps, or data-preservation ceremony "just in case"** — if you think you need one, ask first.

## MediaPicker & Image Pipeline (MANDATORY for any avatar / photo work)

Hard-won DX24 lessons. These will be hit again — don't rediscover them:

- **`MediaPicker.PickPhotosAsync`'s `MaximumWidthHeight` / `CompressionQuality` only apply to camera capture, NOT library picks.** A library pick on a real iPhone returns a full-resolution HEIC/JPEG (5–15 MB). Downsample explicitly with `Microsoft.Maui.Graphics.Platform.PlatformImage` *before* any size-cap validation, or your validator will silently reject every photo.
- **For absolute sandbox paths, load via `ImageSource.FromStream(...)`**, not `Image.Source(absolutePath)` or `ImageSource.FromFile(absolutePath)`. The latter sometimes hits bundle-resource resolution first on iOS and fails to load arbitrary sandbox files.
- **HEIC decode can silently return null** from `PlatformImage.FromStream`. Detect null and fall back; never let the original full-res stream proceed to a size-capped validator unchanged.
- **Cache-bust on re-pick.** If your file naming scheme reuses the same path (e.g. `profile_avatar_{id}.jpg`), `Image` will return the cached version. Either rename per-pick or append a cache-bust query/suffix.
- **Missing-glyph "?" in a `CircularAvatar`-style placeholder usually means font family mismatch**, not a broken image. Check that the control's requested family string (e.g. `"MaterialSymbolsOutlined"`) matches what's actually registered in `MauiProgram.cs` (e.g. `MaterialSymbolsFont.FontFamily = "MaterialIcons..."`). Verify font registration **before** going down image-pipeline rabbit holes.
- Test image features on the physical device (DX24) *before* declaring victory — the simulator's sample photos are small enough to hide every bug above.

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
- 001-inline-bean-creation: Added C# 12 / .NET 10.0 + MauiReactor 4.0.3-beta, UXDivers.Popups.Maui 0.9.0, Entity Framework Core 8.0.0
- 001-logging-migration: Added C# 12 / .NET 10.0 + Microsoft.Extensions.Logging.Debug 10.0.0 (already installed), Microsoft.Extensions.DependencyInjection (MAUI framework)
- 001-ai-shot-advice: Added C# 12 / .NET 10.0 + MauiReactor 4.0.3-beta, Microsoft.Extensions.AI, Microsoft.Extensions.AI.OpenAI, OpenAI SDK, Entity Framework Core 8.0
- 001-bean-rating-tracking: Added C# .NET 10.0 + .NET MAUI 10.0, Entity Framework Core 10.0, SQLite, Reactor.Maui 4.0.3-beta
- 004-bean-detail-page: Added C# 12, .NET 10.0 + MauiReactor (UI), UXDivers.Popups.Maui (feedback), Microsoft.EntityFrameworkCore (data)
- 003-profile-image-picker: Added C# 12 / .NET 10.0


<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
