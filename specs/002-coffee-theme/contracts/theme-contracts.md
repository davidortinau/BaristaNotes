# Theme Contracts: Coffee-Themed Color System

**Feature**: 002-coffee-theme  
**Date**: 2025-12-04  
**Type**: Service Interfaces, Enums, and Color Token Contracts

## Overview

This document defines the programmatic contracts (interfaces, enums, method signatures) for the coffee-themed color system with user-selectable theme modes. These contracts establish the API surface between components and ensure consistent usage across the application.

---

## Service Interfaces

### IThemeService

**Purpose**: Encapsulates theme mode management, preference persistence, and theme application logic

**Namespace**: `BaristaNotes.Services`

**Interface Definition**:
```csharp
using Microsoft.Maui.ApplicationModel;

namespace BaristaNotes.Services;

public interface IThemeService
{
    /// <summary>
    /// Gets the current theme mode (Light, Dark, or System).
    /// </summary>
    ThemeMode CurrentMode { get; }
    
    /// <summary>
    /// Gets the current effective theme (Light or Dark), resolving System mode to actual OS theme.
    /// </summary>
    AppTheme CurrentTheme { get; }
    
    /// <summary>
    /// Asynchronously retrieves the user's saved theme mode preference.
    /// Returns ThemeMode.System if no preference is saved.
    /// </summary>
    Task<ThemeMode> GetThemeModeAsync();
    
    /// <summary>
    /// Asynchronously saves the user's theme mode preference and applies the new theme.
    /// </summary>
    /// <param name="mode">The theme mode to save and apply.</param>
    Task SetThemeModeAsync(ThemeMode mode);
    
    /// <summary>
    /// Forces re-application of the current theme.
    /// Useful when responding to system theme changes in System mode.
    /// </summary>
    void ApplyTheme();
}
```

**Methods**:

#### GetThemeModeAsync()
- **Returns**: `Task<ThemeMode>`
- **Behavior**: Reads `"AppThemeMode"` from Preferences API
- **Default**: Returns `ThemeMode.System` if key doesn't exist
- **Performance**: < 50ms (preference read is synchronous but wrapped in Task for consistency)
- **Thread Safety**: Safe to call from any thread
- **Error Handling**: Invalid preference values default to `ThemeMode.System` (no exceptions thrown)

#### SetThemeModeAsync(ThemeMode mode)
- **Parameters**: `mode` - ThemeMode enum value (Light, Dark, or System)
- **Returns**: `Task` (void async)
- **Behavior**: 
  1. Validates mode is valid enum value
  2. Saves to Preferences API with key `"AppThemeMode"`
  3. Updates `CurrentMode` property
  4. Calls `ApplyTheme()` to immediately reflect change
- **Performance**: < 100ms (NFR-P2 requirement)
- **Side Effects**: Triggers UI re-render via theme application
- **Thread Safety**: Safe to call from UI thread (async/await pattern)

#### ApplyTheme()
- **Returns**: `void`
- **Behavior**:
  1. Resolves effective `AppTheme` from `CurrentMode`
  2. If `System` mode, reads `Application.Current.RequestedTheme`
  3. Updates `Application.Current.UserAppTheme` to force theme
  4. MauiReactor's `ApplicationTheme.OnApply()` is called automatically
- **Performance**: < 200ms (NFR-P3 requirement)
- **Thread Safety**: MUST be called on main/UI thread (modifies Application.Current)

**Properties**:

#### CurrentMode
- **Type**: `ThemeMode`
- **Get**: Returns the user's selected mode (Light, Dark, or System)
- **Set**: Read-only (use `SetThemeModeAsync` to change)
- **Initial Value**: Loaded from preferences on service construction

#### CurrentTheme
- **Type**: `AppTheme` (MAUI enum: Light, Dark, Unspecified)
- **Get**: Returns resolved theme (never returns Unspecified)
- **Behavior**: 
  - If `CurrentMode == Light` → returns `AppTheme.Light`
  - If `CurrentMode == Dark` → returns `AppTheme.Dark`
  - If `CurrentMode == System` → returns `Application.Current.RequestedTheme`

**Lifecycle**:
- **Construction**: Load theme preference, subscribe to `RequestedThemeChanged` event
- **Disposal**: Unsubscribe from `RequestedThemeChanged` event (if IDisposable)

**Dependencies**:
- `IPreferencesService` (from BaristaNotes.Core) for preference persistence
- `Application.Current` (MAUI) for theme detection and application

---

## Enums

### ThemeMode

**Purpose**: Represents the user's theme preference

**Namespace**: `BaristaNotes.Services` (or `BaristaNotes.Models` if preferring model namespace)

**Enum Definition**:
```csharp
namespace BaristaNotes.Services;

/// <summary>
/// Represents the user's theme mode preference.
/// </summary>
public enum ThemeMode
{
    /// <summary>
    /// Use light theme regardless of system setting.
    /// </summary>
    Light,
    
    /// <summary>
    /// Use dark theme regardless of system setting.
    /// </summary>
    Dark,
    
    /// <summary>
    /// Follow the device's system theme setting.
    /// </summary>
    System
}
```

