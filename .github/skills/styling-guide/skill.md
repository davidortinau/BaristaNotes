---
name: styling-guide
description: Applies colors, typography, spacing, sizing, visual states, and other styling attributes for BaristaNotes .NET MAUI app. Use this skill when implementing UI components, updating styles, or ensuring visual consistency across the app.
---

# BaristaNotes .NET MAUI Styling Guide

## Overview

Use this skill when implementing or modifying visual elements in BaristaNotes. It provides the definitive color palette, typography, spacing system, theme keys, and coding patterns for consistent UI.

**Keywords**: MAUI styling, MauiReactor themes, coffee app colors, dark mode, light mode, AppColors, ThemeKeys, spacing, typography, visual design

## Extension Philosophy

When implementing new UI features:

### Prefer Reuse Over Creation
1. **Search existing resources first** - Check `AppColors`, `ThemeKeys`, `AppIcons`, and `MaterialSymbolsFont` before creating anything new
2. **Use the closest existing option** - If an 80% match exists, use it rather than creating a 100% match
3. **Extend only when necessary** - Add new tokens only when existing ones genuinely don't fit the use case

### When Adding New Resources
- **Colors**: Must complement the existing coffee palette (warm browns, cream tones). Never override semantic colors (Primary, Success, Error, etc.)
- **Theme Keys**: Follow naming conventions in `ThemeKeys.cs`. Reuse existing styling patterns from `ApplicationTheme.cs`
- **Icons**: Search `MaterialSymbolsFont.cs` (3,700+ icons) before requesting new assets. Add to `AppIcons.cs` only if the icon will be reused across multiple components
- **Spacing/Typography**: Use existing `AppSpacing` and `AppFontSizes` tokens. These scales are intentionally limited for consistency

## Color Palette

All colors are defined in `AppColors.cs`. Use theme-aware colors via `AppColors.Light.*` or `AppColors.Dark.*`.

### Semantic Colors (Same in Both Themes)

| Color   | Hex       | Usage                    |
|---------|-----------|--------------------------|
| Success | `#4CAF50` | Positive states, confirmations |
| Warning | `#FFA726` | Caution states, alerts   |
| Error   | `#EF5350` | Error states, destructive actions |
| Info    | `#42A5F5` | Informational states     |

### Light Mode Coffee Palette

| Token          | Hex       | Usage                        |
|----------------|-----------|------------------------------|
| Background     | `#D2BCA5` | Page backgrounds             |
| Surface        | `#FCEFE1` | Cards, containers            |
| SurfaceVariant | `#ECDAC4` | Secondary surfaces, inputs   |
| SurfaceElevated| `#FFF7EC` | Elevated cards, modals       |
| Primary        | `#86543F` | Buttons, accents, brand      |
| OnPrimary      | `#F8F6F4` | Text on primary color        |
| TextPrimary    | `#352B23` | Main text                    |
| TextSecondary  | `#7C7067` | Secondary labels             |
| TextMuted      | `#A38F7D` | Disabled, placeholder text   |
| Outline        | `#D7C5B2` | Borders, dividers            |

### Dark Mode Coffee Palette

| Token          | Hex       | Usage                        |
|----------------|-----------|------------------------------|
| Background     | `#48362E` | Page backgrounds             |
| Surface        | `#48362E` | Cards, containers            |
| SurfaceVariant | `#7D5A45` | Secondary surfaces, inputs   |
| SurfaceElevated| `#B3A291` | Elevated cards, modals       |
| Primary        | `#86543F` | Buttons, accents, brand      |
| OnPrimary      | `#F8F6F4` | Text on primary color        |
| TextPrimary    | `#F8F6F4` | Main text                    |
| TextSecondary  | `#C5BFBB` | Secondary labels             |
| TextMuted      | `#A19085` | Disabled, placeholder text   |
| Outline        | `#5A463B` | Borders, dividers            |

### Using Colors in Code

```csharp
// ✅ CORRECT: Theme-aware colors
.BackgroundColor(AppColors.Light.Surface)  // or AppColors.Dark.Surface
.TextColor(AppColors.Dark.TextPrimary)

// ✅ CORRECT: With theme check
var bgColor = ApplicationTheme.IsLightTheme ? AppColors.Light.Surface : AppColors.Dark.Surface;

// ❌ WRONG: Hardcoded hex values
.BackgroundColor(Color.FromArgb("#48362E"))  // Use AppColors instead
```

## Typography

### Font Families

| Font Family     | Alias            | Usage              |
|-----------------|------------------|--------------------|
| Manrope-Regular | `Manrope`        | Body text, labels  |
| Manrope-SemiBold| `ManropeSemibold`| Headlines, emphasis|
| MaterialSymbols | `MaterialIcons`  | Icons              |
| coffee-icons    | `coffee-icons`   | Custom coffee icons|

### Font Sizes (AppFontSizes)

