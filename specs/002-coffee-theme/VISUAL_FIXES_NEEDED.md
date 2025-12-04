# Visual Theme Issues - Use MauiReactor Theme System

**Status**: Foundation complete, pages need theme key updates  
**Date**: 2025-12-04  
**Approach**: Leverage MauiReactor's centralized theme system with semantic theme keys

## What's Been Completed

✅ **Core Theme Infrastructure**:
- Coffee color palette defined in `AppColors.cs` (Light + Dark modes)
- `ApplicationTheme.cs` updated with coffee colors for all control styles
- **NEW**: Semantic theme keys added (SecondaryText, MutedText, Card, CardVariant)
- All WCAG AA contrast tests pass
- Project builds successfully

✅ **Global Styles**:
- All MauiReactor control styles (Button, Label, Entry, etc.) use coffee colors
- Border, Page, Shell, Navigation styles updated
- Default theme colors automatically applied to new controls
- **Theme Keys Available**: Use `.ThemeKey(ThemeKeys.X)` for semantic styling

## MauiReactor Theme System Approach

Instead of inline color helpers, use MauiReactor's centralized theme system:

### ❌ OLD Approach (Inline Colors - DO NOT USE)
```csharp
Label("Made By")
    .FontSize(14)
    .TextColor(Colors.Gray)  // Hardcoded, not theme-aware
```

### ✅ NEW Approach (Theme Keys - USE THIS)
```csharp
Label("Made By")
    .ThemeKey(ThemeKeys.SecondaryText)  // Centralized, theme-aware, consistent
```

### Benefits of Theme Keys
1. **Centralized**: All styling in ApplicationTheme.cs
2. **Consistent**: Same key = same appearance everywhere
3. **Theme-aware**: Automatically switches between Light/Dark
4. **Maintainable**: Change once, applies everywhere
5. **Semantic**: Keys describe purpose, not appearance

## Available Theme Keys

### Text Styles
```csharp
// Label themes
Label("Label Text").ThemeKey(ThemeKeys.SecondaryText)  // 14pt, secondary color
Label("Helper text").ThemeKey(ThemeKeys.MutedText)      // 12pt, muted color
Label("Caption").ThemeKey(ThemeKeys.Caption)            // 12pt, secondary color

// Existing themes (already defined)
Label("Title").ThemeKey("Headline")                     // 32pt, centered
Label("Subtitle").ThemeKey("SubHeadline")               // 24pt, centered
```

### Container Styles
```csharp
// Border/Card themes
Border()
    .ThemeKey(ThemeKeys.Card)         // Surface color, rounded corners, padding
    .Content(/* your content */)

Border()
    .ThemeKey(ThemeKeys.CardVariant)  // SurfaceVariant color, for secondary cards
    .Content(/* your content */)
```

## What Needs Manual Fixing

### Issue: Hardcoded Colors in Pages (100+ instances)

**Affected Files** (with instance counts):
- `BeanManagementPage.cs` - 26 instances
- `UserProfileManagementPage.cs` - 27 instances  
- `EquipmentManagementPage.cs` - 21 instances
- `ShotLoggingPage.cs` - 12 instances
- `SettingsPage.cs` - 10 instances
- `ActivityFeedPage.cs` - 4 instances

### Required Replacements

#### 1. Text Labels (Most Common)
```csharp
// BEFORE (hardcoded)
Label("Made By")
    .FontSize(14)
    .TextColor(Colors.Gray)

Label("Helper text")
    .FontSize(12)
    .TextColor(Colors.LightGray)

// AFTER (theme key)
Label("Made By")
    .ThemeKey(ThemeKeys.SecondaryText)

Label("Helper text")
    .ThemeKey(ThemeKeys.MutedText)
```

#### 2. Card Backgrounds
```csharp
// BEFORE (manual styling)
Border()
    .BackgroundColor(Colors.White)
    .Stroke(Colors.LightGray)
    .StrokeThickness(1)
    .Padding(12)
    .Content(/* content */)

// AFTER (theme key)
Border()
    .ThemeKey(ThemeKeys.Card)
    .Content(/* content */)
```