**Values**:
- `Light` (0): Force light theme
- `Dark` (1): Force dark theme
- `System` (2): Follow OS theme

**Serialization**:
- **To String**: `mode.ToString()` → `"Light"`, `"Dark"`, `"System"`
- **From String**: `Enum.Parse<ThemeMode>(value)` with fallback to `System` on failure

**Validation**:
- C# enum type system prevents invalid values at compile time
- Runtime string parsing uses try-catch to default to `System`

---

## Color Token Contracts

### AppColors (Static Class)

**Purpose**: Centralized color palette with semantic token names

**Namespace**: `BaristaNotes.Styles`

**Class Structure**:
```csharp
using Microsoft.Maui.Graphics;

namespace BaristaNotes.Styles;

/// <summary>
/// Coffee-themed color palette with light and dark mode variants.
/// All colors are static readonly for zero-allocation access.
/// </summary>
public static class AppColors
{
    // Semantic colors (same in both themes)
    public static Color Success { get; } = Color.FromArgb("#4CAF50");
    public static Color Warning { get; } = Color.FromArgb("#FFA726");
    public static Color Error { get; } = Color.FromArgb("#EF5350");
    public static Color Info { get; } = Color.FromArgb("#42A5F5");
    
    /// <summary>
    /// Light mode coffee palette.
    /// </summary>
    public static class Light
    {
        // Backgrounds & Surfaces
        public static Color Background { get; } = Color.FromArgb("#D2BCA5");
        public static Color Surface { get; } = Color.FromArgb("#FCEFE1");
        public static Color SurfaceVariant { get; } = Color.FromArgb("#ECDAC4");
        public static Color SurfaceElevated { get; } = Color.FromArgb("#FFF7EC");
        
        // Brand & Accent
        public static Color Primary { get; } = Color.FromArgb("#86543F");
        public static Color OnPrimary { get; } = Color.FromArgb("#F8F6F4");
        
        // Typography
        public static Color TextPrimary { get; } = Color.FromArgb("#352B23");
        public static Color TextSecondary { get; } = Color.FromArgb("#7C7067");
        public static Color TextMuted { get; } = Color.FromArgb("#A38F7D");
        
        // Borders & Dividers
        public static Color Outline { get; } = Color.FromArgb("#D7C5B2");
    }
    
    /// <summary>
    /// Dark mode coffee palette.
    /// </summary>
    public static class Dark
    {
        // Backgrounds & Surfaces
        public static Color Background { get; } = Color.FromArgb("#48362E");
        public static Color Surface { get; } = Color.FromArgb("#48362E");
        public static Color SurfaceVariant { get; } = Color.FromArgb("#7D5A45");
        public static Color SurfaceElevated { get; } = Color.FromArgb("#B3A291");
        
        // Brand & Accent
        public static Color Primary { get; } = Color.FromArgb("#86543F");
        public static Color OnPrimary { get; } = Color.FromArgb("#F8F6F4");
        
        // Typography
        public static Color TextPrimary { get; } = Color.FromArgb("#F8F6F4");
        public static Color TextSecondary { get; } = Color.FromArgb("#C5BFBB");
        public static Color TextMuted { get; } = Color.FromArgb("#A19085");
        
        // Borders & Dividers
        public static Color Outline { get; } = Color.FromArgb("#5A463B");
    }
}
```

**Token Categories**:

1. **Backgrounds & Surfaces**
   - `Background`: App body, behind cards/lists
   - `Surface`: Main cards, modals, elevated containers
   - `SurfaceVariant`: Secondary cards, chips, input backgrounds
   - `SurfaceElevated`: Highly elevated surfaces (sheets, toasts)

2. **Brand & Accent**
   - `Primary`: Coffee accent color for CTAs, FAB, active states
   - `OnPrimary`: Text/icons that sit on Primary background

3. **Typography**
   - `TextPrimary`: Headings, important content
   - `TextSecondary`: Body text, labels
   - `TextMuted`: Helper text, metadata, disabled states

4. **Borders & Dividers**
   - `Outline`: Divider lines, borders, underlines

**Usage Patterns**:
```csharp
// In MauiReactor components
Label("Hello")
    .TextColor(IsLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary)
    
// In ApplicationTheme.OnApply()
ButtonStyles.Default = _ => _
    .BackgroundColor(IsLightTheme ? AppColors.Light.Primary : AppColors.Dark.Primary)
    .TextColor(IsLightTheme ? AppColors.Light.OnPrimary : AppColors.Dark.OnPrimary);
```

**Accessibility Contract**:
All token pairs MUST meet WCAG AA contrast requirements:
- `TextPrimary` on `Background`: ≥ 4.5:1
- `TextSecondary` on `Background`: ≥ 4.5:1
- `TextMuted` on `Background`: ≥ 3:1 (if used for large text only)
- `OnPrimary` on `Primary`: ≥ 4.5:1

---

### ApplicationTheme (MauiReactor Theme Class)

**Purpose**: Extends MauiReactor's Theme base class to apply coffee color palette to all MAUI controls

**Namespace**: `BaristaNotes.Styles`