| Token       | Size (dp) | Usage                    |
|-------------|-----------|--------------------------|
| Display     | 36        | Hero text, large numbers |
| TitleLarge  | 28        | Page titles              |
| TitleMedium | 22        | Section headers          |
| BodyLarge   | 18        | Emphasized body text     |
| BodyMedium  | 16        | Default body text        |
| BodySmall   | 14        | Default UI text          |
| Caption     | 12        | Captions, timestamps     |

### Typography in Code

```csharp
// Use constants from AppFontSizes
Label()
    .FontFamily("Manrope")
    .FontSize(AppFontSizes.TitleMedium)
    .TextColor(AppColors.Dark.TextPrimary)

// For headlines
Label()
    .FontFamily("ManropeSemibold")
    .FontSize(AppFontSizes.TitleLarge)
```

## Spacing System (AppSpacing)

| Token | Size (dp) | Usage                     |
|-------|-----------|---------------------------|
| XS    | 4         | Tight spacing, inline     |
| S     | 8         | Small gaps, list items    |
| M     | 16        | Standard padding/margins  |
| L     | 24        | Section spacing           |
| XL    | 32        | Large gaps                |
| XXL   | 48        | Page-level spacing        |

### Spacing in Code

```csharp
// ✅ CORRECT: Use AppSpacing constants
.Padding(AppSpacing.M)
.Margin(AppSpacing.S, AppSpacing.M)
VStack(spacing: AppSpacing.S)

// ❌ WRONG: Magic numbers
.Padding(16)  // Use AppSpacing.M instead
```

## Theme Keys

Use `ThemeKeys` constants with `.ThemeKey()` for consistent, themed components.

### Label Theme Keys

| Key           | Description                    |
|---------------|--------------------------------|
| Headline      | Large bold headlines (32pt)    |
| SubHeadline   | Section titles (24pt)          |
| PrimaryText   | Primary-colored text           |
| SecondaryText | Secondary gray text            |
| MutedText     | Disabled/placeholder text      |
| Caption       | Small captions                 |
| CardTitle     | Card header text (16pt bold)   |
| CardSubtitle  | Card subtitle text             |
| FormTitle     | Form section headers           |
| FormLabel     | Input labels                   |

### Border Theme Keys

| Key          | Description                      |
|--------------|----------------------------------|
| Card         | Standard card (12px corners)     |
| CardVariant  | Variant surface card             |
| CardBorder   | Card with 1px border             |
| SelectedCard | Selected state (primary border)  |
| InputBorder  | Text input container             |
| BottomSheet  | Bottom sheet background          |
| PromptDetails| Info/prompt container            |

### Button Theme Keys

| Key             | Description                    |
|-----------------|--------------------------------|
| PrimaryButton   | Primary CTA (filled primary)   |
| SecondaryButton | Secondary action (variant bg)  |
| DangerButton    | Destructive action (error bg)  |

### Using Theme Keys

```csharp
// ✅ CORRECT: Apply theme keys for consistent styling
Label("Welcome")
    .ThemeKey(ThemeKeys.Headline)

Border(
    // content
)
    .ThemeKey(ThemeKeys.Card)

Button("Save")
    .ThemeKey(ThemeKeys.PrimaryButton)

Entry()
    .ThemeKey(ThemeKeys.Entry)
```

## Icons

### Icon Fonts

- **Material Symbols**: Use `MaterialSymbolsFont.*` constants from `Fonts` namespace
- **Coffee Icons**: Use font family `"coffee-icons"` with glyph codes

### Common Icons (AppIcons)

```csharp
// Pre-configured FontImageSource instances
AppIcons.CoffeeCup      // Coffee cup icon (NOT ☕ emoji!)
AppIcons.Feed           // Activity feed
AppIcons.Settings       // Settings gear
AppIcons.Edit           // Edit pencil
AppIcons.Delete         // Delete trash
AppIcons.Add            // Add plus
AppIcons.Ai             // AI/magic wand
AppIcons.Filter         // Filter list
AppIcons.FilterActive   // Active filter (primary color)

// Rating icons (0-4 scale) - NEVER use emoji!
AppIcons.GetRatingIcon(rating)  // Returns MaterialSymbolsFont sentiment icon
// 0 = Sentiment_very_dissatisfied (Terrible)
// 1 = Sentiment_dissatisfied (Bad)
// 2 = Sentiment_neutral (Average)
// 3 = Sentiment_satisfied (Good)
// 4 = Sentiment_very_satisfied (Excellent)
```

### Icon Usage

```csharp
// Using AppIcons
ImageButton()
    .Source(AppIcons.Edit)

// Using MaterialSymbolsFont directly
Label()
    .FontFamily(MaterialSymbolsFont.FontFamily)
    .Text(MaterialSymbolsFont.Coffee)
    .FontSize(24)
```

## MANDATORY UI Guidelines

### 🚫 NO EMOJIS (NON-NEGOTIABLE)