#### 3. Secondary Cards
```csharp
// BEFORE
Border()
    .BackgroundColor(Colors.LightGray)
    .Stroke(Colors.Gray)
    .StrokeThickness(1)
    .Content(/* content */)

// AFTER  
Border()
    .ThemeKey(ThemeKeys.CardVariant)
    .Content(/* content */)
```

#### 4. When Theme Keys Don't Fit
For cases where theme keys don't match your needs, reference colors directly:
```csharp
// Use AppColors for custom styling
Label("Custom Text")
    .TextColor(ApplicationTheme.IsLightTheme 
        ? AppColors.Light.TextSecondary 
        : AppColors.Dark.TextSecondary)
```

## Specific Visual Problems & Fixes

### 2.1 Border Controls (Gray Outlines)
**Symptoms**: Gray borders that don't match coffee theme  
**Location**: All list item cards, entry fields
**Fix**:
```csharp
// Option 1: Use theme key (recommended)
Border()
    .ThemeKey(ThemeKeys.Card)
    .Content(/* your content */)

// Option 2: Just update stroke (if other styling needed)
Border()
    .Stroke(ApplicationTheme.IsLightTheme ? AppColors.Light.Outline : AppColors.Dark.Outline)
    // ... other custom properties
```

### 2.2 Card Backgrounds (Off-White/Gray)
**Locations**: Equipment, Bean, User Profile, Activity feed cards
**Fix**:
```csharp
// Replace all card-like borders with theme key
Border()
    .ThemeKey(ThemeKeys.Card)  // Handles background, stroke, padding
    .Content(/* your card content */)
```

### 2.3 Text Labels (Poor Contrast)
**Locations**: "grams in/out", "Made by", timestamps, form labels
**Fix**:
```csharp
// Secondary labels (14pt)
Label("Made By")
    .ThemeKey(ThemeKeys.SecondaryText)

// Small helper text (12pt)
Label("Optional")
    .ThemeKey(ThemeKeys.MutedText)

// Captions/timestamps (12pt)
Label("2 hours ago")
    .ThemeKey(ThemeKeys.Caption)
```

### 2.4 Entry Field Borders
**Location**: "Add a new shot" page entry fields
**Fix**: Entry styles are already themed via ApplicationTheme.cs EntryStyles.Default.
If custom border styling is applied, use:
```csharp
Entry()
    // Remove any hardcoded stroke/background colors
    // Let default theme styling apply
```

### 2.5 Bottom Sheet Styling
**Location**: Modal bottom sheets across pages
**Investigation Needed**: Check The49.Maui.BottomSheet component for theme property support

### 2.6 Settings Page
**Location**: `SettingsPage.cs` - 10 instances
**Fix**: Replace all hardcoded colors with theme keys

## Implementation Strategy

### Recommended Approach: Systematic Theme Key Replacement

**Phase 1** (High Priority - 20 minutes):
1. Replace all `Label().TextColor(Colors.Gray)` with `.ThemeKey(ThemeKeys.SecondaryText)`
2. Replace all `Label().FontSize(12).TextColor(Colors.LightGray)` with `.ThemeKey(ThemeKeys.MutedText)`
3. Add `using BaristaNotes.Styles;` to access `ThemeKeys`

**Phase 2** (Medium Priority - 20 minutes):
1. Replace card-like `Border()` instances with `.ThemeKey(ThemeKeys.Card)`
2. Replace secondary surfaces with `.ThemeKey(ThemeKeys.CardVariant)`
3. Test on device in both light and dark modes

**Phase 3** (Low Priority - 15 minutes):
1. Fix Settings page completely  
2. Investigate bottom sheet styling
3. Fix any remaining hardcoded colors found during testing

**Total Estimated Time**: 50-60 minutes of focused work

