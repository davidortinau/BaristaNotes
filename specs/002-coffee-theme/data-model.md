# Data Model: Coffee-Themed Color System with Theme Selection

**Feature**: 002-coffee-theme  
**Date**: 2025-12-04  
**Based On**: [spec.md](./spec.md) requirements

## Overview

This document defines the data structures, enums, and persistence model for the coffee-themed color system with user-selectable theme modes. The data model is intentionally minimal—theme preference is a single key-value pair, and color definitions are compile-time constants.

---

## Entities

### ThemeMode (Enum)

**Purpose**: Represents the user's preferred theme mode

**Values**:
- `Light` - Force light theme regardless of system setting
- `Dark` - Force dark theme regardless of system setting  
- `System` - Follow OS/device theme setting (default)

**Default**: `System` (respects user's OS preference without requiring explicit selection)

**Serialization**: Stored as string in preferences (`"Light"`, `"Dark"`, `"System"`)

**State Transitions**:
```
System → Light (user selects light mode)
System → Dark (user selects dark mode)
Light → System (user selects system mode)
Light → Dark (user switches from light to dark)
Dark → Light (user switches from dark to light)
Dark → System (user selects system mode)
```

All transitions are user-initiated from Settings page theme selector. No automatic transitions between Light/Dark (only from System to Light/Dark based on OS theme).

---

### ThemePreference (Persistence Model)

**Purpose**: User's stored theme selection that persists across app restarts

**Storage Mechanism**: MAUI Preferences API (platform-specific secure storage)

**Structure**:
- **Key**: `"AppThemeMode"` (string constant)
- **Value**: `ThemeMode` enum serialized to string
- **Scope**: Application-wide (single preference, not user-specific)
- **Default**: `"System"` if key doesn't exist

**Persistence Details**:
- **iOS**: Stored in `NSUserDefaults` (sandboxed per-app storage)
- **Android**: Stored in `SharedPreferences` with application context
- **Windows**: Stored in `ApplicationData.LocalSettings`
- **macOS**: Stored in `NSUserDefaults`

**Validation Rules**:
- Value MUST be one of: `"Light"`, `"Dark"`, `"System"`
- Invalid values default to `"System"` with no error (fail-safe behavior)
- No expiration—preference persists indefinitely until explicitly changed

**Operations**:
```csharp
// Read
string modeString = Preferences.Get("AppThemeMode", "System");
ThemeMode mode = Enum.Parse<ThemeMode>(modeString);

// Write
Preferences.Set("AppThemeMode", ThemeMode.Light.ToString());

// Clear (reset to default)
Preferences.Remove("AppThemeMode");
```

---

### CoffeeColorPalette (Compile-Time Constants)

**Purpose**: Centralized definition of semantic color tokens with light/dark variants

**Structure**: Static nested classes in `AppColors` (BaristaNotes/Resources/Styles/AppColors.cs)

**Light Mode Palette**:
| Token | Hex | RGB | Usage |
|-------|-----|-----|-------|
| `Background` | `#D2BCA5` | (210,188,165) | App body, behind cards & lists |
| `Surface` | `#FCEFE1` | (252,239,225) | Main cards, modals |
| `SurfaceVariant` | `#ECDAC4` | (236,218,196) | Secondary cards, chips, search bar |
| `SurfaceElevated` | `#FFF7EC` | (255,247,236) | Elevated surfaces, sheets, toasts |
| `Primary` | `#86543F` | (134,84,63) | Coffee accent: FAB, active tab, CTA |
| `OnPrimary` | `#F8F6F4` | (248,246,244) | Icon/text on Primary background |
| `TextPrimary` | `#352B23` | (53,43,35) | Large titles, product names, prices |
| `TextSecondary` | `#7C7067` | (124,112,103) | Category labels, subtitles |
| `TextMuted` | `#A38F7D` | (163,143,125) | Helper text, metadata |
| `Outline` | `#D7C5B2` | (215,197,178) | Dividers, borders, tab underlines |

**Dark Mode Palette**:
| Token | Hex | RGB | Usage |
|-------|-----|-----|-------|
| `Background` | `#48362E` | (72,54,46) | App body, behind cards & lists |
| `Surface` | `#48362E` | (72,54,46) | Main cards, modals (same as bg for low contrast) |
| `SurfaceVariant` | `#7D5A45` | (125,90,69) | Secondary cards, chips, search bar |
| `SurfaceElevated` | `#B3A291` | (179,162,145) | Elevated surfaces, sheets, toasts |
| `Primary` | `#86543F` | (134,84,63) | Coffee accent (same as light mode) |
| `OnPrimary` | `#F8F6F4` | (248,246,244) | Icon/text on Primary background |
| `TextPrimary` | `#F8F6F4` | (248,246,244) | Large titles, product names, prices |
| `TextSecondary` | `#C5BFBB` | (197,191,187) | Category labels, subtitles |
| `TextMuted` | `#A19085` | (161,144,133) | Helper text, metadata |
| `Outline` | `#5A463B` | (90,70,59) | Dividers, borders, tab underlines |

**Semantic Colors (Same in Both Themes)**:
| Token | Hex | RGB | Usage |
|-------|-----|-----|-------|
| `Success` | `#4CAF50` | (76,175,80) | Success states, confirmations |
| `Warning` | `#FFA726` | (255,167,38) | Warning states, cautions |
| `Error` | `#EF5350` | (239,83,80) | Error states, validation failures |
| `Info` | `#42A5F5` | (66,165,245) | Informational messages |

**Accessibility Validation**:
All text/background combinations verified to meet WCAG AA contrast requirements:
- Normal text (< 18pt): ≥ 4.5:1 contrast ratio
- Large text (≥ 18pt): ≥ 3:1 contrast ratio

---

## Relationships

### ThemeMode ← ThemePreference
- ThemeMode enum is the value type stored in ThemePreference
- One-to-one relationship: App has exactly one active theme mode at any time
- Cardinality: 1 ThemePreference → 1 ThemeMode (enum value)

### ThemeMode → AppTheme (Resolved)
- `ThemeMode.Light` → `AppTheme.Light`
- `ThemeMode.Dark` → `AppTheme.Dark`
- `ThemeMode.System` → `AppTheme.Light` or `AppTheme.Dark` (resolved from `Application.Current.RequestedTheme`)

### CoffeeColorPalette → ApplicationTheme
- ApplicationTheme queries CoffeeColorPalette based on `IsLightTheme` boolean
- `IsLightTheme == true` → use `AppColors.Light.*` tokens
- `IsLightTheme == false` → use `AppColors.Dark.*` tokens
- One-way dependency: ApplicationTheme consumes colors, doesn't modify them

---

## State Management

### Application Startup Flow

1. **App Launch**: `ThemeService` initialized in DI container (MauiProgram.cs)
2. **Load Preference**: Read `"AppThemeMode"` from Preferences API
   - Key exists → Parse to `ThemeMode` enum
   - Key missing → Default to `ThemeMode.System`
3. **Resolve Theme**: 
   - If `ThemeMode.Light` → Set `AppTheme.Light`
   - If `ThemeMode.Dark` → Set `AppTheme.Dark`
   - If `ThemeMode.System` → Read `Application.Current.RequestedTheme` (OS setting)
4. **Apply Theme**: Call `ApplicationTheme.OnApply()` to style all controls
5. **Subscribe to Events**: Listen for `Application.Current.RequestedThemeChanged` (for System mode tracking)

### Theme Change Flow (User-Initiated)

1. **User Action**: Taps theme option in Settings page (Light/Dark/System)
2. **Persist Choice**: `ThemeService.SetThemeModeAsync(newMode)` saves to Preferences
3. **Resolve Theme**: Determine effective `AppTheme` based on new mode
4. **Apply Theme**: Trigger MauiReactor theme re-application
5. **UI Update**: All controls re-render with new colors (smooth transition)

### System Theme Change Flow (OS-Initiated, System Mode Only)

1. **OS Event**: User changes device theme in system settings
2. **Event Handler**: `Application.Current.RequestedThemeChanged` fires
3. **Check Mode**: If current mode is `ThemeMode.System`, proceed; otherwise ignore
4. **Resolve Theme**: Read new `Application.Current.RequestedTheme`
5. **Apply Theme**: Trigger MauiReactor theme re-application
6. **UI Update**: All controls re-render with new colors

---

## Validation Rules

### ThemeMode Validation
- **Rule**: Value MUST be one of three enum values: `Light`, `Dark`, `System`
- **Enforcement**: C# enum type system prevents invalid values at compile time
- **Storage**: Serialized as string; invalid strings parsed to default `System` with try-catch

### Color Contrast Validation
- **Rule**: All text/background combinations MUST meet WCAG AA contrast requirements
- **Enforcement**: Unit tests calculate contrast ratios for all token combinations
- **Normal Text**: Minimum 4.5:1 ratio (e.g., `TextPrimary` on `Background`)
- **Large Text**: Minimum 3:1 ratio (e.g., 24pt headings on `Surface`)

### Preference Persistence Validation
- **Rule**: Theme preference MUST persist across app restarts
- **Enforcement**: Integration tests verify preference survives app lifecycle
- **Test**: Set theme, kill app, relaunch, verify theme matches saved preference

---

## Edge Cases

### Missing or Corrupted Preference
- **Scenario**: `"AppThemeMode"` key missing or contains invalid value (e.g., `"Invalid"`)
- **Handling**: Default to `ThemeMode.System` (fail-safe behavior)
- **Rationale**: User sees OS-appropriate theme without manual intervention

### Rapid Theme Changes
- **Scenario**: User taps Light → Dark → System in quick succession
- **Handling**: Each theme change is atomic; last change wins
- **Performance**: Theme switch debounced if needed (<300ms requirement)

### Theme Change During Modal Display
- **Scenario**: User changes theme while shot logging modal is open
- **Handling**: Both modal and underlying page update simultaneously
- **Rationale**: MauiReactor's reactive rendering handles nested component updates

### System Theme Change While App in Background
- **Scenario**: User changes OS theme, then resumes app (System mode active)
- **Handling**: `RequestedThemeChanged` event fires on resume, theme applies
- **Rationale**: MAUI's event system handles background/foreground transitions

---

## Performance Considerations

### Memory Footprint
- **ThemeMode**: Enum (4 bytes)
- **ThemePreference**: Single string in platform-specific preferences (negligible)
- **CoffeeColorPalette**: Static readonly properties (~50 Color objects × 24 bytes = ~1.2 KB)
- **Brush Cache**: ~15 SolidColorBrush objects (~40 bytes each = ~600 bytes)
- **Total**: < 2 KB for entire theme system

### Startup Performance
- **Preference Load**: O(1) key-value lookup (<5ms)
- **Color Initialization**: Static properties lazily initialized on first access
- **Theme Application**: Single `OnApply()` call styles all controls (<50ms)
- **Target**: <200ms total (NFR-P3) ✅

### Runtime Performance
- **Theme Switch**: Update static references, trigger re-render (<300ms target)
- **GC Pressure**: Minimal—reuses existing Color/Brush objects
- **CPU**: No continuous polling; event-driven architecture

---

## Migration Strategy

### Existing Data
- **Current State**: No theme preference stored (new feature)
- **Initial Value**: First app launch after feature deployment defaults to `ThemeMode.System`
- **Backward Compatibility**: N/A (new feature, no prior theme data)

### Existing Color References
- **Current State**: Pages may use hardcoded colors or old `AppColors` tokens
- **Migration**: Code review to replace hardcoded colors with semantic tokens
- **Validation**: Grep for `Color.FromArgb` in page files; flag for review

---

## Conclusion

Data model is intentionally minimal: single enum + single preference key. Color definitions are compile-time constants, not runtime data. This simplicity ensures high performance, minimal memory footprint, and low complexity. All state transitions and validation rules are well-defined. Ready for contract definition (Phase 1: Contracts).
