# Research: Coffee-Themed Color System with Theme Selection

**Feature**: 002-coffee-theme  
**Date**: 2025-12-04  
**Status**: Complete

## Overview

This document captures technology decisions and best practices for implementing a coffee-themed color palette with user-selectable theme modes (Light/Dark/System) in the BaristaNotes MAUI application using MauiReactor.

---

## Technology Decisions

### 1. MauiReactor Theme System Integration

**Decision**: Extend existing `ApplicationTheme : Theme` class and leverage `IsLightTheme` property with `OnApply()` method

**Rationale**: 
- MauiReactor's `Theme` base class already provides theme detection via `Application.Current.RequestedTheme`
- `OnApply()` lifecycle method is called automatically when theme changes
- Existing `ApplicationTheme.cs` already uses this pattern extensively with conditional styling: `IsLightTheme ? LightColor : DarkColor`
- Preserves all existing style definitions while adding coffee color support

**Alternatives Considered**:
- **Custom theme manager**: Rejected—would duplicate MauiReactor framework functionality and break existing style definitions in 20+ control types
- **XAML ResourceDictionary approach**: Rejected—MauiReactor uses C# styling API, not XAML

**Implementation Approach**:
```csharp
class ApplicationTheme : Theme
{
    protected override void OnApply()
    {
        // IsLightTheme provided by base Theme class
        ButtonStyles.Default = _ => _
            .BackgroundColor(IsLightTheme ? AppColors.Light.Primary : AppColors.Dark.Primary)
            .TextColor(IsLightTheme ? AppColors.Light.OnPrimary : AppColors.Dark.OnPrimary);
    }
}
```

---

### 2. System Theme Change Detection

**Decision**: Subscribe to `Application.Current.RequestedThemeChanged` event to detect OS-level theme changes when "System" theme mode is active

**Rationale**:
- Native MAUI event provides reliable, platform-agnostic theme change notifications
- No polling required—event-driven approach is efficient
- Integrates seamlessly with MauiReactor's theme system
- Works across iOS (UITraitCollection), Android (Configuration), Windows, and macOS

**Alternatives Considered**:
- **Platform-specific listeners**: iOS `UITraitCollection.TraitCollectionDidChangeNotification`, Android `onConfigurationChanged`—rejected because MAUI already abstracts this cross-platform
- **Polling system theme**: Check `Application.Current.RequestedTheme` on timer—rejected as inefficient and introduces lag

**Implementation Approach**:
```csharp
public class ThemeService : IThemeService
{
    public ThemeService()
    {
        Application.Current.RequestedThemeChanged += OnSystemThemeChanged;
    }
    
    private void OnSystemThemeChanged(object sender, AppThemeChangedEventArgs e)
    {
        if (CurrentMode == ThemeMode.System)
        {
            ApplyTheme(); // Re-apply theme to reflect system change
        }
    }
}
```

---

### 3. WCAG AA Contrast Ratio Validation

**Decision**: Implement contrast calculator using WCAG 2.1 relative luminance formula for automated testing

**Rationale**:
- WCAG 2.1 specifies deterministic formula: `(L1 + 0.05) / (L2 + 0.05)` where L is relative luminance
- Relative luminance calculated from RGB: `0.2126 * R + 0.7152 * G + 0.0722 * B` (after gamma correction)
- Automated validation in unit tests prevents regression when colors change
- Complement manual testing with screen readers

**WCAG AA Requirements**:
- Normal text (< 18pt or < 14pt bold): Minimum 4.5:1 contrast ratio
- Large text (≥ 18pt or ≥ 14pt bold): Minimum 3:1 contrast ratio

**Alternatives Considered**:
- **Manual testing only**: Rejected—no regression protection, time-consuming
- **Third-party accessibility scanning tools**: Rejected as unnecessary for color-only validation; formula is simple to implement

**Implementation Approach**:
```csharp
public static class ContrastCalculator
{
    public static double CalculateContrast(Color foreground, Color background)
    {
        double l1 = GetRelativeLuminance(foreground);
        double l2 = GetRelativeLuminance(background);
        return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
    }
    
    private static double GetRelativeLuminance(Color color)
    {
        // Apply gamma correction and calculate luminance
        double r = GetLuminanceComponent(color.Red);
        double g = GetLuminanceComponent(color.Green);
        double b = GetLuminanceComponent(color.Blue);
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }
    
    private static double GetLuminanceComponent(float component)
    {
        double c = component;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
```

---

### 4. Theme Preference Persistence

**Decision**: Use MAUI `Preferences` API with key "AppThemeMode" storing `ThemeMode` enum as string