## File-by-File Replacement Guide

### Pattern to Search For
Use your IDE's find feature to locate these patterns:

1. **Text Colors**:
   - Search: `.TextColor(Colors.Gray)`
   - Replace with: `.ThemeKey(ThemeKeys.SecondaryText)`
   
   - Search: `.TextColor(Colors.LightGray)` with `.FontSize(12)`
   - Replace with: `.ThemeKey(ThemeKeys.MutedText)`

2. **Card Backgrounds**:
   - Search: `.BackgroundColor(Colors.White)` on `Border()` instances
   - Replace entire Border styling with: `.ThemeKey(ThemeKeys.Card)`

3. **Strokes/Outlines**:
   - Search: `.Stroke(Colors.LightGray)`
   - If it's a card, use `.ThemeKey(ThemeKeys.Card)` 
   - Otherwise: `.Stroke(ApplicationTheme.IsLightTheme ? AppColors.Light.Outline : AppColors.Dark.Outline)`

### Example Transformations

#### BeanManagementPage.cs (26 instances)
```csharp
// BEFORE
Border()
    .Stroke(Colors.LightGray)
    .BackgroundColor(Colors.White)
    .Padding(12)
    .Content(
        Grid()
            .ColumnDefinitions("*, Auto")
            .RowDefinitions("Auto, Auto")
            .Children(
                Label(bean.Name)
                    .FontSize(16)
                    .TextColor(Colors.Black),
                Label(bean.Roaster)
                    .FontSize(14)
                    .TextColor(Colors.Gray)
            )
    )

// AFTER
Border()
    .ThemeKey(ThemeKeys.Card)  // Replaces Stroke, BackgroundColor, Padding
    .Content(
        Grid()
            .ColumnDefinitions("*, Auto")
            .RowDefinitions("Auto, Auto")
            .Children(
                Label(bean.Name)
                    .FontSize(16),  // Uses default theme TextPrimary
                Label(bean.Roaster)
                    .ThemeKey(ThemeKeys.SecondaryText)  // Replaces FontSize + TextColor
            )
    )
```

## Testing Checklist

After fixes, verify:
- [ ] All pages display coffee colors in light mode
- [ ] All pages display coffee colors in dark mode  
- [ ] No white/gray borders visible
- [ ] Text is readable (passes visual WCAG check)
- [ ] Cards have consistent coffee-themed backgrounds using ThemeKeys
- [ ] Labels use semantic theme keys (SecondaryText, MutedText, Caption)
- [ ] Settings page matches theme
- [ ] Switching between light/dark themes updates all themed elements

## Quick Reference

### Import Statement
```csharp
using BaristaNotes.Styles;  // For ThemeKeys access
```

### Common Replacements
```csharp
// Text
Label().TextColor(Colors.Gray) 
    → Label().ThemeKey(ThemeKeys.SecondaryText)

Label().FontSize(12).TextColor(Colors.LightGray) 
    → Label().ThemeKey(ThemeKeys.MutedText)

// Containers
Border().BackgroundColor(Colors.White).Stroke(Colors.LightGray) 
    → Border().ThemeKey(ThemeKeys.Card)

Border().BackgroundColor(Colors.LightGray) 
    → Border().ThemeKey(ThemeKeys.CardVariant)
```

### Available Theme Keys
```csharp
ThemeKeys.SecondaryText   // 14pt, secondary text color
ThemeKeys.MutedText       // 12pt, muted text color  
ThemeKeys.Caption         // 12pt, secondary text color
ThemeKeys.Card            // Surface background, outline stroke, rounded
ThemeKeys.CardVariant     // SurfaceVariant background, outline stroke, rounded
```

### When to Use Direct Colors
Only when theme keys don't fit your specific needs:
```csharp
.TextColor(ApplicationTheme.IsLightTheme 
    ? AppColors.Light.TextSecondary 
    : AppColors.Dark.TextSecondary)
```