Emojis are **ABSOLUTELY PROHIBITED** in all user interface code. This includes but is not limited to: ☕, ⭐, ⚠️, ✓, ✕, ℹ️, and ANY Unicode emoji character.

```csharp
// ❌ ABSOLUTELY FORBIDDEN - Never use emojis
Label("☕ Coffee")           // BANNED
Label("⭐ Rating: 4")        // BANNED
Label("⚠️ Warning")          // BANNED

// ✅ CORRECT: Use MaterialSymbolsFont icons
Label()
    .FontFamily(MaterialSymbolsFont.FontFamily)
    .Text(MaterialSymbolsFont.Coffee)

Label()
    .FontFamily(MaterialSymbolsFont.FontFamily)
    .Text(MaterialSymbolsFont.Warning)
```

**Why**: Emojis render inconsistently across platforms, break accessibility (screen readers), and violate professional UI standards.

### ThemeKey System (MANDATORY)

**Never use inline styling methods**. All styling MUST use the ThemeKey system for consistency and theme support.

```csharp
// ❌ WRONG: Inline styling is PROHIBITED
Label("Title")
    .FontSize(24)
    .TextColor(Colors.Brown)
    .FontAttributes(FontAttributes.Bold)

// ✅ CORRECT: Always use ThemeKeys
Label("Title")
    .ThemeKey(ThemeKeys.Headline)
```

Before creating new theme keys, check existing keys in:
- `ThemeKeys.cs` - Theme key constants
- `ApplicationTheme.cs` - Theme implementations

### Accessibility (WCAG 2.1 Level AA)

- All interactive elements must be keyboard navigable
- Screen-reader compatible labels required
- Minimum contrast ratios must be maintained
- Touch targets minimum 44x44dp

### Deprecated APIs - DO NOT USE

```csharp
// ❌ NEVER use these deprecated controls:
Frame()      // Use Border() instead
ListView()   // Use CollectionView() instead
TableView()  // Use CollectionView() instead
```

### Rounded Corners - Use Border, NOT BoxView

```csharp
// ❌ WRONG: BoxView corner radius distorts on rotation
BoxView()
    .BackgroundColor(backgroundColor)
    .CornerRadius(25)

// ✅ CORRECT: Border with RoundRectangle
Border()
    .BackgroundColor(backgroundColor)
    .StrokeThickness(0)
    .StrokeShape(new RoundRectangle { CornerRadius = 25 })
```

### Minimum Touch Targets

Always ensure interactive elements have minimum 44x44dp touch targets:

```csharp
Button()
    .MinimumHeightRequest(44)
    .MinimumWidthRequest(44)
```

## Component Patterns

### Card Component

```csharp
Border(
    VStack(spacing: AppSpacing.S,
        Label(title).ThemeKey(ThemeKeys.CardTitle),
        Label(subtitle).ThemeKey(ThemeKeys.CardSubtitle)
    )
)
    .ThemeKey(ThemeKeys.Card)
```

### Form Input

```csharp
VStack(spacing: AppSpacing.XS,
    Label("Label").ThemeKey(ThemeKeys.FormLabel),
    Border(
        Entry()
            .Placeholder("Enter value")
            .ThemeKey(ThemeKeys.Entry)
    )
        .ThemeKey(ThemeKeys.InputBorder)
)
```

### Primary Action Button

```csharp
Button("Save")
    .ThemeKey(ThemeKeys.PrimaryButton)
    .OnClicked(HandleSave)
```

### Danger/Delete Button

```csharp
Button("Delete")
    .ThemeKey(ThemeKeys.DangerButton)
    .OnClicked(HandleDelete)
```

## Orientation Handling

When UI elements need to respond to orientation changes:

```csharp
private class State
{
    public DisplayOrientation Orientation { get; set; } = DisplayOrientation.Portrait;
}

protected override void OnMounted()
{
    DeviceDisplay.MainDisplayInfoChanged += OnDisplayInfoChanged;
    SetState(s => s.Orientation = DeviceDisplay.MainDisplayInfo.Orientation);
    base.OnMounted();
}

protected override void OnWillUnmount()
{
    DeviceDisplay.MainDisplayInfoChanged -= OnDisplayInfoChanged;
    base.OnWillUnmount();
}

private void OnDisplayInfoChanged(object? sender, DisplayInfoChangedEventArgs e)
{
    SetState(s => s.Orientation = e.DisplayInfo.Orientation);
}
```

## File References

| File | Purpose |
|------|---------|
| `Resources/Styles/AppColors.cs` | Color palette definitions |
| `Resources/Styles/AppFontSizes.cs` | Typography scale |
| `Resources/Styles/AppSpacing.cs` | Spacing system |
| `Resources/Styles/ThemeKeys.cs` | Theme key constants |
| `Resources/Styles/ApplicationTheme.cs` | MauiReactor theme setup |
| `Resources/Styles/AppIcons.cs` | Icon definitions |
| `Components/MaterialSymbolsFont.cs` | Material icon glyphs |