**Rationale**:
- MAUI Preferences API is platform-agnostic and secure (iOS Keychain, Android SharedPreferences with encryption)
- Already used in project—`IPreferencesService` interface exists in BaristaNotes.Core
- Simple key-value storage appropriate for single preference value
- Synchronous API suitable for quick theme load on app startup

**Storage Details**:
- Key: `"AppThemeMode"`
- Value: ThemeMode enum serialized to string (`"Light"`, `"Dark"`, `"System"`)
- Default: `"System"` (respects user's OS preference)

**Alternatives Considered**:
- **SQLite database**: Rejected—overkill for single key-value pair, adds unnecessary dependency and complexity
- **File storage** (JSON/XML): Rejected—Preferences API handles platform-specific secure storage better with less code
- **In-memory only**: Rejected—requirement explicitly states persistence across app restarts

**Implementation Approach**:
```csharp
public async Task<ThemeMode> GetThemeModeAsync()
{
    var modeString = _preferencesService.Get("AppThemeMode", "System");
    return Enum.Parse<ThemeMode>(modeString);
}

public async Task SetThemeModeAsync(ThemeMode mode)
{
    _preferencesService.Set("AppThemeMode", mode.ToString());
}
```

---

### 5. Smooth Theme Transition Animation

**Decision**: Leverage MauiReactor's reactive rendering to automatically animate property changes; ensure `OnApply()` updates atomically

**Rationale**:
- MauiReactor's component diffing engine handles smooth property transitions automatically
- Key is avoiding partial state where some controls are styled and others aren't
- Single `OnApply()` method ensures all style updates happen in one render pass
- No white flashes if theme colors are resolved before render

**Alternatives Considered**:
- **Custom fade animations**: Animate each control individually with opacity/color animation—rejected as too complex, error-prone, and hard to maintain across 20+ control types
- **Delay rendering**: Buffer theme change and render once all colors computed—rejected because introduces visible lag (100-200ms delay)

**Best Practices**:
- Pre-compute all colors before calling `OnApply()`
- Use static readonly color objects (no allocations during theme switch)
- Avoid async operations in `OnApply()` (blocks rendering)

---

### 6. Coffee Color Palette Implementation

**Decision**: Static readonly properties in `AppColors.Light` and `AppColors.Dark` nested classes

**Rationale**:
- Matches existing `AppColors.cs` structure (already has `AppColors.Light` nested class)
- Zero runtime overhead—colors initialized once at app startup
- Compile-time type safety—typos caught at build time
- IDE autocomplete support—IntelliSense shows available semantic tokens
- No dependency injection complexity for immutable constants

**Color Organization**:
```csharp
public static class AppColors
{
    // Semantic colors (same in light and dark)
    public static Color Success { get; } = Color.FromArgb("#4CAF50");
    public static Color Warning { get; } = Color.FromArgb("#FFA726");
    public static Color Error { get; } = Color.FromArgb("#EF5350");
    public static Color Info { get; } = Color.FromArgb("#42A5F5");
    
    public static class Light
    {
        public static Color Background { get; } = Color.FromArgb("#D2BCA5");
        public static Color Surface { get; } = Color.FromArgb("#FCEFE1");
        public static Color SurfaceVariant { get; } = Color.FromArgb("#ECDAC4");
        public static Color SurfaceElevated { get; } = Color.FromArgb("#FFF7EC");
        public static Color Primary { get; } = Color.FromArgb("#86543F");
        public static Color OnPrimary { get; } = Color.FromArgb("#F8F6F4");
        public static Color TextPrimary { get; } = Color.FromArgb("#352B23");
        public static Color TextSecondary { get; } = Color.FromArgb("#7C7067");
        public static Color TextMuted { get; } = Color.FromArgb("#A38F7D");
        public static Color Outline { get; } = Color.FromArgb("#D7C5B2");
    }
    
    public static class Dark
    {
        public static Color Background { get; } = Color.FromArgb("#48362E");
        public static Color Surface { get; } = Color.FromArgb("#48362E");
        public static Color SurfaceVariant { get; } = Color.FromArgb("#7D5A45");
        public static Color SurfaceElevated { get; } = Color.FromArgb("#B3A291");
        public static Color Primary { get; } = Color.FromArgb("#86543F");
        public static Color OnPrimary { get; } = Color.FromArgb("#F8F6F4");
        public static Color TextPrimary { get; } = Color.FromArgb("#F8F6F4");
        public static Color TextSecondary { get; } = Color.FromArgb("#C5BFBB");
        public static Color TextMuted { get; } = Color.FromArgb("#A19085");
        public static Color Outline { get; } = Color.FromArgb("#5A463B");
    }
}
```

**Alternatives Considered**:
- **XAML ResourceDictionary**: Rejected—MauiReactor uses C# styling API exclusively, not XAML markup
- **Dependency injection**: Inject `IColorPalette` service—rejected as overkill for immutable constants; adds complexity with no benefit
- **Configuration file** (JSON/YAML): Load colors from external file—rejected because colors are design-time constants, not runtime configuration

---

## Best Practices

### Semantic Color Naming

**Principle**: Use intent-based names (Background, Surface, Primary) rather than visual descriptions (LightBrown, DarkCream)

**Benefits**:
- Facilitates future theme changes without renaming throughout codebase
- Communicates purpose to developers (Background = page body, Surface = card/modal, Primary = brand accent)
- Aligns with Material Design and Apple Human Interface Guidelines terminology

**Examples**:
- ✅ `AppColors.Light.Background` — clear purpose
- ❌ `AppColors.Light.WarmCream` — implementation detail leaks, hard to change

---

### Color Token Centralization

**Principle**: All color definitions in single file (`AppColors.cs`), no hardcoded hex values in UI components

**Benefits**:
- Single source of truth for color palette
- Easy to find and update colors without hunting through codebase
- Compile-time safety—refactoring tools can track usages
- Prevents drift where different pages use slightly different shades

**Enforcement**:
- Code review checklist: Reject PRs with hardcoded colors like `.TextColor(Color.FromArgb("#123456"))`
- Require usage: `.TextColor(AppColors.Light.TextPrimary)`

---

### Performance Optimization

**Principle**: Static color objects initialized once at startup; brush objects cached; theme switching reuses objects

**Techniques**:
1. **Static Color Properties**: No allocations during theme switches
2. **Brush Caching**: Create `SolidColorBrush` objects once in `ApplicationTheme` static properties
3. **Object Reuse**: Theme switch updates property references, doesn't create new Color instances

**Performance Targets**:
- Theme switch: <300ms (NFR-P1)
- Preference persistence: <100ms (NFR-P2)
- Initial theme load: <200ms (NFR-P3)

---

### Accessibility Testing

**Principle**: Combine automated contrast tests with manual screen reader testing

**Automated Testing**:
- Unit tests validate all text/background combinations meet WCAG AA ratios
- Run on every build—prevents regression when colors change
- Example: `Assert.True(ContrastCalculator.CalculateContrast(TextPrimary, Background) >= 4.5)`

**Manual Testing**:
- iOS: VoiceOver enabled, verify theme selection announces "Light theme selected"
- Android: TalkBack enabled, verify theme changes are announced
- Test theme changes while screen reader active—ensure smooth experience

---

## Integration Points

### Existing System Dependencies

1. **MauiReactor Theme System**
   - Extend `ApplicationTheme : Theme` base class
   - Implement `OnApply()` lifecycle method
   - Use `IsLightTheme` property for conditional styling

2. **MAUI Preferences API**
   - Use existing `IPreferencesService` wrapper from BaristaNotes.Core
   - Key: `"AppThemeMode"`, Value: `ThemeMode` enum as string
   - Synchronous API suitable for quick startup load

3. **Application.RequestedThemeChanged Event**
   - Subscribe in `ThemeService` constructor
   - Detect OS theme changes for "System" mode
   - Trigger theme re-application when event fires

4. **Existing Pages** (6 pages)
   - ShotLoggingPage.cs
   - ActivityFeedPage.cs
   - SettingsPage.cs (add theme selection UI here)
   - EquipmentManagementPage.cs
   - BeanManagementPage.cs
   - UserProfileManagementPage.cs
   - **Action Required**: Verify coffee colors render correctly (no code changes needed if semantic tokens used properly)

---

## Risk Mitigation

### Performance Risks

**Risk**: Theme switching feels sluggish or causes UI jank  
**Mitigation**: 
- Benchmark theme switch with performance profiler
- Ensure <300ms target (NFR-P1)
- Pre-allocate brush objects to avoid GC pressure

### Accessibility Risks

**Risk**: Contrast ratios fail WCAG AA after color updates  
**Mitigation**:
- Automated contrast tests on every build
- Designer provides WCAG-validated color palette upfront
- Manual verification with accessibility tools

### User Experience Risks

**Risk**: White flash during theme transitions  
**Mitigation**:
- Leverage MauiReactor's atomic rendering
- Update all styles in single `OnApply()` pass
- Test on physical devices (simulators may not show flashing)

### Compatibility Risks

**Risk**: System theme detection fails on older OS versions  
**Mitigation**:
- MAUI's `RequestedThemeChanged` event works on all supported platforms
- Fallback: If event doesn't fire, user can manually select theme in settings

---

## Conclusion

All research questions resolved. Technology decisions align with existing BaristaNotes architecture (MauiReactor + MAUI + Preferences API). Best practices identified for semantic color naming, centralization, performance, and accessibility. Ready to proceed to Phase 1: Design & Contracts.