**Class Structure**:
```csharp
using MauiReactor;
using MauiReactor.Shapes;

namespace BaristaNotes.Styles;

/// <summary>
/// MauiReactor theme that applies coffee color palette to all controls.
/// Automatically detects light/dark mode via IsLightTheme property.
/// </summary>
class ApplicationTheme : Theme
{
    // Cached brush objects for performance (reuse across renders)
    public static Brush PrimaryBrush { get; private set; }
    public static Brush SurfaceBrush { get; private set; }
    public static Brush BackgroundBrush { get; private set; }
    // ... other brushes
    
    protected override void OnApply()
    {
        // Update cached brushes based on current theme
        UpdateBrushes();
        
        // Apply styles to all control types using coffee colors
        ApplyButtonStyles();
        ApplyLabelStyles();
        ApplyPageStyles();
        // ... other control styles
    }
    
    private void UpdateBrushes()
    {
        PrimaryBrush = new SolidColorBrush(
            IsLightTheme ? AppColors.Light.Primary : AppColors.Dark.Primary);
        SurfaceBrush = new SolidColorBrush(
            IsLightTheme ? AppColors.Light.Surface : AppColors.Dark.Surface);
        // ...
    }
    
    private void ApplyButtonStyles()
    {
        ButtonStyles.Default = _ => _
            .TextColor(IsLightTheme ? AppColors.Light.OnPrimary : AppColors.Dark.OnPrimary)
            .BackgroundColor(IsLightTheme ? AppColors.Light.Primary : AppColors.Dark.Primary)
            // ... other button properties
    }
    
    // ... more style application methods
}
```

**Protected Methods**:
- `OnApply()`: Called by MauiReactor when theme changes; override to apply custom styles

**Protected Properties**:
- `IsLightTheme`: Boolean indicating if current theme is light mode (true) or dark mode (false)

**Public Static Properties** (Cached Brushes):
- `PrimaryBrush`, `SurfaceBrush`, `BackgroundBrush`, etc.
- Initialized in `OnApply()` to avoid allocations on every component render

---

## Event Contracts

### Application.RequestedThemeChanged

**Purpose**: MAUI event that fires when OS/system theme changes

**Event Handler Signature**:
```csharp
void OnSystemThemeChanged(object sender, AppThemeChangedEventArgs e)
```

**Parameters**:
- `sender`: `Application.Current`
- `e.RequestedTheme`: New `AppTheme` value (Light or Dark)

**Subscription**:
```csharp
Application.Current.RequestedThemeChanged += OnSystemThemeChanged;
```

**Usage in ThemeService**:
```csharp
private void OnSystemThemeChanged(object sender, AppThemeChangedEventArgs e)
{
    // Only respond if System mode is active
    if (CurrentMode == ThemeMode.System)
    {
        ApplyTheme(); // Re-apply theme to reflect OS change
    }
}
```

---

## Dependency Injection Registration

**MauiProgram.cs Registration**:
```csharp
// Register theme service as singleton (app-wide state)
builder.Services.AddSingleton<IThemeService, ThemeService>();
```

**Rationale**: Singleton scope ensures single theme state across app lifetime

---

## Versioning & Compatibility

**Current Version**: 1.0.0

**Breaking Changes**: None (new feature)

**Backward Compatibility**: 
- Existing pages using old color references will continue to work
- New pages SHOULD use semantic tokens from `AppColors.Light` / `AppColors.Dark`

**Future Extensions**:
- Additional theme modes (e.g., "Auto" with time-based switching)
- Custom color palette editor (user-defined colors)
- Per-page theme overrides

**Deprecation Policy**:
- Old `AppColors` properties (non-semantic names) marked `[Obsolete]` after migration period
- Removal planned for v2.0 after all pages migrated to semantic tokens

---

## Testing Contracts

### Unit Test Coverage Requirements

**ThemeService**:
- ✅ `GetThemeModeAsync` returns default `System` when no preference saved
- ✅ `GetThemeModeAsync` returns saved preference value
- ✅ `SetThemeModeAsync` persists mode to preferences
- ✅ `SetThemeModeAsync` updates `CurrentMode` property
- ✅ `SetThemeModeAsync` calls `ApplyTheme()`
- ✅ `ApplyTheme` resolves correct `AppTheme` for each `ThemeMode`
- ✅ System theme change event triggers `ApplyTheme` only in System mode

**Color Contrast Validation**:
- ✅ `TextPrimary` on `Background` meets 4.5:1 ratio (light & dark)
- ✅ `TextSecondary` on `Background` meets 4.5:1 ratio (light & dark)
- ✅ `OnPrimary` on `Primary` meets 4.5:1 ratio
- ✅ All text/surface combinations documented and validated

**Preference Persistence**:
- ✅ Theme preference survives app restart (integration test)
- ✅ Invalid preference values default to `System` without crash

---

## Conclusion

All contracts defined for theme service, enums, color tokens, and event handling. Interfaces are minimal and focused—`IThemeService` provides three methods and two properties. Color palette uses static readonly properties for zero-allocation access. WCAG AA accessibility requirements are explicit contract constraints. Ready for implementation (Phase 2: Tasks).
